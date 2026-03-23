using BTreeEditor.Utility;
using System.Collections.Generic;

namespace BTreeEditor 
{
    public static class TreeBaker
    {
        public static void BakeTree(BehaviourNode root, ref NodeData[] nodeDatas)
        {
            //Create Contiguous Array
            Dictionary<BehaviourNode, int> nodeToIndex = new Dictionary<BehaviourNode, int>();

            int index = 0;
            AssignIndices(root, nodeToIndex, ref index);

            nodeDatas = new NodeData[nodeToIndex.Count];

            FillNodeData(nodeDatas, nodeToIndex);
        }

        private static void AssignIndices(BehaviourNode node, Dictionary<BehaviourNode, int> nodeDict, ref int totalIndex)
        {
            //Debug.Log("Assigning: " + node.name + "Index: " + totalIndex);
            if (nodeDict.ContainsValue(totalIndex)) return; //Disable for flattening debug test

            //Add root node
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

        private static void FillNodeData(NodeData[] nodeDataArray, Dictionary<BehaviourNode, int> nodeIndices)
        {
            //Sort through node types and populate struct with relevant per nodeType data

            //Populate NodeData structs
            foreach (BehaviourNode node in nodeIndices.Keys)
            {
                NodeData nodeData = new NodeData();

                //Debug.Log(node.NodeType + " " + nodeIndices[node]);

                //Default initialization
                nodeData.nodeType = node.NodeType;
                nodeData.firstChildIndex = -1;
                nodeData.lastChildIndex = -1;
                nodeData.methodID = MethodID.NONE;
                nodeData.blackBoardTypeID = BlackBoardType.SELF;

                //Populate nodeData differently based on nodeType
                switch (node.NodeType)
                {
                    case BehaviourNodeType.ROOT:
                    case BehaviourNodeType.SEQUENCE:
                    case BehaviourNodeType.SELECTOR:

                        nodeData.firstChildIndex = node.firstChildIndex;
                        nodeData.lastChildIndex = node.lastChildIndex;

                        break;

                    case BehaviourNodeType.CONDITION:

                        ConditionNode conditionNode = (ConditionNode)node;

                        nodeData.methodID = conditionNode.methodID;
                        nodeData.blackBoardTypeID = conditionNode.BlackBoardTypeID;

                        GetFixedByteBuffer(conditionNode.treeConfigID, ref nodeData);

                        break;

                    case BehaviourNodeType.ACTION:

                        ActionNode actionNode = (ActionNode)node;

                        nodeData.methodID = actionNode.methodID;
                        nodeData.blackBoardTypeID = actionNode.BlackBoardTypeID;

                        GetFixedByteBuffer(actionNode.treeConfigID, ref nodeData);

                        break;
                }

                nodeDataArray[nodeIndices[node]] = nodeData;
            }
        }

        private static unsafe void GetFixedByteBuffer(NodeConfigID configID, ref NodeData nodeData)
        {
            byte[] byteArray = NodeConfigRegistry.GetStaticConfig(configID);

            fixed (byte* destination = nodeData.configByteBlob)
            {
                ByteHelper.ByteArrayToFixedBuffer(byteArray, destination, 32);
            }
        }
    }
}
