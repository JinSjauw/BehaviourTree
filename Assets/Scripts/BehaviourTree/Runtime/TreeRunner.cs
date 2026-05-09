using BehaviourTree.Core;
using BehaviourTree.Editor;
using UnityEngine;

namespace BehaviourTree.Runtime 
{
   public class TreeRunner : MonoBehaviour
   {
      [SerializeField] private BlackBoard blackBoardTest;
      [SerializeField] private RuntimeBTreeAsset treeAsset;
      [SerializeField] private NodeData[] nodeDatas;

      private TreeEvaluator evaluator;
      private RuntimeDebugProvider debugProvider;


      private void Start()
      {
         if (treeAsset == null) return;

         blackBoardTest.Initialize(treeAsset.blackboardDefinition);

         evaluator = new TreeEvaluator(treeAsset.runtimeNodeData, treeAsset.runtimeFieldData);

         // Ensure debug provider exists
         debugProvider = GetComponent<RuntimeDebugProvider>();
         if (debugProvider == null)
            debugProvider = gameObject.AddComponent<RuntimeDebugProvider>();
      }

      private void Update()
      {
         evaluator.Evaluate(blackBoardTest);

         // Expose to editor
         if (debugProvider != null)
         {
            debugProvider.currentNodeStates = evaluator.nodeStates;
            debugProvider.activeNodeIndex = evaluator.currentNodeIndex;
         }
      }

      private void OnDestroy()
      {
         if (treeAsset == null) return;
      }

      public BehaviourTreeAsset GetSourceTree()
      {
         return treeAsset.sourceTree;
      }
   }
}


