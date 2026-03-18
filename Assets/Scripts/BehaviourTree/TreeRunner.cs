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

        blackBoardTest.RegisterFields();

        TreeBaker.BakeTree(treeAsset.rootCopy, ref nodeDatas);

        for (int i = 0; i < nodeDatas.Length; i++)
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
    }

    private void OnDestroy()
    {
        if (treeAsset == null) return;

        treeAsset.ClearNodes();
    }
}
