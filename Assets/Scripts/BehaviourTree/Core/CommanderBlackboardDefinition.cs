using UnityEngine;

namespace BehaviourTree.Core
{
    /// <summary>
    /// A blackboard definition specifically for commander trees.
    /// Inherits all variable declaration from BlackboardDefinition,
    /// adds maxSize for per-agent array slots (stride).
    /// Use this instead of the base class when creating commander BB assets.
    /// </summary>
    [CreateAssetMenu(menuName = "BehaviourTree/Commander Blackboard")]
    public class CommanderBlackboardDefinition : BlackboardDefinition
    {
        [Tooltip("Maximum number of agents this commander can manage. Used as stride for per-agent variables.")]
        public int maxSize = 8;
    }
}
