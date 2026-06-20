using System;
using UnityEngine;

namespace BehaviourTree.Core
{
    /// <summary>
    /// A role available in a squad, with visual and behavioural properties.
    /// </summary>
    [Serializable]
    public class SquadRole
    {
        /// <summary>Display name of the role (e.g. "Scout", "Flanker").</summary>
        public string name;

        /// <summary>Colour used to identify agents with this role in the UI.</summary>
        public Color colour = Color.gray;

        /// <summary>Maximum number of agents that can be assigned this role.</summary>
        public int maxAmount = 1;

        /// <summary>If true, agents are assigned this role when no other role matches.</summary>
        public bool isFallback;
    }
}
