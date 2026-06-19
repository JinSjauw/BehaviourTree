using System.Collections.Generic;
using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Runtime
{
    /// <summary>
    /// Orchestrates a group of agents under a commander behaviour tree.
    /// Each frame: ticks all agents (bridge push → copy commander → evaluate → copy agent),
    /// then evaluates the commander tree against the shared commander BB.
    /// </summary>
    [RequireComponent(typeof(BlackBoard))]
    public class CommanderTreeRunner : MonoBehaviour
    {
        [SerializeField] private BlackBoard commanderBB;
        [SerializeField] private RuntimeBehaviourTreeAsset runtimeAsset;
        [SerializeField] private List<TreeRunner> registeredAgents = new List<TreeRunner>();
#if UNITY_EDITOR
        [SerializeField] private BehaviourTreeAssetBase authoringAsset;
#endif

        private TreeEvaluator evaluator;
        private RuntimeDebugProvider debugProvider;
        private CommanderBindingBridge[] cachedBridges;
        private bool initialized = false;

        private void Start()
        {
            Initialize();
        }

        private void Update()
        {
            if (evaluator == null || commanderBB == null) return;

            TickAgents();
            EvaluateCommander();
        }

        /// <summary>
        /// Ticks each registered agent: push data providers, copy commander data in,
        /// evaluate agent tree, copy agent data out to commander BB.
        /// </summary>
        private void TickAgents()
        {
            if (cachedBridges == null) return;

            for (int i = 0; i < registeredAgents.Count; i++)
            {
                TreeRunner agent = registeredAgents[i];
                CommanderBindingBridge bridge = cachedBridges[i];
                if (agent == null) continue;

                if (bridge != null)
                {
                    bridge.PushDataProviders();
                    bridge.CopyCommanderToAgent();
                    agent.Evaluate();
                    bridge.CopyAgentToCommander();
                }
                else
                {
                    agent.Evaluate();
                }
            }
        }

        /// <summary>
        /// Evaluates the commander tree against the commander BB.
        /// </summary>
        private void EvaluateCommander()
        {
            evaluator.Evaluate(commanderBB);

            if (debugProvider != null)
            {
                debugProvider.currentNodeStates = evaluator.nodeStates;
                debugProvider.activeNodeIndex = evaluator.currentNodeIndex;
                debugProvider.currentNodeGuids = runtimeAsset != null ? runtimeAsset.runtimeNodeGuids : null;
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
            initialized = false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (runtimeAsset != null)
            {
                commanderBB?.BuildSerializedReferences(runtimeAsset.blackboardDefinition);
            }
            else
            {
                BehaviourTreeAssetBase authoring = authoringAsset as BehaviourTreeAssetBase;
                if (authoring != null)
                {
                    commanderBB?.BuildSerializedReferences(authoring.BlackboardDefinition);
                }
            }

            if (runtimeAsset == null && authoringAsset == null)
            {
                commanderBB?.ClearSerializedReferences();
            }
        }
#endif
        public void Initialize()
        {
            if (initialized) return;

            runtimeAsset = RuntimeAssetHelper.GetOrBake(runtimeAsset,
#if UNITY_EDITOR
                authoringAsset,
#else
                null,
#endif
                "CommanderTreeRunner");
            if (runtimeAsset == null) return;

            if (commanderBB == null)
            {
                Debug.LogError("[CommanderTreeRunner] Commander BlackBoard is null.");
                return;
            }

            commanderBB.Initialize(runtimeAsset.blackboardDefinition);
            evaluator = new TreeEvaluator(runtimeAsset.runtimeNodeData, runtimeAsset.runtimeFieldData, runtimeAsset.boxedConstants, runtimeAsset.maxTreeDepth);

            debugProvider = GetComponent<RuntimeDebugProvider>();
            if (debugProvider == null) debugProvider = gameObject.AddComponent<RuntimeDebugProvider>();

            // Disable independent update on registered agents — we tick them manually
            cachedBridges = new CommanderBindingBridge[registeredAgents.Count];
            for (int i = 0; i < registeredAgents.Count; i++)
            {
                TreeRunner agent = registeredAgents[i];
                if (agent != null)
                {
                    agent.Initialize(); // Ensure agent BB is initialized before resolving bridge
                    agent.RunIndependently = false;
                    CommanderBindingBridge bridge = agent.GetComponent<CommanderBindingBridge>();
                    cachedBridges[i] = bridge;
                    if (bridge != null)
                    {
                        bridge.ResolveBindings();
                    }
                }
            }

            initialized = true;
        }
#if UNITY_EDITOR

        public Object GetSourceTree()
        {
            if (runtimeAsset != null && runtimeAsset.sourceTree != null) return runtimeAsset.sourceTree;
            return authoringAsset ?? null;
        }
#endif

    }
}
