using BehaviourTree.Core;
using BehaviourTree.Editor;
using UnityEngine;

namespace BehaviourTree.Runtime 
{
   [RequireComponent(typeof(BlackBoard))]
   public class TreeRunner : MonoBehaviour
   {
      [SerializeField] private BlackBoard blackBoard;
      [SerializeField] private RuntimeBTreeAsset runtimeAsset;
      [SerializeField] private BehaviourTreeAsset authoringAsset;

      private TreeEvaluator evaluator;
      private RuntimeDebugProvider debugProvider;


      private void Start()
      {
         //Check for runtime asset
         if (runtimeAsset == null)
         {
            //Check for authoring time asset
            if(authoringAsset == null) return;

            //bake temp runtime asset
            RuntimeBTreeAsset tempRuntimeAsset = ScriptableObject.CreateInstance<RuntimeBTreeAsset>();
            tempRuntimeAsset.name = authoringAsset.name + "_Runtime";
            tempRuntimeAsset.blackboardDefinition = authoringAsset.blackboardDefinition;
            tempRuntimeAsset.sourceTree = authoringAsset;

            TreeBaker.BakeTree(authoringAsset.rootCopy, authoringAsset.blackboardDefinition, ref tempRuntimeAsset.runtimeNodeData, ref tempRuntimeAsset.runtimeFieldData);

            runtimeAsset = tempRuntimeAsset;
         }

         if(blackBoard == null)
         {
            Debug.LogError("BlackBoard is null");
            return;
         };

         blackBoard.Initialize(runtimeAsset.blackboardDefinition);
         evaluator = new TreeEvaluator(runtimeAsset.runtimeNodeData, runtimeAsset.runtimeFieldData);

         if(evaluator == null)
         {
            Debug.LogError("TreeEvaluator is null");
            return;
         };

         // Ensure debug provider exists
         debugProvider = GetComponent<RuntimeDebugProvider>();
         if (debugProvider == null)
            debugProvider = gameObject.AddComponent<RuntimeDebugProvider>();
      }

      private void Update()
      {
         if(evaluator == null || blackBoard == null) return;
         evaluator.Evaluate(blackBoard);  

         // Expose to editor
         if (debugProvider != null)
         {
            debugProvider.currentNodeStates = evaluator.nodeStates;
            debugProvider.activeNodeIndex = evaluator.currentNodeIndex;
         }
      }

      private void OnDestroy()
      {
         if (runtimeAsset == null) return;
      }

      private void OnValidate()
      {
         if(runtimeAsset != null)
         {
            blackBoard?.BuildSerializedReferences(runtimeAsset.blackboardDefinition);
         }
         else if(authoringAsset != null)
         {
            blackBoard?.BuildSerializedReferences(authoringAsset.blackboardDefinition);
         }

         if(runtimeAsset == null && authoringAsset == null)
         {
            blackBoard?.ClearSerializedReferences();
         }
      }

      public BehaviourTreeAsset GetSourceTree()
      {
         BehaviourTreeAsset sourceTree = null;

         if(runtimeAsset != null)
         {
            sourceTree = runtimeAsset.sourceTree;
         }
         else if(authoringAsset != null)
         {
            sourceTree = authoringAsset;
         }

         return sourceTree;
      }
   }
}


