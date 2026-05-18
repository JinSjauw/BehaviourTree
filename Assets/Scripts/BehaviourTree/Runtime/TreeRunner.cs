using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Runtime 
{
   [RequireComponent(typeof(BlackBoard))]
   public class TreeRunner : MonoBehaviour
   {
      [SerializeField] private BlackBoard blackBoard;
      [SerializeField] private RuntimeBehaviourTreeAsset runtimeAsset;
#if UNITY_EDITOR
      [SerializeField] private UnityEngine.Object authoringAsset;
#endif

      private TreeEvaluator evaluator;
      private RuntimeDebugProvider debugProvider;


      private void Start()
      {
         if (runtimeAsset == null)
         {
#if UNITY_EDITOR
            BehaviourTree.Core.IBehaviourTreeAuthoringAsset authoring = authoringAsset as BehaviourTree.Core.IBehaviourTreeAuthoringAsset;
            if (authoring != null)
            {
               RuntimeBehaviourTreeAsset tempRuntimeAsset = ScriptableObject.CreateInstance<RuntimeBehaviourTreeAsset>();
               tempRuntimeAsset.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
               tempRuntimeAsset.name = authoring.DisplayName + "_Runtime";
               tempRuntimeAsset.blackboardDefinition = authoring.BlackboardDefinition;
               tempRuntimeAsset.sourceTree = authoringAsset;

               TreeBaker.BakeTree(authoring.Root, authoring.BlackboardDefinition, ref tempRuntimeAsset.runtimeNodeData, ref tempRuntimeAsset.runtimeFieldData);
               runtimeAsset = tempRuntimeAsset;
            }
            else
            {
               Debug.LogError("RuntimeBehaviourTreeAsset is null");
               return;
            }
#else
            Debug.LogError("RuntimeBehaviourTreeAsset is null");
            return;
#endif
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

#if UNITY_EDITOR
      private void OnValidate()
      {
         if(runtimeAsset != null)
         {
            blackBoard?.BuildSerializedReferences(runtimeAsset.blackboardDefinition);
         }
         else
         {
            IBehaviourTreeAuthoringAsset authoring = authoringAsset as IBehaviourTreeAuthoringAsset;
            if (authoring != null)
            {
               blackBoard?.BuildSerializedReferences(authoring.BlackboardDefinition);
            }
         }

         if(runtimeAsset == null && authoringAsset == null)
         {
            blackBoard?.ClearSerializedReferences();
         }
      }

      public UnityEngine.Object GetSourceTree()
      {
         if (runtimeAsset != null && runtimeAsset.sourceTree != null) return runtimeAsset.sourceTree;
         return authoringAsset;
      }
#endif
   }
}


