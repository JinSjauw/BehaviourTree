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
    public class CommanderTreeRunner : BehaviourTreeRunnerBase
    {
        [SerializeField] private List<AgentTreeRunner> registeredAgents = new List<AgentTreeRunner>();
        private CommanderBindingBridge[] cachedBridges;

        private void Start()
        {
            Initialize();
        }

        private void Update()
        {
            if (evaluator == null || blackBoard == null) return;

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
                AgentTreeRunner agent = registeredAgents[i];
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
            evaluator.Evaluate(blackBoard);

            if (debugProvider != null)
            {
                debugProvider.currentNodeStates = evaluator.nodeStates;
                debugProvider.activeNodeIndex = evaluator.currentNodeIndex;
                debugProvider.currentNodeGuids = runtimeAsset != null ? runtimeAsset.runtimeNodeGuids : null;
            }
        }

        protected override void OnPostInitialize()
        {
            // Disable independent update on registered agents — we tick them manually
            cachedBridges = new CommanderBindingBridge[registeredAgents.Count];
            for (int i = 0; i < registeredAgents.Count; i++)
            {
                AgentTreeRunner agent = registeredAgents[i];
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
        }
    }
}
