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
      [SerializeField] private BehaviourTreeAssetBase authoringAsset;
#endif

      private TreeEvaluator evaluator;
      private RuntimeDebugProvider debugProvider;
      private bool Initialized = false;

      private void Start()
      {
         Initialize();
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
            debugProvider.currentNodeGuids = runtimeAsset != null ? runtimeAsset.runtimeNodeGuids : null;
         }

         Initialized = true;
      }

      private void OnDestroy()
      {
         if (runtimeAsset == null) return;
         Destroy(runtimeAsset);
         runtimeAsset = null;
      }

      private void OnDisable()
      {
         Initialized = false;
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
            BehaviourTreeAssetBase authoring = authoringAsset as BehaviourTreeAssetBase;
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

      public void Initialize()
      {
         if (Initialized) return;
         if (runtimeAsset == null)
         {
#if UNITY_EDITOR
            BehaviourTreeAssetBase authoring = authoringAsset as BehaviourTreeAssetBase;
            if (authoring != null)
            {
               RuntimeBehaviourTreeAsset tempRuntimeAsset = ScriptableObject.CreateInstance<RuntimeBehaviourTreeAsset>();
               tempRuntimeAsset.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
               tempRuntimeAsset.name = authoring.DisplayName + "_Runtime";
               tempRuntimeAsset.sourceTree = authoringAsset;

               tempRuntimeAsset.blackboardDefinition = TreeBaker.BakeTree(authoring.Root, authoring.BlackboardDefinition, ref tempRuntimeAsset.runtimeNodeData, ref tempRuntimeAsset.runtimeFieldData, ref tempRuntimeAsset.runtimeNodeGuids, out tempRuntimeAsset.maxTreeDepth);
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
         else
         {
            runtimeAsset = Instantiate(runtimeAsset);
            runtimeAsset.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
         }

         if(blackBoard == null)
         {
            Debug.LogError("BlackBoard is null");
            return;
         };

         blackBoard.Initialize(runtimeAsset.blackboardDefinition);
         evaluator = new TreeEvaluator(runtimeAsset.runtimeNodeData, runtimeAsset.runtimeFieldData, runtimeAsset.maxTreeDepth);

         // Ensure debug provider exists
         debugProvider = GetComponent<RuntimeDebugProvider>();
         if (debugProvider == null) debugProvider = gameObject.AddComponent<RuntimeDebugProvider>();
      }

      public UnityEngine.Object GetSourceTree()
      {
         if (runtimeAsset != null && runtimeAsset.sourceTree != null) return runtimeAsset.sourceTree;
         return authoringAsset;
      }
#endif
   }
}


