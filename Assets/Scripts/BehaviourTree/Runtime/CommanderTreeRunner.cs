using System.Collections.Generic;
using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Runtime
{
    /// <summary>
    /// Orchestrates a group of agents under a commander behaviour tree.
    /// Per-frame flow:
    ///   1. EvaluateCommander:
    ///      a. Squads → commander BB (agent status from last frame)
    ///      b. Commander evaluates (reads state, writes orders)
    ///      c. Commander → squads (orders)
    ///   2. TickAgents: for each agent
    ///      a. Squad → agent BB (fresh orders + status)
    ///      b. Push data providers → agent self BB
    ///      c. Agent evaluates (reacts to orders)
    ///      d. Agent → squad BB (reports new status)
    /// Communication between agents and commander happens exclusively through
    /// the squad blackboard channel.
    /// </summary>
    [RequireComponent(typeof(BlackBoard))]
    public class CommanderTreeRunner : BehaviourTreeRunnerBase
    {
        [SerializeField] private List<AgentTreeRunner> registeredAgents = new List<AgentTreeRunner>();

        /// <summary>Squad instances this commander has registered with.</summary>
        [System.NonSerialized] public List<SquadInstance> registeredSquads = new List<SquadInstance>();

        private void Start()
        {
            Initialize();
        }

        protected override void OnDisable()
        {
            // Deregister all agents — they may outlive the commander
            for (int i = registeredAgents.Count - 1; i >= 0; i--)
            {
                AgentTreeRunner agent = registeredAgents[i];
                if (agent != null)
                {
                    // Clear the agent's commander reference to break the cycle
                    agent.commander = null;
                }
                registeredAgents.RemoveAt(i);
            }
        }

        private void Update()
        {
            if (evaluator == null || blackBoard == null) return;

            PushTrackedBindings();
            EvaluateCommander();
            TickAgents();
        }

        /// <summary>
        /// Agents tick after commander: squad BB has fresh orders from commander.
        /// Each agent reads squad data, pushes own data providers, evaluates,
        /// then writes results back to squad BB.
        /// The agent index is used as the offset into per-agent squad data slots.
        /// </summary>
        private void TickAgents()
        {
            for (int i = 0; i < registeredAgents.Count; i++)
            {
                AgentTreeRunner agent = registeredAgents[i];
                if (agent == null)
                {
                    continue;
                }

                agent.PushDataProviders();
                CopySquadsToTree(agent, i);
                agent.Evaluate();
                CopySquadsFromTree(agent, i);
            }
        }

        /// <summary>
        /// Copies squad data into a tree runner's BB (agent or commander).
        /// When agentOffset is >= 0, per-agent squad variables use it as
        /// the slot offset to read the correct agent's data.
        /// </summary>
        private static void CopySquadsToTree(BehaviourTreeRunnerBase runner, int agentOffset = -1)
        {
            List<SquadInstance> squads = GetRunnerSquads(runner);
            if (squads == null || squads.Count == 0) return;

            BlackboardDefinition treeDef = runner.BlackBoard?.Definition;
            if (treeDef == null) return;

            for (int i = 0; i < squads.Count; i++)
            {
                if (squads[i] != null)
                {
                    squads[i].CopyToBB(runner.BlackBoard, treeDef, agentOffset);
                }
            }
        }

        /// <summary>
        /// Copies tree runner's BB data back to all registered squads.
        /// When agentOffset is >= 0, per-agent squad variables use it as
        /// the slot offset to write to the correct agent's slot.
        /// </summary>
        private static void CopySquadsFromTree(BehaviourTreeRunnerBase runner, int agentOffset = -1)
        {
            List<SquadInstance> squads = GetRunnerSquads(runner);
            if (squads == null || squads.Count == 0) return;

            BlackboardDefinition treeDef = runner.BlackBoard?.Definition;
            if (treeDef == null) return;

            for (int i = 0; i < squads.Count; i++)
            {
                if (squads[i] != null)
                {
                    squads[i].CopyFromBB(runner.BlackBoard, treeDef, agentOffset);
                }
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
        /// Commander evaluates first: reads agent status from squad BB (last frame),
        /// runs the commander behaviour tree, writes orders back to squad BB.
        /// </summary>
        private void EvaluateCommander()
        {
            CopySquadsToTree(this);

            evaluator.agentCount = registeredAgents.Count;
            evaluator.Evaluate(blackBoard);

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
            ResolveTrackedBindings();

            for (int i = 0; i < registeredAgents.Count; i++)
            {
                AgentTreeRunner agent = registeredAgents[i];
                if (agent != null)
                {
                    agent.Initialize();
                    agent.RunIndependently = false;
                }
            }
        }

        /// <summary>
        /// Registers an agent with this commander. The stride is fixed at bake time
        /// (from the CommanderBlackboardDefinition asset), so no runtime resize occurs.
        /// Logs an error if the agent count would exceed the available stride capacity.
        /// </summary>
        public void RegisterAgent(AgentTreeRunner agent)
        {
            if (agent == null || registeredAgents.Contains(agent))
                return;

            // Verify capacity against the fixed stride set in the asset
            int maxStride = GetMaxSquadDataStride();
            if (maxStride > 0 && registeredAgents.Count >= maxStride)
            {
                Debug.LogError($"[CommanderTreeRunner] Cannot register agent: squad-data stride ({maxStride}) exceeded. " +
                               "Increase the stride on squad-data variables in the Commander Blackboard Definition to support more agents.");
                return;
            }

            registeredAgents.Add(agent);
        }

        /// <summary>
        /// Unregisters an agent. With fixed strides, no compaction is needed —
        /// the slot for the removed agent becomes unused and will not be accessed
        /// (ForEachAgent limits iteration to agentCount).
        /// </summary>
        public void UnregisterAgent(AgentTreeRunner agent)
        {
            if (agent == null) return;
            registeredAgents.Remove(agent);
        }

        /// <summary>
        /// Returns the stride of the first squad-data variable in the commander BB,
        /// or 0 if no squad-data variables exist.
        /// </summary>
        private int GetMaxSquadDataStride()
        {
            if (blackBoard?.Definition == null) return 0;
            IReadOnlyList<BlackboardVariableBase> vars = blackBoard.Definition.GetAllVariables();
            for (int i = 0; i < vars.Count; i++)
            {
                if (vars[i].isSquadData)
                    return vars[i].Stride;
            }
            return 0;
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
