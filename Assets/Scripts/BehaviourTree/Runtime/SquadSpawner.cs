using System.Collections.Generic;
using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Runtime
{
    /// <summary>
    /// Handles spawning a commander + N agents and registering them with a squad.
    /// 
    /// Init order:
    ///   1. SquadInstance created and initialized from SquadDefinition
    ///   2. Commander spawned and initialized
    ///   3. Commander registered with squad (bindings resolved)
    ///   4. Each agent spawned, initialized, registered with commander (stride resize)
    ///      and squad (bindings resolved).
    /// 
    /// Cleanup reverses the order on OnDestroy.
    /// </summary>
    public class SquadSpawner : MonoBehaviour
    {
        [SerializeField, Tooltip("The squad data schema. Must have base channel variables (AgentRoles, AgentOrders).")]
        private SquadDefinition definition;

        [SerializeField, Tooltip("Prefab with CommanderTreeRunner component.")]
        private GameObject commanderPrefab;

        [SerializeField, Tooltip("Prefab with AgentTreeRunner component.")]
        private GameObject agentPrefab;

        [SerializeField, Min(0), Tooltip("Number of agents to spawn.")]
        private int agentCount = 3;

        [SerializeField, Tooltip("Spawn automatically in Start(). Disable to trigger manually.")]
        private bool spawnOnStart = true;

        // ── Runtime state ──────────────────────────────────────────────

        private SquadInstance squadInstance;
        private CommanderTreeRunner commanderRunner;
        private readonly List<AgentTreeRunner> spawnedAgents = new List<AgentTreeRunner>();
        private bool hasSpawned;

        // ── Public API ─────────────────────────────────────────────────

        /// <summary>Shorthand: spawns using the serialized fields.</summary>
        public void Spawn()
        {
            Spawn(commanderPrefab, agentPrefab, agentCount);
        }

        /// <summary>Spawns with explicit parameters. Defers to the internal path.</summary>
        public void Spawn(GameObject commanderPrefabOverride, GameObject agentPrefabOverride, int count)
        {
            if (hasSpawned)
            {
                Debug.LogWarning("[SquadSpawner] Already spawned. Despawn first.");
                return;
            }

            if (definition == null)
            {
                Debug.LogError("[SquadSpawner] SquadDefinition is null.");
                return;
            }

            this.commanderPrefab = commanderPrefabOverride != null ? commanderPrefabOverride : commanderPrefab;
            this.agentPrefab = agentPrefabOverride != null ? agentPrefabOverride : agentPrefab;
            this.agentCount = count;

            SpawnSquad();
        }

        public void Despawn()
        {
            DespawnSquad();
        }

        // ── Unity messages ─────────────────────────────────────────────

        private void Start()
        {
            if (spawnOnStart)
                Spawn();
        }

        private void OnDestroy()
        {
            DespawnSquad();
        }

        // ── Implementation ─────────────────────────────────────────────

        private void SpawnSquad()
        {
            // 1 — Squad instance
            GameObject squadGo = new GameObject($"[Squad] {definition.name}");
            squadInstance = squadGo.AddComponent<SquadInstance>();
            squadInstance.Initialize(definition);

            // 2 — Commander
            if (commanderPrefab == null)
            {
                Debug.LogError("[SquadSpawner] Commander prefab is null.");
                Destroy(squadGo);
                return;
            }

            GameObject commanderGo = Instantiate(commanderPrefab, transform);
            commanderGo.name = $"[Commander] {commanderPrefab.name}";
            commanderRunner = commanderGo.GetComponent<CommanderTreeRunner>();
            if (commanderRunner == null)
            {
                Debug.LogError($"[SquadSpawner] Commander prefab '{commanderPrefab.name}' has no CommanderTreeRunner component.");
                Destroy(commanderGo);
                Destroy(squadGo);
                return;
            }

            commanderRunner.Initialize();
            commanderRunner.RegisterSquad(squadInstance);

            // 3 — Agents (clamped to squad's max capacity)
            int effectiveCount = Mathf.Min(agentCount, definition.MaxAgents);
            if (agentCount > definition.MaxAgents)
            {
                Debug.LogWarning($"[SquadSpawner] agentCount ({agentCount}) exceeds SquadDefinition.MaxAgents ({definition.MaxAgents}). Clamping to {effectiveCount}.");
            }
            for (int i = 0; i < effectiveCount; i++)
            {
                SpawnAgent(i);
            }

            hasSpawned = true;
        }

        private void SpawnAgent(int agentIndex)
        {
            if (agentPrefab == null)
            {
                Debug.LogError("[SquadSpawner] Agent prefab is null.");
                return;
            }

            GameObject agentGo = Instantiate(agentPrefab, transform);
            agentGo.name = $"[Agent {agentIndex}] {agentPrefab.name}";

            AgentTreeRunner agent = agentGo.GetComponent<AgentTreeRunner>();
            if (agent == null)
            {
                Debug.LogError($"[SquadSpawner] Agent prefab '{agentPrefab.name}' has no AgentTreeRunner component.");
                Destroy(agentGo);
                return;
            }

            // Set references — agent auto-registers in OnPostInitialize
            agent.commander = commanderRunner;
            agent.squadInstance = squadInstance;

            agent.Initialize();
            agent.RunIndependently = false;

            // AssignRole needs the BB to be initialized (done by Initialize() above)
            AssignRole(agent, agentIndex);

            spawnedAgents.Add(agent);
        }

        /// <summary>
        /// Writes the agent's assigned role into the AgentAssignedRole BB variable.
        /// Roles are taken from the squad definition's availableRoles list, cycling
        /// through fallback roles when agentIndex exceeds the role count.
        /// </summary>
        private void AssignRole(AgentTreeRunner agent, int agentIndex)
        {
            if (definition == null || definition.availableRoles == null || definition.availableRoles.Count == 0)
                return;

            BlackBoard bb = agent.BlackBoard;
            BlackboardDefinition bbDef = bb?.Definition;
            if (bbDef == null) return;

            int variableIndex = bbDef.GetVariableIndex("AgentAssignedRole");
            if (variableIndex < 0) return;

            // Compute base slot by summing strides of all preceding variables
            int baseSlot = 0;
            IReadOnlyList<BlackboardVariableBase> variables = bbDef.GetAllVariables();
            for (int i = 0; i < variableIndex && i < variables.Count; i++)
            {
                int stride = variables[i].Stride;
                baseSlot += (stride > 1) ? stride : 1;
            }

            int roleIndex = agentIndex % definition.availableRoles.Count;
            int roleValue = roleIndex;
            bb.SetBoxed(baseSlot, roleValue);
        }

        private void DespawnSquad()
        {
            if (!hasSpawned) return;

            // Destroy agents first — OnDisable auto-deregisters from commander + squad
            for (int i = spawnedAgents.Count - 1; i >= 0; i--)
            {
                if (spawnedAgents[i] != null)
                    Destroy(spawnedAgents[i].gameObject);
            }
            spawnedAgents.Clear();

            // Destroy commander — OnDisable clears agent refs
            if (commanderRunner != null)
            {
                Destroy(commanderRunner.gameObject);
                commanderRunner = null;
            }

            // Destroy squad instance last (agents + commander already deregistered via OnDisable)
            if (squadInstance != null)
            {
                Destroy(squadInstance.gameObject);
                squadInstance = null;
            }

            hasSpawned = false;
        }
    }
}
