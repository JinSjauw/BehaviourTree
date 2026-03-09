using System.Collections.Generic;
using UnityEngine;

//This tree runner script will be where the tree gets ran. Execution logic lives elsewhere. Tick/Update calls happen here. 
public class TreeRunner : MonoBehaviour
{
    //Need a SO for the treeAsset with both the root node and flattened tree node array!

    [SerializeField] private BehaviourTreeAsset treeAsset;
    [SerializeField] private NodeData[] nodeDatas;

    private Dictionary<BehaviourNode, int> nodeToIndex;

    private void Start()
    {
        if (treeAsset == null) return;

        treeAsset.Initialize();

        FlattenTree(treeAsset.rootCopy);
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
    private void FlattenTree(BehaviourNode root) 
    {
        //Create Contiguos Array
        nodeToIndex = new Dictionary<BehaviourNode, int>();

        int index = 0;
        AssignIndices(root, nodeToIndex, ref index);

        nodeDatas = new NodeData[nodeToIndex.Count];
        Debug.Log(index + " " + nodeToIndex.Count);

        FillNodeData(nodeDatas, nodeToIndex);
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

        //Test : this should be in the evaluator class

        //if(node.NodeType == BehaviourNodeType.ACTION) 
        //{
        //    BehaviorMethod method = MethodRegistry.GetMethod(MethodID.HELLOWORLD);
        //    method.Invoke(ParamSetID.NONE, null);
        //}
        //else if(node.NodeType == BehaviourNodeType.CONDITION) 
        //{
        //    BehaviorMethod method = MethodRegistry.GetMethod(MethodID.BYEWORLD);
        //    method.Invoke(ParamSetID.NONE, null);
        //}

        //Test End

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
            nodeData.paramSetID = ParamSetID.NONE;
            nodeData.blackBoardTypeID = BlackBoardType.SELF;
            nodeData.staticConfigSetID = StaticConfigSetID.NONE;
            nodeData.treeConfigID = TreeConfigID.NONE;

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
                    nodeData.paramSetID = conditionNode.paramSetID;
                    nodeData.blackBoardTypeID = conditionNode.BlackBoardTypeID;
                    nodeData.staticConfigSetID = conditionNode.configParamSetID;
                    nodeData.treeConfigID = conditionNode.treeConfigID;

                    break;

                case BehaviourNodeType.ACTION:

                    ActionNode actionNode = (ActionNode)node;

                    nodeData.methodID = actionNode.methodID;
                    nodeData.paramSetID = actionNode.paramSetID;
                    nodeData.blackBoardTypeID = actionNode.BlackBoardTypeID;
                    nodeData.staticConfigSetID = actionNode.configParamSetID;
                    nodeData.treeConfigID = actionNode.treeConfigID;

                    //Retrieve data for static byteBlob;
                    //Retrieve correct treeConfig 
                    //Retrieve correct blob with ConfigSetID

                    break;
            }

            nodeDataArray[nodeIndices[node]] = nodeData;
        }
    }
    //Tree flattener end
}
