using BehaviourTree.Core;
using BehaviourTree.Utility;
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
                if (node is ActionNode action)
                    totalFieldDataCount += action.fieldEntries?.Count ?? 0;
                else if (node is ConditionNode cond)
                    totalFieldDataCount += cond.fieldEntries?.Count ?? 0;
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
                totalIndex++;
            }

            for (int i = 0; i < node.children.Count; i++)
            {
                BehaviourNode child = node.children[i];
                nodeDict[child] = totalIndex;

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
                        {
                            ConditionNode cond = (ConditionNode)node;
                            nodeData.methodID = cond.methodID;
                            nodeData.blackBoardTypeID = cond.BlackBoardTypeID;
                            nodeData.fieldDataStartIndex = currentFieldDataOffset;
                            nodeData.fieldDataCount = cond.fieldEntries?.Count ?? 0;

                            if (cond.fieldEntries != null)
                            {
                                foreach (var entry in cond.fieldEntries)
                                {
                                    FieldData fd = PackFieldEntry(entry, bbDef);
                                    fieldDataArray[currentFieldDataOffset++] = fd;
                                }
                            }
                        }
                        break;

                    case BehaviourNodeType.ACTION:
                        {
                            ActionNode action = (ActionNode)node;
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
                }

                nodeDataArray[nodeIndices[node]] = nodeData;
            }
        }

        private static unsafe FieldData PackFieldEntry(NodeFieldEntry entry, BlackboardDefinition bbDef)
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
                case FieldType.Vector2:
                    // Pack Vector2 as two consecutive FieldData slots handled in evaluator
                    return FieldData.FromConstant(0);
                case FieldType.Vector3:
                    // Pack Vector3 as three consecutive FieldData slots handled in evaluator
                    return FieldData.FromConstant(0);
                default:
                    return FieldData.FromConstant(0);
            }
        }
    }
}

