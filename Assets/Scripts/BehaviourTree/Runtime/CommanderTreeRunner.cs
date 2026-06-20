using System.Collections.Generic;
using System.Reflection;
using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Runtime
{
    /// <summary>
    /// Orchestrates a group of agents under a commander behaviour tree.
    /// Each frame: copies squad data → agent BB, ticks all agents 
    /// (bridge push → copy commander → evaluate → copy agent), copies agent data → squad,
    /// copies squad data → commander BB, evaluates commander tree, copies commander → squad.
    /// </summary>
    [RequireComponent(typeof(BlackBoard))]
    public class CommanderTreeRunner : BehaviourTreeRunnerBase
    {
        [SerializeField] private List<AgentTreeRunner> registeredAgents = new List<AgentTreeRunner>();
        private CommanderBindingBridge[] cachedBridges;

        /// <summary>Squad instances this commander has registered with.</summary>
        [System.NonSerialized] public List<SquadInstance> registeredSquads = new List<SquadInstance>();

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
        /// Ticks each registered agent:
        ///   1. Squad → agent BB
        ///   2. Bridge: push data providers, copy commander → agent
        ///   3. Agent evaluates
        ///   4. Bridge: copy agent → commander
        ///   5. Agent → squad
        /// </summary>
        private void TickAgents()
        {
            if (cachedBridges == null) return;

            for (int i = 0; i < registeredAgents.Count; i++)
            {
                AgentTreeRunner agent = registeredAgents[i];
                CommanderBindingBridge bridge = cachedBridges[i];
                if (agent == null) continue;

                // 1. Squad → agent
                CopySquadsToTree(agent);

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

                // 5. Agent → squad
                CopySquadsFromTree(agent);
            }
        }

        /// <summary>
        /// Copies squad data into a tree runner's BB (agent or commander).
        /// Iterates all squads the tree is registered with.
        /// </summary>
        private static void CopySquadsToTree(BehaviourTreeRunnerBase runner)
        {
            List<SquadInstance> squads = GetRunnerSquads(runner);
            if (squads == null || squads.Count == 0) return;

            BlackboardDefinition treeDef = runner.BlackBoard?.Definition;
            if (treeDef == null) return;

            for (int i = 0; i < squads.Count; i++)
            {
                if (squads[i] != null)
                    squads[i].CopyToBB(runner.BlackBoard, treeDef);
            }
        }

        /// <summary>
        /// Copies tree runner's BB data back to all registered squads.
        /// </summary>
        private static void CopySquadsFromTree(BehaviourTreeRunnerBase runner)
        {
            List<SquadInstance> squads = GetRunnerSquads(runner);
            if (squads == null || squads.Count == 0) return;

            BlackboardDefinition treeDef = runner.BlackBoard?.Definition;
            if (treeDef == null) return;

            for (int i = 0; i < squads.Count; i++)
            {
                if (squads[i] != null)
                    squads[i].CopyFromBB(runner.BlackBoard, treeDef);
            }
        }

        /// <summary>
        /// Gets the registered squads for a runner. Handles both AgentTreeRunner
        /// and CommanderTreeRunner types.
        /// </summary>
        private static List<SquadInstance> GetRunnerSquads(BehaviourTreeRunnerBase runner)
        {
            if (runner is AgentTreeRunner agentRunner)
                return agentRunner.registeredSquads;
            if (runner is CommanderTreeRunner commanderRunner)
                return commanderRunner.registeredSquads;
            return null;
        }

        /// <summary>
        /// Evaluates the commander tree against the commander BB.
        /// Squad → commander data is copied first, then commander → squad after evaluation.
        /// </summary>
        private void EvaluateCommander()
        {
            // Squad → commander
            CopySquadsToTree(this);

            evaluator.Evaluate(blackBoard);

            // Commander → squad
            CopySquadsFromTree(this);

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

        /// <summary>
        /// Registers an agent with this commander. Assigns agentID, resizes
        /// strided squad-data BB variables, and wires up the bridge.
        /// </summary>
        public void RegisterAgent(AgentTreeRunner agent)
        {
            if (agent == null || registeredAgents.Contains(agent))
                return;

            int agentID = registeredAgents.Count;
            registeredAgents.Add(agent);

            // Resize commander BB squad-data strides to accommodate the new agent
            ResizeSquadDataStrides(registeredAgents.Count);

            CommanderBindingBridge bridge = agent.GetComponent<CommanderBindingBridge>();
            if (bridge != null)
            {
                // Set agentID via reflection (field is serialized private)
                FieldInfo agentIdField = typeof(CommanderBindingBridge).GetField("agentID",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (agentIdField != null)
                    agentIdField.SetValue(bridge, agentID);
            }

            // Rebuild bridge cache
            CommanderBindingBridge[] newCache = new CommanderBindingBridge[registeredAgents.Count];
            if (cachedBridges != null)
            {
                for (int i = 0; i < cachedBridges.Length; i++)
                    newCache[i] = cachedBridges[i];
            }
            newCache[agentID] = bridge;
            cachedBridges = newCache;
        }

        /// <summary>
        /// Unregisters an agent. Compacts remaining agent data, resizes
        /// strided BB variables, and updates remaining agent IDs.
        /// </summary>
        public void UnregisterAgent(AgentTreeRunner agent)
        {
            if (agent == null) return;

            int removedIndex = registeredAgents.IndexOf(agent);
            if (removedIndex < 0) return;

            registeredAgents.RemoveAt(removedIndex);

            // Compact remaining agent data in strided variables
            CompactSquadDataAfterRemoval(removedIndex, registeredAgents.Count + 1);

            // Resize squad-data strides to new agent count
            ResizeSquadDataStrides(registeredAgents.Count);

            // Update agent IDs for agents shifted down
            for (int i = removedIndex; i < registeredAgents.Count; i++)
            {
                CommanderBindingBridge bridge = registeredAgents[i]?.GetComponent<CommanderBindingBridge>();
                if (bridge != null)
                {
                    FieldInfo agentIdField = typeof(CommanderBindingBridge).GetField("agentID",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    if (agentIdField != null)
                        agentIdField.SetValue(bridge, i);
                }
            }

            // Rebuild bridge cache
            CommanderBindingBridge[] newCache = new CommanderBindingBridge[registeredAgents.Count];
            for (int i = 0; i < registeredAgents.Count; i++)
                newCache[i] = registeredAgents[i]?.GetComponent<CommanderBindingBridge>();
            cachedBridges = newCache;
        }

        /// <summary>
        /// Resizes all squad-data variable strides in the commander BB
        /// to the given agent count. Preserves existing data.
        /// </summary>
        private void ResizeSquadDataStrides(int newAgentCount)
        {
            if (blackBoard?.Storage is not ManagedBlackboardStorage storage)
                return;

            BlackboardDefinition def = blackBoard.Definition;
            if (def == null) return;

            IReadOnlyList<BlackboardVariableBase> vars = def.GetAllVariables();
            bool hasSquadData = false;
            for (int i = 0; i < vars.Count; i++)
            {
                if (vars[i].isSquadData)
                {
                    vars[i].Stride = Mathf.Max(1, newAgentCount);
                    hasSquadData = true;
                }
            }

            if (hasSquadData)
                storage.ResizeFromVariables(vars);
        }

        /// <summary>
        /// Compacts squad-data slots after an agent is removed.
        /// Shifts data for agents with higher IDs down by one slot.
        /// </summary>
        private void CompactSquadDataAfterRemoval(int removedIndex, int oldAgentCount)
        {
            if (blackBoard?.Storage is not ManagedBlackboardStorage storage)
                return;

            BlackboardDefinition def = blackBoard.Definition;
            if (def == null) return;

            IReadOnlyList<BlackboardVariableBase> vars = def.GetAllVariables();
            int baseSlot = 0;

            for (int varIndex = 0; varIndex < vars.Count; varIndex++)
            {
                BlackboardVariableBase variable = vars[varIndex];
                int stride = variable.Stride;
                int effectiveStride = (stride > 1) ? stride : 1;

                if (variable.isSquadData && effectiveStride > 1)
                {
                    // Shift slots: for each agent after the removed one, copy its data down
                    for (int agentIndex = removedIndex; agentIndex < oldAgentCount - 1; agentIndex++)
                    {
                        int srcSlot = baseSlot + agentIndex + 1;
                        int dstSlot = baseSlot + agentIndex;
                        storage.SetBoxed(dstSlot, storage.GetBoxed(srcSlot));
                    }
                }

                baseSlot += effectiveStride;
            }
        }

        /// <summary>
        /// Registers this commander with a squad instance.
        /// </summary>
        public void RegisterSquad(SquadInstance squad)
        {
            if (squad == null || registeredSquads.Contains(squad))
                return;

            registeredSquads.Add(squad);

            if (blackBoard != null && blackBoard.Definition != null)
                squad.EnsureResolved(blackBoard.Definition);
        }

        /// <summary>
        /// Unregisters this commander from a squad instance.
        /// </summary>
        public void UnregisterSquad(SquadInstance squad)
        {
            registeredSquads.Remove(squad);
        }

    }
}
