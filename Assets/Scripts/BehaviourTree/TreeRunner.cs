using System.Collections.Generic;
using UnityEngine;

public class TreeRunner : MonoBehaviour
{
    //Need a SO for the treeAsset with both the root node and flattened tree node array!

    [SerializeField] private BehaviourTreeAsset treeAsset;
    [SerializeField] private NodeData[] nodeDatas;
    [SerializeField] private BehaviourNodeType[] nodeTypesTest;

    private Dictionary<BehaviourNode, int> nodeToIndex;

    private void Start()
    {
        if (treeAsset == null) return;

        treeAsset.Initialize();

        nodeTypesTest = new BehaviourNodeType[20];

        FlattenTree(treeAsset.rootCopy);
    }

    private void OnDestroy()
    {
        if (treeAsset == null) return;

        treeAsset.ClearNodes();
    }

    private void FlattenTree(BehaviourNode root) 
    {
        //Create Contiguos Array
        nodeToIndex = new Dictionary<BehaviourNode, int>();

        int index = 0;
        AssignIndices(root, nodeToIndex, ref index);

        nodeDatas = new NodeData[nodeToIndex.Count];
        Debug.Log(index + " " + nodeToIndex.Count);
    }

    private void AssignIndices(BehaviourNode node, Dictionary<BehaviourNode, int> nodeDict, ref int totalIndex) 
    {
        Debug.Log("Assigning: " + node.name + "Index: " + totalIndex);
        if (nodeDict.ContainsValue(totalIndex)) return; //Disable for flattening debug test

        //Test

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

        //if(node.children.Count <= 0) 
        //{
        //    nodeDict[node] = totalIndex;
        //    totalIndex++;

        //    return;
        //}

        for (int i = 0; i < node.children.Count; i++)
        {
            BehaviourNode child = node.children[i];
            nodeDict[child] = totalIndex++;

            nodeTypesTest[totalIndex] = child.NodeType;
            Debug.Log("Assigning Child: " + child.name + "Index: " + totalIndex);

            if (i == 0) node.firstChildIndex = totalIndex;
            if (i == node.children.Count - 1) node.lastChildIndex = totalIndex;
        }

        for (int i = 0; i < node.children.Count; i++)
        {
            BehaviourNode child = node.children[i];
            AssignIndices(child, nodeDict, ref totalIndex);
        }
    }

    private void FillNodeData(NodeData[] arrayToFill, Dictionary<BehaviourNode, int> nodeDataIndex) 
    {
        //Sort through node types and populate struct with relevant per nodeType data

        foreach (BehaviourNode node in nodeDataIndex.Keys) 
        {
            NodeData nodeData;
            
        }
        
    }
}
