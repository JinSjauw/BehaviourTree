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
                CopySquadsToTree(agent);
                agent.Evaluate();
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
                {
                    squads[i].CopyToBB(runner.BlackBoard, treeDef);
                }
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
                {
                    squads[i].CopyFromBB(runner.BlackBoard, treeDef);
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
        /// Registers an agent with this commander. Assigns agentID, resizes
        /// strided squad-data BB variables.
        /// </summary>
        public void RegisterAgent(AgentTreeRunner agent)
        {
            if (agent == null || registeredAgents.Contains(agent))
                return;

            registeredAgents.Add(agent);

            ResizeSquadDataStrides(registeredAgents.Count);
        }

        /// <summary>
        /// Unregisters an agent. Compacts remaining agent data and resizes
        /// strided BB variables.
        /// </summary>
        public void UnregisterAgent(AgentTreeRunner agent)
        {
            if (agent == null) return;

            int removedIndex = registeredAgents.IndexOf(agent);
            if (removedIndex < 0) return;

            registeredAgents.RemoveAt(removedIndex);

            CompactSquadDataAfterRemoval(removedIndex, registeredAgents.Count + 1);

            ResizeSquadDataStrides(registeredAgents.Count);
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
