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

        /// <summary>Squad instances this agent has registered with.
        /// Populated via RegisterSquad() during spawn or by the commander.</summary>
        [System.NonSerialized] public List<SquadInstance> registeredSquads = new List<SquadInstance>();

        /// <summary>The commander that owns this agent. Set by SquadSpawner or scene setup.</summary>
        [SerializeField] public CommanderTreeRunner commander;

        /// <summary>The squad instance this agent is part of. Set by SquadSpawner or scene setup.</summary>
        [SerializeField] public SquadInstance squadInstance;

        private void Start()
        {
            if (!runIndependently) return;
            Initialize();
        }

        private void OnEnable()
        {
            if (commander != null && !initialized)
            {
                Initialize();
            }

            if (commander != null && initialized)
            {
                RegisterWithCommanderAndSquad();
            }
        }

        protected override void OnDisable()
        {
            UnregisterFromCommanderAndSquad();
            base.OnDisable();
        }

        private void Update()
        {
            if (!runIndependently) return;

            PushTrackedBindings();
            Evaluate();
        }

        protected override void OnPostInitialize()
        {
            ResolveTrackedBindings();

            // Auto-register if commander/squad references are set
            if (commander != null)
            {
                RegisterWithCommanderAndSquad();
            }
        }

        /// <summary>
        /// Registers this agent with its commander and squad instance.
        /// Safe to call multiple times — guards against duplicates.
        /// </summary>
        private void RegisterWithCommanderAndSquad()
        {
            if (commander != null)
            {
                commander.RegisterAgent(this);
            }

            if (squadInstance != null)
            {
                RegisterSquad(squadInstance);
            }
        }

        /// <summary>
        /// Unregisters this agent from its commander and squad instance.
        /// Safe to call multiple times.
        /// </summary>
        private void UnregisterFromCommanderAndSquad()
        {
            if (squadInstance != null)
            {
                UnregisterSquad(squadInstance);
            }

            if (commander != null)
            {
                commander.UnregisterAgent(this);
            }
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
