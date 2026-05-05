using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Runtime 
{
    //This tree runner script will be where the tree gets ran. Execution logic lives elsewhere. Tick/Update calls happen here. 
    public class TreeRunner : MonoBehaviour
    {

        [SerializeField] private BlackBoard blackBoardTest;

        //Need a SO for the treeAsset with both the root node and flattened tree node array!

        [SerializeField] private RuntimeBTreeAsset treeAsset;
        [SerializeField] private NodeData[] nodeDatas;

        private TreeEvaluator evaluator;

        private void Start()
        {
           if (treeAsset == null) return;

           blackBoardTest.Initialize(treeAsset.blackboardDefinition);

           evaluator = new TreeEvaluator(treeAsset.runtimeNodeData, treeAsset.runtimeFieldData);
        }

        private void Update()
        {
           //evaluator.Tick()
           evaluator.Evaluate(blackBoardTest);
        }

        private void OnDestroy()
        {
           if (treeAsset == null) return;

           //treeAsset.ClearNodes();
        }
    }
}

