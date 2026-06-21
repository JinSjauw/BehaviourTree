using System.Collections.Generic;
using UnityEngine;

namespace BehaviourTree.Core
{
    public class BehaviourTreeAssetBase : ScriptableObject
    {
        [HideInInspector] public BehaviourNode root;
        [HideInInspector] public BlackboardDefinition blackboardDefinition;
        [HideInInspector] public BlackboardDefinition commanderBlackboardDefinition;

        /// <summary>
        /// Squads this tree connects to. Each entry pairs a SquadDefinition
        /// with an optional role assignment (agent-only).
        /// </summary>
        public List<SquadConnection> squadConnections = new List<SquadConnection>();

        public BehaviourNode Root => root;
        public BlackboardDefinition BlackboardDefinition => blackboardDefinition;
        public BlackboardDefinition CommanderBlackboardDefinition => commanderBlackboardDefinition;
        public string DisplayName => name ?? "NO NAME GIVEN";

        /// <summary>
        /// When true, commander BB variables preserve their stride during baking
        /// so that per-agent slot offsets match the storage layout. Commander trees
        /// override this to true; agent trees use the default (false, stride=1).
        /// </summary>
        public virtual bool PreserveCommanderStride => false;
    }
}
