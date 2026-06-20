using System.Collections.Generic;
using UnityEngine;

namespace BehaviourTree.Core
{
    public class BehaviourTreeAssetBase : ScriptableObject
    {
        [HideInInspector] public BehaviourNode root;
        [HideInInspector] public BlackboardDefinition blackboardDefinition;
        [HideInInspector] public CommanderBlackboardDefinition commanderBlackboardDefinition;

        /// <summary>
        /// Squads this tree connects to. Each entry pairs a SquadDefinition
        /// with an optional role assignment (agent-only).
        /// </summary>
        public List<SquadConnection> squadConnections = new List<SquadConnection>();

        public BehaviourNode Root => root;
        public BlackboardDefinition BlackboardDefinition => blackboardDefinition;
        public CommanderBlackboardDefinition CommanderBlackboardDefinition => commanderBlackboardDefinition;
        public string DisplayName => name ?? "NO NAME GIVEN";
    }
}
