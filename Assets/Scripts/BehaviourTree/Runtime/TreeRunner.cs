using System.Collections.Generic;
using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Runtime 
{
   [RequireComponent(typeof(BlackBoard))]
   public class TreeRunner : MonoBehaviour
   {
      [SerializeField] private BlackBoard blackBoard;
      [SerializeField] private RuntimeBehaviourTreeAsset runtimeAsset;
      private bool runIndependently = true;
      public bool RunIndependently
      {
          get => runIndependently;
          set => runIndependently = value;
      }
#if UNITY_EDITOR
      [SerializeField] private BehaviourTreeAssetBase authoringAsset;
#endif

      private TreeEvaluator evaluator;
      private RuntimeDebugProvider debugProvider;
      private List<IBlackboardDataProvider> dataProviders;
      private bool Initialized = false;

      private void Start()
      {
         if(!runIndependently) return; 
         
         Initialize();
      }

      private void Update()
      {
         if (!runIndependently) return;

         PushDataProviders();
         Evaluate();
      }

      /// <summary>
      /// Evaluates the tree once. In standalone mode, called from Update()
      /// after PushDataProviders(). In commander mode, called externally
      /// by the CommanderBindingBridge after it has pushed providers
      /// and copied commander data into self BB.
      /// </summary>
      public void Evaluate()
      {
         if (evaluator == null || blackBoard == null) return;

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

      private void PushDataProviders()
      {
         if (dataProviders == null) return;

         for (int i = 0; i < dataProviders.Count; i++)
         {
            dataProviders[i].ProvideData(blackBoard);
         }
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
#endif

      public void Initialize()
      {
         if (Initialized) return;

         runtimeAsset = RuntimeAssetHelper.GetOrBake(runtimeAsset,
#if UNITY_EDITOR
            authoringAsset
#else
            null
#endif
         );
         if (runtimeAsset == null) return;

         if(blackBoard == null)
         {
            Debug.LogError("BlackBoard is null");
            return;
         };

         blackBoard.Initialize(runtimeAsset.blackboardDefinition);
         evaluator = new TreeEvaluator(runtimeAsset.runtimeNodeData, runtimeAsset.runtimeFieldData, runtimeAsset.boxedConstants, runtimeAsset.maxTreeDepth);

         // Ensure debug provider exists
         debugProvider = GetComponent<RuntimeDebugProvider>();
         if (debugProvider == null) debugProvider = gameObject.AddComponent<RuntimeDebugProvider>();

         // Collect all data providers on this GameObject
         IBlackboardDataProvider[] providers = GetComponentsInChildren<IBlackboardDataProvider>();
         dataProviders = new List<IBlackboardDataProvider>(providers);
      }

      public Object GetSourceTree()
      {
         if (runtimeAsset != null && runtimeAsset.sourceTree != null) return runtimeAsset.sourceTree;
#if UNITY_EDITOR
         return authoringAsset;
#else
         return null;
#endif
      }
   }
}


