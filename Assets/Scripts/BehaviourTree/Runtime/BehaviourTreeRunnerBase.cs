using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Runtime
{
    /// <summary>
    /// Shared base class for AgentTreeRunner and CommanderTreeRunner.
    /// Handles baking, BB initialization, evaluator creation, and debug provider setup.
    /// </summary>
    [RequireComponent(typeof(BlackBoard))]
    public abstract class BehaviourTreeRunnerBase : MonoBehaviour
    {
        [SerializeField] protected BlackBoard blackBoard;

        /// <summary>Public accessor for the blackboard. Used by squad copy helpers.</summary>
        public BlackBoard BlackBoard => blackBoard;
        [SerializeField] protected RuntimeBehaviourTreeAsset runtimeAsset;
#if UNITY_EDITOR
        [SerializeField] protected BehaviourTreeAssetBase authoringAsset;
#endif

        protected TreeEvaluator evaluator;
        protected RuntimeDebugProvider debugProvider;
        protected bool initialized;

        /// <summary>
        /// Template Method: bakes/copies the runtime asset, initializes the BB,
        /// creates the evaluator and debug provider, then calls OnPostInitialize().
        /// </summary>
        public virtual void Initialize()
        {
            if (initialized) return;

            runtimeAsset = RuntimeAssetHelper.GetOrBake(runtimeAsset,
#if UNITY_EDITOR
                authoringAsset,
#else
                null,
#endif
                GetType().Name);
            if (runtimeAsset == null) return;

            if (blackBoard == null)
            {
                Debug.LogError($"[{GetType().Name}] BlackBoard is null.");
                return;
            }

            blackBoard.Initialize(runtimeAsset.blackboardDefinition);
            evaluator = new TreeEvaluator(runtimeAsset.runtimeNodeData, runtimeAsset.runtimeFieldData, runtimeAsset.boxedConstants, runtimeAsset.maxTreeDepth);

            debugProvider = GetComponent<RuntimeDebugProvider>();
            if (debugProvider == null) debugProvider = gameObject.AddComponent<RuntimeDebugProvider>();

            OnPostInitialize();
            initialized = true;
        }

        /// <summary>Subclass hook called after base initialization is complete.</summary>
        protected virtual void OnPostInitialize() { }

        /// <summary>Evaluates the tree once against the blackboard.</summary>
        public void Evaluate()
        {
            if (evaluator == null || blackBoard == null) return;

            evaluator.Evaluate(blackBoard);

            if (debugProvider != null)
            {
                debugProvider.currentNodeStates = evaluator.nodeStates;
                debugProvider.activeNodeIndex = evaluator.currentNodeIndex;
                debugProvider.currentNodeGuids = runtimeAsset != null ? runtimeAsset.runtimeNodeGuids : null;
            }
        }

        protected virtual void OnDestroy()
        {
            if (runtimeAsset == null) return;
            Destroy(runtimeAsset);
            runtimeAsset = null;
        }

        protected virtual void OnDisable()
        {
            initialized = false;
        }

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            if (runtimeAsset != null)
            {
                blackBoard?.BuildSerializedReferences(runtimeAsset.blackboardDefinition);
            }
            else
            {
                BehaviourTreeAssetBase authoring = authoringAsset;
                if (authoring != null)
                {
                    blackBoard?.BuildSerializedReferences(authoring.BlackboardDefinition);
                }
            }

            if (runtimeAsset == null && authoringAsset == null)
            {
                blackBoard?.ClearSerializedReferences();
            }
        }

        public Object GetSourceTree()
        {
            if (runtimeAsset != null && runtimeAsset.sourceTree != null) return runtimeAsset.sourceTree;
            return authoringAsset ?? null;
        }
#endif
    }
}
