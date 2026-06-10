using BehaviourTree.Core;

namespace BehaviourTree.Runtime
{
    /// <summary>
    /// Tick function for Subtree nodes.
    /// Simply ticks the expanded first child. The subtree's internal
    /// tree is already flattened into the main node array by TreeBaker.
    /// </summary>
    internal static partial class TickFunctions
    {
        internal static NodeState TickSubtree(int nodeIndex, ref TickContext ctx)
        {
            ref NodeData node = ref ctx.nodeDatas[nodeIndex];
            int childIndex = node.firstChildIndex;

            if (childIndex < 0) return NodeState.FAILURE;

            return TickDispatcher.TickNode(childIndex, ref ctx);
        }
    }
}
