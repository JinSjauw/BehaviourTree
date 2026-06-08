using BehaviourTree.Core;
using BehaviourTree.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BehaviourTree.Runtime 
{
    public static class TreeBaker
    {
        public static BlackboardDefinition BakeTree(BehaviourNode root, BehaviourTreeAssetBase asset, ref NodeData[] nodeDatas, ref FieldData[] fieldDatas, ref string[] nodeGuids, out int maxTreeDepth)
        {
            if (root == null)
            {
                nodeDatas = Array.Empty<NodeData>();
                fieldDatas = Array.Empty<FieldData>();
                nodeGuids = Array.Empty<string>();
                maxTreeDepth = 0;
                return null;
            }

            BehaviourNode effectiveRoot = GetEffectiveRoot(root);

            BlackboardDefinition runtimeBbDef = ScriptableObject.CreateInstance<BlackboardDefinition>();
            runtimeBbDef.name = "RuntimeMerged_BB";

            BlackboardDefinition selfDef = asset != null ? asset.BlackboardDefinition : null;

            // 1. Copy self BB variables
            if (selfDef != null && selfDef.sharedVariables != null)
            {
                runtimeBbDef.sharedVariables.AddRange(selfDef.sharedVariables);
            }

            // 2. Append commander BB variables (preserving stride for per-agent arrays)
            CommanderBlackboardDefinition commanderDef = asset != null ? asset.CommanderBlackboardDefinition : null;

            if (commanderDef != null && commanderDef.sharedVariables != null)
            {
                for (int i = 0; i < commanderDef.sharedVariables.Count; i++)
                {
                    BlackboardVariable variable = commanderDef.sharedVariables[i];
                    variable.stride = 1; // Per-agent indexing handled by bridge — agent side only needs one slot
                    runtimeBbDef.sharedVariables.Add(variable);
                }
            }

            Dictionary<string, int> rootVarIndexByName = new Dictionary<string, int>();
            for (int i = 0; i < runtimeBbDef.sharedVariables.Count; i++)
            {
                string name = runtimeBbDef.sharedVariables[i].name;
                if (!string.IsNullOrEmpty(name) && !rootVarIndexByName.ContainsKey(name))
                    rootVarIndexByName[name] = i;
            }

            Dictionary<string, Dictionary<string, int>> scopeVarIndexByName = new Dictionary<string, Dictionary<string, int>>();

            List<BakedNodeInstance> instances = new List<BakedNodeInstance>();
            List<int> firstChild = new List<int>();
            List<int> lastChild = new List<int>();
            Dictionary<string, int> runtimeGuidToIndex = new Dictionary<string, int>();

            int rootIndex = EnsureInstance(effectiveRoot, string.Empty, instances, firstChild, lastChild, runtimeGuidToIndex);
            HashSet<BehaviourTreeAssetBase> expandingSubtrees = new HashSet<BehaviourTreeAssetBase>();
            HashSet<int> processedIndices = new HashSet<int>();
            maxTreeDepth = 0;
            ProcessChildren(rootIndex, instances, firstChild, lastChild, runtimeGuidToIndex, scopeVarIndexByName, runtimeBbDef, rootVarIndexByName, expandingSubtrees, processedIndices, ref maxTreeDepth);

            nodeDatas = new NodeData[instances.Count];
            nodeGuids = new string[instances.Count];

            int totalFieldDataCount = 0;
            for (int i = 0; i < instances.Count; i++)
            {
                BehaviourNode node = instances[i].node;
                if (node is LeafNode action)
                {
                    totalFieldDataCount += CountFieldDataForNode(action, runtimeBbDef);
                }
                else if (node is DecoratorNode decorator)
                {
                    totalFieldDataCount += CountFieldDataForNode(decorator, runtimeBbDef);
                }
            }

            fieldDatas = new FieldData[totalFieldDataCount];
            FillNodeData(nodeDatas, fieldDatas, nodeGuids, instances, firstChild, lastChild, scopeVarIndexByName, runtimeBbDef, rootVarIndexByName);
            return runtimeBbDef;
        }

        private readonly struct BakedNodeInstance
        {
            public readonly BehaviourNode node;
            public readonly string scopePrefix;
            public readonly string runtimeGuid;

            public BakedNodeInstance(BehaviourNode node, string scopePrefix, string runtimeGuid)
            {
                this.node = node;
                this.scopePrefix = scopePrefix;
                this.runtimeGuid = runtimeGuid;
            }
        }

        private static BehaviourNode GetEffectiveRoot(BehaviourNode root)
        {
            if (root != null && root.NodeType == BehaviourNodeType.ROOT && root.children.Count > 0)
                return root.children.First();
            return root;
        }

        private static BehaviourNode GetEffectiveRoot(BehaviourTreeAssetBase authoring)
        {
            if (authoring == null) return null;
            return GetEffectiveRoot(authoring.Root);
        }

        private static int EnsureInstance(BehaviourNode node, string scopePrefix, List<BakedNodeInstance> instances, List<int> firstChild, List<int> lastChild, Dictionary<string, int> runtimeGuidToIndex)
        {
            string runtimeGuid = string.IsNullOrEmpty(scopePrefix) ? node.guid : scopePrefix + "/" + node.guid;
            if (runtimeGuidToIndex.TryGetValue(runtimeGuid, out int nodeIndex))
                return nodeIndex;

            nodeIndex = instances.Count;
            instances.Add(new BakedNodeInstance(node, scopePrefix, runtimeGuid));
            firstChild.Add(-1);
            lastChild.Add(-1);
            runtimeGuidToIndex[runtimeGuid] = nodeIndex;
            return nodeIndex;
        }

        private static void ProcessChildren(
            int index,
            List<BakedNodeInstance> instances,
            List<int> firstChild,
            List<int> lastChild,
            Dictionary<string, int> runtimeGuidToIndex,
            Dictionary<string, Dictionary<string, int>> scopeVarIndexByName,
            BlackboardDefinition runtimeBbDef,
            Dictionary<string, int> rootVarIndexByName,
            HashSet<BehaviourTreeAssetBase> expandingSubtrees,
            HashSet<int> processedIndices,
            ref int maxTreeDepth,
            int iterationDepth = 0)
        {
            const int maxIteration = 500;
            if (iterationDepth > maxIteration)
            {
                BehaviourNode overflowNode = instances[index].node;
                Debug.LogError($"[TreeBaker] Max recursion depth ({maxIteration}) exceeded at node '{overflowNode?.name}' (guid: {overflowNode?.guid}). The tree likely contains a cycle.");
                return;
            }

            if (iterationDepth > maxTreeDepth)
                maxTreeDepth = iterationDepth;

            BehaviourNode node = instances[index].node;
            string scopePrefix = instances[index].scopePrefix;
            string runtimeGuid = instances[index].runtimeGuid;

            if (!processedIndices.Add(index))
                return;

            List<(BehaviourNode child, string childScope)> logicalChildren = new List<(BehaviourNode, string)>();

            if (node.NodeType == BehaviourNodeType.SUBTREE)
            {
                SubtreeNode subtreeNode = node as SubtreeNode;
                BehaviourTreeAssetBase subtreeAsset = subtreeNode != null ? subtreeNode.SubTreeAsset : null;
                if (subtreeAsset != null && !expandingSubtrees.Add(subtreeAsset))
                {
                    Debug.LogWarning($"[TreeBaker] Circular subtree reference detected: '{subtreeAsset.DisplayName}'. Skipping expansion.");
                }
                else if (subtreeAsset != null)
                {
                    BehaviourNode subRoot = GetEffectiveRoot(subtreeAsset);
                    if (subRoot != null)
                    {
                        if (subRoot.NodeType == BehaviourNodeType.ROOT && subRoot.children.Count == 0)
                        {
                            Debug.LogWarning($"[TreeBaker] Subtree '{subtreeAsset.DisplayName}' contains only a ROOT node with no children. Skipping expansion.");
                        }
                        else
                        {
                            string childScope = runtimeGuid;
                            EnsureScopeMapping(childScope, scopePrefix, subtreeNode, runtimeBbDef, rootVarIndexByName, scopeVarIndexByName);
                            logicalChildren.Add((subRoot, childScope));
                        }
                    }
                }
            }
            else
            {
                for (int i = 0; i < node.children.Count; i++)
                {
                    BehaviourNode child = node.children[i];
                    if (child == null) continue;
                    logicalChildren.Add((child, scopePrefix));
                }
            }

            List<int> childIndices = new List<int>(logicalChildren.Count);
            for (int i = 0; i < logicalChildren.Count; i++)
            {
                int childIndex = EnsureInstance(logicalChildren[i].child, logicalChildren[i].childScope, instances, firstChild, lastChild, runtimeGuidToIndex);
                childIndices.Add(childIndex);
                if (i == 0) firstChild[index] = childIndex;
                if (i == logicalChildren.Count - 1) lastChild[index] = childIndex;
            }

            for (int i = 0; i < childIndices.Count; i++)
            {
                ProcessChildren(childIndices[i], instances, firstChild, lastChild, 
                runtimeGuidToIndex, 
                scopeVarIndexByName, 
                runtimeBbDef, 
                rootVarIndexByName, 
                expandingSubtrees, 
                processedIndices, 
                ref maxTreeDepth, iterationDepth + 1);
            }

            if (node.NodeType == BehaviourNodeType.SUBTREE)
            {
                SubtreeNode subtreeNode = node as SubtreeNode;
                if (subtreeNode != null && subtreeNode.SubTreeAsset != null)
                    expandingSubtrees.Remove(subtreeNode.SubTreeAsset);
            }
        }

        private static void EnsureScopeMapping(
            string childScopePrefix,
            string parentScopePrefix,
            SubtreeNode subtreeNode,
            BlackboardDefinition runtimeBbDef,
            Dictionary<string, int> rootVarIndexByName,
            Dictionary<string, Dictionary<string, int>> scopeVarIndexByName)
        {
            if (string.IsNullOrEmpty(childScopePrefix)) return;
            if (scopeVarIndexByName.ContainsKey(childScopePrefix)) return;

            Dictionary<string, int> map = new Dictionary<string, int>();
            BehaviourTreeAssetBase subtreeAsset = subtreeNode != null ? subtreeNode.SubTreeAsset : null;
            BlackboardDefinition subtreeDef = subtreeAsset != null ? subtreeAsset.BlackboardDefinition : null;

            if (subtreeDef != null && subtreeDef.sharedVariables != null)
            {
                for (int i = 0; i < subtreeDef.sharedVariables.Count; i++)
                {
                    BlackboardVariable subVar = subtreeDef.sharedVariables[i];
                    int mappedIndex = ResolveSubtreeVarIndex(subtreeNode, subVar, childScopePrefix, runtimeBbDef, rootVarIndexByName, parentScopePrefix, scopeVarIndexByName);
                    map[subVar.name] = mappedIndex;
                }
            }

            scopeVarIndexByName[childScopePrefix] = map;
        }

        private static int ResolveSubtreeVarIndex(
            SubtreeNode subtreeNode,
            BlackboardVariable subtreeVar,
            string childScopePrefix,
            BlackboardDefinition runtimeBbDef,
            Dictionary<string, int> rootVarIndexByName,
            string parentScopePrefix,
            Dictionary<string, Dictionary<string, int>> scopeVarIndexByName)
        {
            if (subtreeNode != null && subtreeNode.bindings != null)
            {
                for (int i = 0; i < subtreeNode.bindings.Count; i++)
                {
                    SubtreeBinding b = subtreeNode.bindings[i];
                    if (b.subtreeVariableName == subtreeVar.name && !string.IsNullOrEmpty(b.parentVariableName))
                    {
                        if (rootVarIndexByName.TryGetValue(b.parentVariableName, out int mappedIndex))
                        {
                            return mappedIndex;
                        }
                        if (!string.IsNullOrEmpty(parentScopePrefix) &&
                            scopeVarIndexByName.TryGetValue(parentScopePrefix, out var parentMap) &&
                            parentMap.TryGetValue(b.parentVariableName, out int parentIdx))
                        {
                            return parentIdx;
                        }

                        Debug.LogWarning($"Subtree binding not found: {b.parentVariableName} returning -1");

                        return -1;
                    }
                }
            }

            BlackboardVariable runtimeVar = subtreeVar;
            runtimeVar.name = childScopePrefix + "/" + subtreeVar.name;
            int newIndex = runtimeBbDef.sharedVariables.Count;
            runtimeBbDef.sharedVariables.Add(runtimeVar);
            return newIndex;
        }

        private static void FillNodeData(
            NodeData[] nodeDataArray,
            FieldData[] fieldDataArray,
            string[] nodeGuids,
            List<BakedNodeInstance> instances,
            List<int> firstChild,
            List<int> lastChild,
            Dictionary<string, Dictionary<string, int>> scopeVarIndexByName,
            BlackboardDefinition runtimeBbDef,
            Dictionary<string, int> rootVarIndexByName)
        {
            int currentFieldDataOffset = 0;
            for (int i = 0; i < instances.Count; i++)
            {
                BehaviourNode node = instances[i].node;
                string scopePrefix = instances[i].scopePrefix;

                nodeGuids[i] = instances[i].runtimeGuid;
                if (string.IsNullOrEmpty(scopePrefix))
                    node.runtimeIndex = i;

                NodeData nodeData = new NodeData
                {
                    nodeType = node.NodeType,
                    firstChildIndex = firstChild[i],
                    lastChildIndex = lastChild[i],
                    methodID = MethodID.NONE,
                    blackBoardTypeID = BlackBoardType.SELF,
                    fieldDataStartIndex = -1,
                    fieldDataCount = 0,
                };

                if (node.NodeType == BehaviourNodeType.ACTION || node.NodeType == BehaviourNodeType.CONDITION)
                {
                    LeafNode action = (LeafNode)node;
                    nodeData.methodID = action.methodID;
                    nodeData.blackBoardTypeID = action.BlackBoardTypeID;
                    nodeData.fieldDataStartIndex = currentFieldDataOffset;
                    nodeData.fieldDataCount = CountFieldDataForNode(action, runtimeBbDef);

                    if (action.fieldEntries != null)
                    {
                        Dictionary<string, int> map = GetScopeMap(scopePrefix, scopeVarIndexByName, rootVarIndexByName);
                        for (int fieldIndex = 0; fieldIndex < action.fieldEntries.Count; fieldIndex++)
                        {
                            PackFieldEntryWithArray(action.fieldEntries[fieldIndex], map, runtimeBbDef, fieldDataArray, ref currentFieldDataOffset);
                        }
                    }
                }
                else if (node.NodeType == BehaviourNodeType.DECORATOR)
                {
                    DecoratorNode decorator = (DecoratorNode)node;
                    nodeData.methodID = decorator.methodID;
                    nodeData.blackBoardTypeID = decorator.BlackBoardTypeID;
                    nodeData.firstChildIndex = firstChild[i];
                    nodeData.lastChildIndex = firstChild[i];
                    nodeData.fieldDataStartIndex = currentFieldDataOffset;
                    nodeData.fieldDataCount = CountFieldDataForNode(decorator, runtimeBbDef);

                    if (decorator.fieldEntries != null)
                    {
                        Dictionary<string, int> map = GetScopeMap(scopePrefix, scopeVarIndexByName, rootVarIndexByName);
                        for (int fieldIndex = 0; fieldIndex < decorator.fieldEntries.Count; fieldIndex++)
                        {
                            PackFieldEntryWithArray(decorator.fieldEntries[fieldIndex], map, runtimeBbDef, fieldDataArray, ref currentFieldDataOffset);
                        }
                    }
                }
                else if (node.NodeType == BehaviourNodeType.SUBTREE)
                {
                    nodeData.firstChildIndex = firstChild[i];
                    nodeData.lastChildIndex = firstChild[i];
                }

                nodeDataArray[i] = nodeData;
            }
        }

        private static Dictionary<string, int> GetScopeMap(string scopePrefix, Dictionary<string, Dictionary<string, int>> scopeVarIndexByName, Dictionary<string, int> rootVarIndexByName)
        {
            if (string.IsNullOrEmpty(scopePrefix))
                return rootVarIndexByName;
            if (scopeVarIndexByName.TryGetValue(scopePrefix, out var map))
                return map;
            return rootVarIndexByName;
        }

        /// <summary>
        /// Computes the base slot offset for a variable index in the definition,
        /// accounting for strides of preceding variables.
        /// </summary>
        private static int ResolveSlotOffset(int variableIndex, BlackboardDefinition definition)
        {
            if (variableIndex < 0 || definition == null || definition.sharedVariables == null || variableIndex >= definition.sharedVariables.Count)
                return -1;

            int slotOffset = 0;
            for (int i = 0; i < variableIndex; i++)
            {
                int stride = definition.sharedVariables[i].stride;
                slotOffset += (stride > 1) ? stride : 1;
            }
            return slotOffset;
        }

        /// <summary>
        /// Returns the number of FieldData entries needed for this node's field entries,
        /// accounting for array parameters that expand to 2 entries (baseSlot + stride).
        /// </summary>
        private static int CountFieldDataForNode(LeafNode node, BlackboardDefinition runtimeBbDef)
        {
            if (node.fieldEntries == null) return 0;
            int count = 0;
            for (int i = 0; i < node.fieldEntries.Count; i++)
            {
                count += IsArrayFieldEntry(node.fieldEntries[i], runtimeBbDef) ? 2 : 1;
            }
            return count;
        }

        private static int CountFieldDataForNode(DecoratorNode node, BlackboardDefinition runtimeBbDef)
        {
            if (node.fieldEntries == null) return 0;
            int count = 0;
            for (int i = 0; i < node.fieldEntries.Count; i++)
            {
                count += IsArrayFieldEntry(node.fieldEntries[i], runtimeBbDef) ? 2 : 1;
            }
            return count;
        }

        /// <summary>
        /// Returns true if the field entry references an array variable.
        /// Trusts the serialized flag first; falls back to stride check for backwards compat.
        /// </summary>
        private static bool IsArrayFieldEntry(NodeFieldEntry entry, BlackboardDefinition runtimeBbDef)
        {
            if (!entry.isVariable || string.IsNullOrEmpty(entry.variableName)) return false;

            // Trust the serialized flag — survives BB definition changes
            if (entry.isArray) return true;

            // Fallback: old nodes without the flag use dynamic stride check
            if (runtimeBbDef == null || runtimeBbDef.sharedVariables == null) return false;

            for (int i = 0; i < runtimeBbDef.sharedVariables.Count; i++)
            {
                if (runtimeBbDef.sharedVariables[i].name == entry.variableName)
                    return runtimeBbDef.sharedVariables[i].stride > 1;
            }
            return false;
        }

        /// <summary>
        /// Packs one or two FieldData entries for the given field entry.
        /// For array variables (stride > 1): packs baseSlot + stride (constant).
        /// For scalar variables: packs single baseSlot.
        /// For constants: packs single constant value.
        /// </summary>
        private static void PackFieldEntryWithArray(
            NodeFieldEntry entry,
            Dictionary<string, int> varIndexByName,
            BlackboardDefinition runtimeBbDef,
            FieldData[] fieldDataArray,
            ref int offset)
        {
            if (!entry.isVariable)
            {
                // Constant: pack as usual
                fieldDataArray[offset++] = PackFieldEntry(entry, varIndexByName, runtimeBbDef);
                return;
            }

            // Resolve variable index and slot offset
            int varIndex = -1;
            if (varIndexByName != null && !string.IsNullOrEmpty(entry.variableName) && varIndexByName.TryGetValue(entry.variableName, out int mapped))
                varIndex = mapped;

            if (varIndex < 0 || runtimeBbDef == null || runtimeBbDef.sharedVariables == null || varIndex >= runtimeBbDef.sharedVariables.Count)
            {
                // Unresolved variable — pack as single entry with invalid slot
                fieldDataArray[offset++] = FieldData.FromVariable(-1);
                return;
            }

            int baseSlot = ResolveSlotOffset(varIndex, runtimeBbDef);
            int stride = runtimeBbDef.sharedVariables[varIndex].stride;

            if (stride > 1)
            {
                // Array parameter: pack baseSlot + stride
                fieldDataArray[offset++] = FieldData.FromVariable(baseSlot);
                fieldDataArray[offset++] = FieldData.FromConstant(stride);
            }
            else
            {
                // Scalar parameter: pack as single entry (with type validation)
                fieldDataArray[offset++] = PackFieldEntry(entry, varIndexByName, runtimeBbDef);
            }
        }

        private static FieldData PackFieldEntry(NodeFieldEntry entry, Dictionary<string, int> varIndexByName, BlackboardDefinition runtimeBbDef)
        {
            if (entry.isVariable)
            {
                int varIndex = -1;
                if (varIndexByName != null && !string.IsNullOrEmpty(entry.variableName) && varIndexByName.TryGetValue(entry.variableName, out int mapped))
                {
                    varIndex = mapped;
                }
                if (varIndex >= 0 && runtimeBbDef != null && runtimeBbDef.sharedVariables != null && varIndex < runtimeBbDef.sharedVariables.Count)
                {
                    if (!FieldTypeHelper.TryGetSystemTypeFromName(runtimeBbDef.sharedVariables[varIndex].typeName, out Type bbType) || bbType == null)
                        varIndex = -1;
                    else if (FieldTypeHelper.GetFieldType(bbType) != entry.fieldType)
                        varIndex = -1;
                }
                // Convert variable index to slot offset (accounts for stride in preceding variables)
                int slotOffset = ResolveSlotOffset(varIndex, runtimeBbDef);
                return FieldData.FromVariable(slotOffset);
            }

            switch (entry.fieldType)
            {
                case FieldType.Int:
                    return FieldData.FromConstant(entry.intValue);
                case FieldType.Float:
                    return FieldData.FromConstant(entry.floatValue);
                case FieldType.Bool:
                    return FieldData.FromConstant(entry.boolValue);
                default:
                    Debug.LogWarning($"[TreeBaker] Unsupported field type for '{entry.fieldType}' for STATIC field '{entry.fieldName}'");
                    return FieldData.FromConstant(0);
            }
        }
    }
}

