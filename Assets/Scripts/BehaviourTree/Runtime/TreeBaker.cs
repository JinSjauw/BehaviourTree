using BehaviourTree.Core;
using BehaviourTree.Utility;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BehaviourTree.Runtime 
{
    public static class TreeBaker
    {
        public static void BakeTree(BehaviourNode root, BlackboardDefinition bbDef, ref NodeData[] nodeDatas, ref FieldData[] fieldDatas)
        {
            Dictionary<BehaviourNode, int> nodeToIndex = new Dictionary<BehaviourNode, int>();

            if(root.NodeType == BehaviourNodeType.ROOT && root.children.Count > 0)
            {
                root = root.children.First();
            }

            int index = 0;
            AssignIndices(root, nodeToIndex, ref index);

            nodeDatas = new NodeData[nodeToIndex.Count];

            // Count total FieldData entries first
            int totalFieldDataCount = 0;
            foreach (BehaviourNode node in nodeToIndex.Keys)
            {
                if (node is LeafNode action)
                {
                    totalFieldDataCount += action.fieldEntries?.Count ?? 0;
                }
                else if (node is DecoratorNode decorator)
                {
                    totalFieldDataCount += decorator.fieldEntries?.Count ?? 0; 
                }
            }

            fieldDatas = new FieldData[totalFieldDataCount];

            FillNodeData(nodeDatas, fieldDatas, nodeToIndex, bbDef);
        }

        private static void AssignIndices(BehaviourNode node, Dictionary<BehaviourNode, int> nodeDict, ref int totalIndex)
        {
            if (nodeDict.ContainsValue(totalIndex)) return;
            if (totalIndex == 0)
            {
                nodeDict[node] = totalIndex;
                node.runtimeIndex = totalIndex;
                totalIndex++;
            }

            for (int i = 0; i < node.children.Count; i++)
            {
                BehaviourNode child = node.children[i];
                nodeDict[child] = totalIndex;
                child.runtimeIndex = totalIndex;

                if (i == 0) node.firstChildIndex = totalIndex;
                if (i == node.children.Count - 1) node.lastChildIndex = totalIndex;

                totalIndex++;
            }

            for (int i = 0; i < node.children.Count; i++)
            {
                BehaviourNode child = node.children[i];
                AssignIndices(child, nodeDict, ref totalIndex);
            }
        }

        private static void FillNodeData(NodeData[] nodeDataArray, FieldData[] fieldDataArray, Dictionary<BehaviourNode, int> nodeIndices, BlackboardDefinition bbDef)
        {
            int currentFieldDataOffset = 0;
            foreach (BehaviourNode node in nodeIndices.Keys)
            {
                NodeData nodeData = new NodeData();

                nodeData.nodeType = node.NodeType;
                nodeData.firstChildIndex = -1;
                nodeData.lastChildIndex = -1;
                nodeData.methodID = MethodID.NONE;
                nodeData.blackBoardTypeID = BlackBoardType.SELF;
                nodeData.fieldDataStartIndex = -1;
                nodeData.fieldDataCount = 0;

                switch (node.NodeType)
                {
                    case BehaviourNodeType.ROOT:
                    case BehaviourNodeType.SEQUENCE:
                    case BehaviourNodeType.SELECTOR:
                        nodeData.firstChildIndex = node.firstChildIndex;
                        nodeData.lastChildIndex = node.lastChildIndex;
                        break;

                    case BehaviourNodeType.CONDITION:
                    case BehaviourNodeType.ACTION:
                        {
                            LeafNode action = (LeafNode)node;
                            nodeData.methodID = action.methodID;
                            nodeData.blackBoardTypeID = action.BlackBoardTypeID;
                            nodeData.fieldDataStartIndex = currentFieldDataOffset;
                            nodeData.fieldDataCount = action.fieldEntries?.Count ?? 0;

                            if (action.fieldEntries != null)
                            {
                                foreach (var entry in action.fieldEntries)
                                {
                                    FieldData fd = PackFieldEntry(entry, bbDef);
                                    fieldDataArray[currentFieldDataOffset++] = fd;
                                }
                            }
                        }
                        break;
                    case BehaviourNodeType.DECORATOR:
                        {
                            DecoratorNode decorator = (DecoratorNode)node;
                            nodeData.methodID = decorator.methodID;
                            nodeData.blackBoardTypeID = decorator.BlackBoardTypeID;
                            nodeData.firstChildIndex = node.firstChildIndex;   // single child
                            nodeData.lastChildIndex  = node.firstChildIndex;   // same as first
                            nodeData.fieldDataStartIndex = currentFieldDataOffset;
                            nodeData.fieldDataCount = decorator.fieldEntries?.Count ?? 0;

                            if (decorator.fieldEntries != null)
                            {
                                foreach (var entry in decorator.fieldEntries)
                                {
                                    fieldDataArray[currentFieldDataOffset++] = PackFieldEntry(entry, bbDef);
                                }
                            }
                        }
                    break;
                }

                nodeDataArray[nodeIndices[node]] = nodeData;
            }
        }

        private static FieldData PackFieldEntry(NodeFieldEntry entry, BlackboardDefinition bbDef)
        {
            if (entry.isVariable)
            {
                int varIndex = -1;
                if (bbDef != null && bbDef.sharedVariables != null)
                {
                    varIndex = bbDef.sharedVariables.FindIndex(v => v.name == entry.variableName);
                    if (varIndex < 0)
                    {
                        UnityEngine.Debug.LogWarning(
                            $"[TreeBaker] Blackboard variable '{entry.variableName}' not found in definition. " +
                            $"Field '{entry.fieldName}' will use invalid index -1.");
                    }
                    else
                    {
                        Type bbType = FieldTypeHelper.GetSystemTypeFromName(bbDef.sharedVariables[varIndex].typeName);
                        FieldType bbFieldType = FieldTypeHelper.GetFieldType(bbType);
                        if (bbFieldType != entry.fieldType)
                        {
                            UnityEngine.Debug.LogWarning(
                                $"[TreeBaker] Blackboard variable '{entry.variableName}' type mismatch. " +
                                $"Field '{entry.fieldName}' expects {entry.fieldType} but blackboard has {bbFieldType}. " +
                                $"Using invalid index -1.");
                            varIndex = -1;
                        }
                    }
                }
                return FieldData.FromVariable(varIndex);
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
                    UnityEngine.Debug.LogWarning($"[TreeBaker] Unsupported field type for '{entry.fieldType}' for STATIC field '{entry.fieldName}'");
                    return FieldData.FromConstant(0);
            }
        }
    }
}

