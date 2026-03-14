using System.Collections.Generic;
using UnityEngine;

//This tree runner script will be where the tree gets ran. Execution logic lives elsewhere. Tick/Update calls happen here. 
public class TreeRunner : MonoBehaviour
{

    [SerializeField] private BlackBoard blackBoardTest;

    //Need a SO for the treeAsset with both the root node and flattened tree node array!

    [SerializeField] private BehaviourTreeAsset treeAsset;
    [SerializeField] private NodeData[] nodeDatas;

    private Dictionary<BehaviourNode, int> nodeToIndex;

    private void Start()
    {
        if (treeAsset == null) return;

        treeAsset.Initialize();

        BakeTree(treeAsset.rootCopy);
    }

    private void OnDestroy()
    {
        if (treeAsset == null) return;

        treeAsset.ClearNodes();
    }

    //Tree Runner is driving the tick operation! 

    //Tree Runner Start

    //Tree Runner End

    //Tree flattener start

    //Tree flattening should happen in a separate helper class
    private void BakeTree(BehaviourNode root)
    {
        //Create Contiguous Array
        nodeToIndex = new Dictionary<BehaviourNode, int>();

        int index = 0;
        AssignIndices(root, nodeToIndex, ref index);

        nodeDatas = new NodeData[nodeToIndex.Count];

        FillNodeData(nodeDatas, nodeToIndex);

        //TESTING BYTEBUFFER

        //int i = 0;

        //Init blackBoards
        blackBoardTest.RegisterFields();

        for(int i = 0; i < nodeDatas.Length; i++)
        {
            NodeData nodeData = nodeDatas[i];

            unsafe
            {
                byte* ptr = nodeData.configByteBlob;

                TestParams* values = (TestParams*)ptr;

                float moveSpeed = values->MoveSpeed;
                float fireRange = values->FireRange;

                Debug.Log("Name: " + nodeData.nodeType + "_" + i + "_" + nodeData);
                Debug.Log("FireRange: " + fireRange + " MoveSpeed: " + moveSpeed);
            }

            if (nodeData.nodeType == BehaviourNodeType.ACTION || nodeData.nodeType == BehaviourNodeType.CONDITION)
            {
                BehaviorMethod method = MethodRegistry.GetMethod(nodeData.methodID);
                method.Invoke(blackBoardTest, ref nodeData);
            }

            Debug.Log("----------------------");
        }

        //TESTING END
    }

    private void AssignIndices(BehaviourNode node, Dictionary<BehaviourNode, int> nodeDict, ref int totalIndex) 
    {
        //Debug.Log("Assigning: " + node.name + "Index: " + totalIndex);
        if (nodeDict.ContainsValue(totalIndex)) return; //Disable for flattening debug test
        
        //Add root node
        if(totalIndex == 0) 
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

    private void FillNodeData(NodeData[] nodeDataArray, Dictionary<BehaviourNode, int> nodeIndices) 
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

    private unsafe void GetFixedByteBuffer(NodeConfigID configID, ref NodeData nodeData) 
    {
        byte[] byteArray = NodeConfigRegistry.GetStaticConfig(configID);

        fixed (byte* destination = nodeData.configByteBlob) 
        {
            ByteHelper.ByteArrayToFixedBuffer(byteArray, destination, 32);
        }
    }

    //Tree flattener end
}
