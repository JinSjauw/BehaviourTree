using System.Collections.Generic;
using UnityEngine;

public class TreeRunner : MonoBehaviour
{
    //Need a SO for the treeAsset with both the root node and flattened tree node array!

    [SerializeField] private BehaviourTreeAsset treeAsset;
    [SerializeField] private List<NodeData> nodeDatas = new List<NodeData>();

    //TEST
    [SerializeField] private List<int> nodeIndices = new List<int>();
    [SerializeField] private BehaviourNodeType[] nodeTypesTest;

    private Dictionary<BehaviourNode, int> nodeToIndex;

    private void Start()
    {
        if (treeAsset == null) return;

        //TEST
        nodeTypesTest = new BehaviourNodeType[10];
        //

        FlattenTree(treeAsset.root);
    }

    private void FlattenTree(BehaviourNode root) 
    {
        //Create Contiguos Array
        nodeToIndex = new Dictionary<BehaviourNode, int>();

        int index = 0;
        AssignIndices(root, nodeToIndex, ref index);

        nodeDatas.Capacity = nodeToIndex.Count;

        NodeData[] flattenedArray = new NodeData[nodeToIndex.Count];
    }

    private void AssignIndices(BehaviourNode node, Dictionary<BehaviourNode, int> nodeDict, ref int totalIndex) 
    {
        if (nodeDict.ContainsKey(node)) return; //Disable for flattening debug test
        nodeDict[node] = totalIndex;
        
        //Test
        nodeTypesTest[totalIndex] = node.NodeType;
        
        if(node.NodeType == BehaviourNodeType.ACTION) 
        {
            BehaviorMethod method = MethodRegistry.GetMethod(MethodID.HELLOWORLD);
            method.Invoke(ParamSetID.NONE, null);
        }
        else if(node.NodeType == BehaviourNodeType.CONDITION) 
        {
            BehaviorMethod method = MethodRegistry.GetMethod(MethodID.BYEWORLD);
            method.Invoke(ParamSetID.NONE, null);
        }

        //Test End


        totalIndex++;

        for (int i = 0; i < node.children.Count; i++) 
        {
            BehaviourNode child = node.children[i];
            AssignIndices(child, nodeDict, ref totalIndex);
        }
    }

    private void FillNodeData() 
    {
        //Sort through node types and populate struct with relevant per nodeType data
    }
}
