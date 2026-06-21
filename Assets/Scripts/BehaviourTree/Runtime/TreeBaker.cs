using BehaviourTree.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BehaviourTree.Runtime 
{
    public static class TreeBaker
    {
        public static BlackboardDefinition BakeTree(BehaviourNode root, BehaviourTreeAssetBase asset, ref NodeData[] nodeDatas, ref FieldData[] fieldDatas, ref string[] fieldTypeNames, ref object[] boxedConstants, ref string[] nodeGuids, out int maxTreeDepth)
        {
            if (root == null)
            {
                nodeDatas = Array.Empty<NodeData>();
                fieldDatas = Array.Empty<FieldData>();
                fieldTypeNames = Array.Empty<string>();
                boxedConstants = Array.Empty<object>();
                nodeGuids = Array.Empty<string>();
                maxTreeDepth = 0;
                return null;
            }

            BehaviourNode effectiveRoot = GetEffectiveRoot(root);

            BlackboardDefinition runtimeBbDef = ScriptableObject.CreateInstance<BlackboardDefinition>();
            runtimeBbDef.name = "RuntimeMerged_BB";
            runtimeBbDef.sourceTreeAsset = asset;

            BlackboardDefinition selfDef = asset != null ? asset.BlackboardDefinition : null;

            // 1. Copy self BB variables (generic)
            if (selfDef != null)
                CopyGenericVariables(runtimeBbDef, selfDef);

            // 2. Append commander BB variables.
            // Commander trees need the actual stride so that baked slot offsets
            // match the storage layout (which is initialized with the same stride).
            // Agent trees only need 1 slot per variable (squad data is copied in single-slot).
            BlackboardDefinition commanderDef = asset != null ? asset.CommanderBlackboardDefinition : null;
            if (commanderDef != null)
                CopyGenericVariables(runtimeBbDef, commanderDef, withStrideOfOne: !asset.PreserveCommanderStride);

            IReadOnlyList<BlackboardVariableBase> allVars = runtimeBbDef.GetAllVariables();
            Dictionary<string, int> rootVarIndexByName = new Dictionary<string, int>();
            for (int i = 0; i < allVars.Count; i++)
            {
                string name = allVars[i].Name;
                if (!string.IsNullOrEmpty(name) && !rootVarIndexByName.ContainsKey(name))
                    rootVarIndexByName[name] = i;
            }

#if UNITY_EDITOR
            Debug.Log($"[TreeBaker] rootVarIndexByName ({rootVarIndexByName.Count} entries):");
            foreach (var kvp in rootVarIndexByName)
                Debug.Log($"[TreeBaker]   '{kvp.Key}' → index {kvp.Value}");
#endif

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
                    totalFieldDataCount += CountFieldDataForNode(action.fieldEntries, runtimeBbDef);
                else if (node is DecoratorNode decorator)
                    totalFieldDataCount += CountFieldDataForNode(decorator.fieldEntries, runtimeBbDef);
                else if (node is CompositeNode composite)
                    totalFieldDataCount += CountFieldDataForNode(composite.fieldEntries, runtimeBbDef);
            }

            fieldDatas = new FieldData[totalFieldDataCount];
            List<object> boxedConstantsList = new List<object>();
            List<string> fieldTypeNamesList = new List<string>();
            FillNodeData(nodeDatas, fieldDatas, boxedConstantsList, fieldTypeNamesList, nodeGuids, instances, firstChild, lastChild, scopeVarIndexByName, runtimeBbDef, rootVarIndexByName);
            boxedConstants = boxedConstantsList.Count > 0 ? boxedConstantsList.ToArray() : Array.Empty<object>();
            fieldTypeNames = fieldTypeNamesList.Count > 0 ? fieldTypeNamesList.ToArray() : Array.Empty<string>();
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

            if (subtreeDef != null)
            {
                IReadOnlyList<BlackboardVariableBase> subtreeVars = subtreeDef.GetAllVariables();
                for (int i = 0; i < subtreeVars.Count; i++)
                {
                    BlackboardVariableBase subVar = subtreeVars[i];
                    int mappedIndex = ResolveSubtreeVarIndex(subtreeNode, subVar, childScopePrefix, runtimeBbDef, rootVarIndexByName, parentScopePrefix, scopeVarIndexByName);
                    map[subVar.Name] = mappedIndex;
                }
            }

            scopeVarIndexByName[childScopePrefix] = map;
        }

        private static int ResolveSubtreeVarIndex(
            SubtreeNode subtreeNode,
            BlackboardVariableBase subtreeVar,
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
                    if (b.subtreeVariableName == subtreeVar.Name && !string.IsNullOrEmpty(b.parentVariableName))
                    {
                        if (rootVarIndexByName.TryGetValue(b.parentVariableName, out int mappedIndex))
                            return mappedIndex;
                        if (!string.IsNullOrEmpty(parentScopePrefix) &&
                            scopeVarIndexByName.TryGetValue(parentScopePrefix, out var parentMap) &&
                            parentMap.TryGetValue(b.parentVariableName, out int parentIdx))
                            return parentIdx;

                        Debug.LogWarning($"Subtree binding not found: {b.parentVariableName} returning -1");
                        return -1;
                    }
                }
            }

            // Create a cloned generic variable for the subtree-scoped entry
            BlackboardVariableBase clone = subtreeVar.Clone();
            if (clone == null)
            {
                // Fallback: create via reflection if type can be resolved
                Type valueType = subtreeVar.GetValueType();
                if (valueType != null)
                {
                    Type sharedVarType = typeof(BlackboardVariable<>).MakeGenericType(valueType);
                    clone = (BlackboardVariableBase)Activator.CreateInstance(sharedVarType);
                }
            }

            if (clone != null)
            {
                clone.Name = childScopePrefix + "/" + subtreeVar.Name;
                clone.Stride = subtreeVar.Stride;
            }
            else
            {
                // Last resort: create object-typed stub
                clone = new BlackboardVariable<object> { Name = childScopePrefix + "/" + subtreeVar.Name, Stride = subtreeVar.Stride };
            }

            if (runtimeBbDef.sharedVariables == null)
                runtimeBbDef.sharedVariables = new List<BlackboardVariableBase>();
            int newIndex = runtimeBbDef.sharedVariables.Count;
            runtimeBbDef.sharedVariables.Add(clone);
            return newIndex;
        }

        private static void FillNodeData(
            NodeData[] nodeDataArray,
            FieldData[] fieldDataArray,
            List<object> boxedConstantsList,
            List<string> fieldTypeNamesList,
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
                        methodName = null,
                        blackBoardTypeID = BlackBoardType.SELF,
                    fieldDataStartIndex = -1,
                    fieldDataCount = 0,
                };

                if (node.NodeType == BehaviourNodeType.ACTION || node.NodeType == BehaviourNodeType.CONDITION)
                {
                    LeafNode action = (LeafNode)node;
                    nodeData.methodName = action.methodName;
                    nodeData.blackBoardTypeID = action.BlackBoardTypeID;
                    nodeData.fieldDataStartIndex = currentFieldDataOffset;
                    nodeData.fieldDataCount = CountFieldDataForNode(action.fieldEntries, runtimeBbDef);

                    if (action.fieldEntries != null)
                    {
                        Dictionary<string, int> map = GetScopeMap(scopePrefix, scopeVarIndexByName, rootVarIndexByName);
                        for (int fieldIndex = 0; fieldIndex < action.fieldEntries.Count; fieldIndex++)
                        {
                            PackFieldEntryWithArray(action.fieldEntries[fieldIndex], map, runtimeBbDef, fieldDataArray, boxedConstantsList, fieldTypeNamesList, ref currentFieldDataOffset);
                        }
                    }
                }
                else if (node.NodeType == BehaviourNodeType.DECORATOR)
                {
                    DecoratorNode decorator = (DecoratorNode)node;
                    nodeData.methodName = decorator.methodName;
                    nodeData.blackBoardTypeID = decorator.BlackBoardTypeID;
                    nodeData.firstChildIndex = firstChild[i];
                    nodeData.lastChildIndex = firstChild[i];
                    nodeData.fieldDataStartIndex = currentFieldDataOffset;
                    nodeData.fieldDataCount = CountFieldDataForNode(decorator.fieldEntries, runtimeBbDef);

                    if (decorator.fieldEntries != null)
                    {
                        Dictionary<string, int> map = GetScopeMap(scopePrefix, scopeVarIndexByName, rootVarIndexByName);
                        for (int fieldIndex = 0; fieldIndex < decorator.fieldEntries.Count; fieldIndex++)
                        {
                            PackFieldEntryWithArray(decorator.fieldEntries[fieldIndex], map, runtimeBbDef, fieldDataArray, boxedConstantsList, fieldTypeNamesList, ref currentFieldDataOffset);
                        }
                    }
                }
                else if (node.NodeType == BehaviourNodeType.COMPOSITE)
                {
                    CompositeNode composite = (CompositeNode)node;
                    nodeData.methodName = composite.methodName;
                    nodeData.fieldDataStartIndex = currentFieldDataOffset;
                    nodeData.fieldDataCount = CountFieldDataForNode(composite.fieldEntries, runtimeBbDef);
                    nodeData.abortType = composite.abortType;

                    if (composite.fieldEntries != null)
                    {
                        Dictionary<string, int> map = GetScopeMap(scopePrefix, scopeVarIndexByName, rootVarIndexByName);
                        for (int fieldIndex = 0; fieldIndex < composite.fieldEntries.Count; fieldIndex++)
                        {
                            PackFieldEntryWithArray(composite.fieldEntries[fieldIndex], map, runtimeBbDef, fieldDataArray, boxedConstantsList, fieldTypeNamesList, ref currentFieldDataOffset);
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
        /// Copies generic variables from a source definition into the merged runtime definition.
        /// Clones each variable to avoid mutating the source asset.
        /// </summary>
        private static void CopyGenericVariables(BlackboardDefinition target, BlackboardDefinition source, bool withStrideOfOne = false)
        {
            if (source.sharedVariables == null)
                return;
            if (target.sharedVariables == null)
                target.sharedVariables = new List<BlackboardVariableBase>();

            for (int i = 0; i < source.sharedVariables.Count; i++)
            {
                BlackboardVariableBase sourceVar = source.sharedVariables[i];
                BlackboardVariableBase clone = sourceVar.Clone();

                if (clone == null)
                {
                    // Fallback via reflection
                    Type valueType = sourceVar.GetValueType();
                    if (valueType != null)
                    {
                        Type sharedVarType = typeof(BlackboardVariable<>).MakeGenericType(valueType);
                        clone = (BlackboardVariableBase)Activator.CreateInstance(sharedVarType);
                        clone.Name = sourceVar.Name;
                        clone.Stride = sourceVar.Stride;
                    }
                }

                if (clone == null)
                {
                    // Last resort stub
                    clone = new BlackboardVariable<object> { Name = sourceVar.Name, Stride = sourceVar.Stride };
                }

                if (withStrideOfOne)
                    clone.Stride = 1;

                target.sharedVariables.Add(clone);
            }
        }

        /// <summary>
        /// Computes the base slot offset for a variable index in the definition,
        /// accounting for strides of preceding variables.
        /// </summary>
        private static int ResolveSlotOffset(int variableIndex, BlackboardDefinition definition)
        {
            IReadOnlyList<BlackboardVariableBase> allVars = definition.GetAllVariables();
            if (variableIndex < 0 || allVars == null || variableIndex >= allVars.Count)
                return -1;

            int slotOffset = 0;
            for (int i = 0; i < variableIndex; i++)
            {
                int stride = allVars[i].Stride;
                slotOffset += (stride > 1) ? stride : 1;
            }
            return slotOffset;
        }

        /// <summary>
        /// Resolves the System.Type for a field entry from its stored assembly-qualified type name.
        /// </summary>
        private static Type ResolveFieldType(NodeFieldEntry entry)
        {
            if (!string.IsNullOrEmpty(entry.fieldTypeName))
                return Type.GetType(entry.fieldTypeName);
            return null;
        }

        private static int CountFieldDataForNode(List<NodeFieldEntry> fieldEntries, BlackboardDefinition runtimeBbDef)
        {
            if (fieldEntries == null) return 0;
            int count = 0;
            for (int i = 0; i < fieldEntries.Count; i++)
            {
                count += IsArrayFieldEntry(fieldEntries[i], runtimeBbDef) ? 2 : 1;
            }
            return count;
        }

        private static bool IsArrayFieldEntry(NodeFieldEntry entry, BlackboardDefinition runtimeBbDef)
        {
            if (!entry.isVariable || string.IsNullOrEmpty(entry.variableName)) return false;

            // Check the variable's actual stride first — it takes priority over entry.isArray
            IReadOnlyList<BlackboardVariableBase> allVars = runtimeBbDef.GetAllVariables();
            if (allVars != null)
            {
                for (int i = 0; i < allVars.Count; i++)
                {
                    if (allVars[i].Name == entry.variableName)
                        return allVars[i].Stride > 1;
                }
            }

            // Variable not found in definition — fall back to the entry's own flag
            return entry.isArray;
        }

        private static void PackFieldEntryWithArray(
            NodeFieldEntry entry,
            Dictionary<string, int> varIndexByName,
            BlackboardDefinition runtimeBbDef,
            FieldData[] fieldDataArray,
            List<object> boxedConstantsList,
            List<string> fieldTypeNamesList,
            ref int offset)
        {
            if (!entry.isVariable)
            {
                fieldTypeNamesList.Add(entry.fieldTypeName ?? string.Empty);
                fieldDataArray[offset++] = PackFieldEntry(entry, varIndexByName, runtimeBbDef, boxedConstantsList, fieldTypeNamesList);
                return;
            }

            int varIndex = -1;
            if (varIndexByName != null && !string.IsNullOrEmpty(entry.variableName) && varIndexByName.TryGetValue(entry.variableName, out int mapped))
                varIndex = mapped;

            IReadOnlyList<BlackboardVariableBase> allVars = runtimeBbDef.GetAllVariables();

            if (varIndex < 0 || allVars == null || varIndex >= allVars.Count)
            {
#if UNITY_EDITOR
                Debug.LogWarning($"[TreeBaker] Unresolved variable '{entry.variableName}' — not found in definition. varIndex={varIndex}, varCount={(allVars?.Count ?? -1)}");
#endif
                fieldTypeNamesList.Add(entry.fieldTypeName ?? string.Empty);
                fieldDataArray[offset++] = FieldData.FromVariable(-1);
                return;
            }

            int baseSlot = ResolveSlotOffset(varIndex, runtimeBbDef);
            int stride = allVars[varIndex].Stride;

            if (stride > 1)
            {
                fieldTypeNamesList.Add(entry.fieldTypeName ?? string.Empty);
                fieldDataArray[offset++] = FieldData.FromVariable(baseSlot);
                fieldTypeNamesList.Add(entry.fieldTypeName ?? string.Empty);
                fieldDataArray[offset++] = FieldData.FromStride(stride);
            }
            else
            {
                fieldTypeNamesList.Add(entry.fieldTypeName ?? string.Empty);
                fieldDataArray[offset++] = PackFieldEntry(entry, varIndexByName, runtimeBbDef, boxedConstantsList, fieldTypeNamesList);
            }
        }

        private static FieldData PackFieldEntry(NodeFieldEntry entry, Dictionary<string, int> varIndexByName, BlackboardDefinition runtimeBbDef, List<object> boxedConstantsList, List<string> fieldTypeNamesList)
        {
            if (entry.isVariable)
            {
                int varIndex = -1;
                if (varIndexByName != null && !string.IsNullOrEmpty(entry.variableName) && varIndexByName.TryGetValue(entry.variableName, out int mapped))
                {
                    varIndex = mapped;
                }

                IReadOnlyList<BlackboardVariableBase> allVars = runtimeBbDef.GetAllVariables();
                if (varIndex >= 0 && allVars != null && varIndex < allVars.Count)
                {
                    if (!FieldTypeHelper.TryGetSystemTypeFromName(allVars[varIndex].TypeName, out Type bbType) || bbType == null)
                    {
#if UNITY_EDITOR
                        Debug.LogWarning($"[TreeBaker] PackFieldEntry type resolve failed: '{entry.variableName}' bbTypeName='{allVars[varIndex].TypeName}'");
#endif
                        varIndex = -1;
                    }
                    else
                    {
                        Type expectedType = ResolveFieldType(entry);
                        if (expectedType == null)
                        {
#if UNITY_EDITOR
                            Debug.LogWarning($"[TreeBaker] PackFieldEntry — cannot verify type for '{entry.variableName}': fieldTypeName is empty. " +
                                             $"Open the node in the tree editor to re-serialize the field entry.");
#endif
                            varIndex = -1;
                        }
                        else if (!expectedType.IsAssignableFrom(bbType))
                        {
#if UNITY_EDITOR
                            Debug.LogWarning($"[TreeBaker] PackFieldEntry type mismatch: '{entry.variableName}' bbType={bbType.Name} expectedType={expectedType.Name} entryFieldTypeName={entry.fieldTypeName}");
#endif
                            varIndex = -1;
                        }
                    }
                }
                int slotOffset = ResolveSlotOffset(varIndex, runtimeBbDef);
                return FieldData.FromVariable(slotOffset);
            }

            // Constant path
            Type constType = ResolveFieldType(entry);

            // Order constants — resolve name → index at bake time so reordering survives
            if (entry.isOrderConstant && (constType == typeof(int) || constType == typeof(uint)))
            {
                int orderIndex = -1;
                if (!string.IsNullOrEmpty(entry.stringValue))
                {
                    OrderRegistry registry = OrderRegistry.FindInstance();
                    if (registry != null)
                        orderIndex = registry.GetIndex(entry.stringValue);
                }
                if (orderIndex < 0)
                {
                    Debug.LogWarning(
                        $"[TreeBaker] Order '{entry.stringValue}' not found in OrderRegistry. " +
                        $"Defaulting to 0 for field '{entry.fieldName}'.");
                    orderIndex = 0;
                }
                return FieldData.FromConstant(orderIndex);
            }

            if (constType == typeof(int) || constType == typeof(uint))
                return FieldData.FromConstant(entry.intValue);
            if (constType == typeof(float))
                return FieldData.FromConstant(entry.floatValue);
            if (constType == typeof(bool))
                return FieldData.FromConstant(entry.boolValue);
            if (constType != null && constType.IsEnum)
                return FieldData.FromConstant(entry.intValue);

            // Non-primitive types (Vector3, GameObject, etc.) must use boxed constants
            if (constType != null && boxedConstantsList != null)
            {
                object boxedValue = GetConstantValue(entry, constType);
                if (boxedValue != null)
                {
                    int boxedIndex = boxedConstantsList.Count;
                    boxedConstantsList.Add(boxedValue);
                    return FieldData.FromBoxedConstant(boxedIndex);
                }
            }

            // Fallback: type couldn't be resolved. Store as boxed constant if we can
            // infer the value from the entry, otherwise produce a mode-0 zero that
            // DeserializeFields will safely skip (IsPackedConstantType returns false).
            if (boxedConstantsList != null)
            {
                object fallbackValue = GetConstantValue(entry, constType);
                if (fallbackValue != null)
                {
                    int boxedIndex = boxedConstantsList.Count;
                    boxedConstantsList.Add(fallbackValue);
                    return FieldData.FromBoxedConstant(boxedIndex);
                }
            }

            Debug.LogWarning($"[TreeBaker] Unsupported field type '{entry.fieldTypeName}' for STATIC field '{entry.fieldName}'");
            return FieldData.FromConstant(0);
        }

        private static object GetConstantValue(NodeFieldEntry entry, Type type)
        {
            if (type == typeof(int)) return entry.intValue;
            if (type == typeof(float)) return entry.floatValue;
            if (type == typeof(bool)) return entry.boolValue;
            if (type == typeof(Vector2)) return entry.vector2Value;
            if (type == typeof(Vector3)) return entry.vector3Value;
            if (type == typeof(GameObject)) return entry.gameObjectValue;
            if (type == typeof(Transform)) return entry.transformValue;
            return null;
        }
    }
}
