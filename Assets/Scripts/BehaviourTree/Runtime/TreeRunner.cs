using System.Collections.Generic;
using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Runtime
{
    [RequireComponent(typeof(BlackBoard))]
    public class AgentTreeRunner : BehaviourTreeRunnerBase
    {
        private bool runIndependently = true;
        public bool RunIndependently
        {
            get => runIndependently;
            set => runIndependently = value;
        }

        private List<IBlackboardDataProvider> dataProviders;

        /// <summary>Squad instances this agent has registered with.
        /// Populated via RegisterSquad() during spawn or by the commander.</summary>
        [System.NonSerialized] public List<SquadInstance> registeredSquads = new List<SquadInstance>();

        private void Start()
        {
            if (!runIndependently) return;
            Initialize();
        }

        private void Update()
        {
            if (!runIndependently) return;

            PushDataProviders();
            PushTrackedBindings();
            Evaluate();
        }

        internal void PushDataProviders()
        {
            if (dataProviders == null) return;

            for (int i = 0; i < dataProviders.Count; i++)
            {
                dataProviders[i].ProvideData(blackBoard);
            }
        }

        protected override void OnPostInitialize()
        {
            // Collect all data providers on this GameObject
            IBlackboardDataProvider[] providers = GetComponentsInChildren<IBlackboardDataProvider>();
            dataProviders = new List<IBlackboardDataProvider>(providers);

            ResolveTrackedBindings();
        }

        /// <summary>
        /// Registers this agent with a squad instance. Resolves bindings
        /// between the agent's tree BB and the squad BB.
        /// Called during spawn setup or by the commander.
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
        /// Unregisters this agent from a squad instance.
        /// Called when the agent is despawned or removed from the commander.
        /// </summary>
        public void UnregisterSquad(SquadInstance squad)
        {
            registeredSquads.Remove(squad);
        }
    }
}
