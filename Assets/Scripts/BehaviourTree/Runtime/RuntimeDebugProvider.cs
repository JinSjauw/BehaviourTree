using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Runtime
{
    /// <summary>
    /// Exposes the current node states so the editor debug window can read them.
    /// </summary>
    public class RuntimeDebugProvider : MonoBehaviour
    {
        public NodeState[] currentNodeStates;
        public int activeNodeIndex = -1;
    }
}