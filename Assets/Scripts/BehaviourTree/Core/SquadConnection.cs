using System;
using UnityEngine;

namespace BehaviourTree.Core
{
    /// <summary>
    /// Stored on tree assets (BehaviourTreeAssetBase.squadConnections).
    /// Links a tree to a squad and optionally assigns a role.
    /// The actual variable bindings live on SquadDefinition, keyed by treeAsset.
    /// </summary>
    [Serializable]
    public class SquadConnection
    {
        /// <summary>The squad this tree connects to.</summary>
        public SquadDefinition squad;

        /// <summary>
        /// Agent-only. Role this agent plays in the squad.
        /// Picked from squad.availableRoles (e.g. "Scout", "Flanker").
        /// Null or empty for commander trees.
        /// </summary>
        public string assignedRole;
    }
}
