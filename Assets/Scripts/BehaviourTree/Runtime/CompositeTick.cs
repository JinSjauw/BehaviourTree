using BehaviourTree.Core;

namespace BehaviourTree.Runtime
{
    /// <summary>
    /// Tick functions for composite nodes (Sequence, Selector, Priority).
    /// Uses activeChildIndex for resumption across frames.
    /// </summary>
    internal static partial class TickFunctions
    {
        /// <summary>
        /// Sequence: ticks children left-to-right. Stops on FAILURE, saves index on RUNNING,
        /// returns SUCCESS when all children succeed. Resets activeChildIndex on completion.
        /// </summary>
        internal static NodeState TickSequence(int nodeIndex, ref TickContext ctx)
        {
            ref NodeData node = ref ctx.nodeDatas[nodeIndex];
            if (node.firstChildIndex < 0)
                return NodeState.SUCCESS;

            int child = ctx.activeChildIndex[nodeIndex];
            int childCount = node.lastChildIndex - node.firstChildIndex + 1;

            while (child < childCount)
            {
                int childIndex = node.firstChildIndex + child;
                NodeState result = TickDispatcher.TickNode(childIndex, ref ctx);
                ctx.nodeStates[childIndex] = result;

                if (result == NodeState.RUNNING)
                {
                    ctx.activeChildIndex[nodeIndex] = child;
                    return NodeState.RUNNING;
                }
                if (result == NodeState.FAILURE)
                {
                    ctx.activeChildIndex[nodeIndex] = 0;
                    return NodeState.FAILURE;
                }

                child++;
            }

            ctx.activeChildIndex[nodeIndex] = 0;
            return NodeState.SUCCESS;
        }

        /// <summary>
        /// Selector: ticks children left-to-right. Stops on SUCCESS, saves index on RUNNING,
        /// returns FAILURE when all children fail. Resets activeChildIndex on completion.
        /// </summary>
        internal static NodeState TickSelector(int nodeIndex, ref TickContext ctx)
        {
            ref NodeData node = ref ctx.nodeDatas[nodeIndex];
            if (node.firstChildIndex < 0)
                return NodeState.FAILURE;

            int child = ctx.activeChildIndex[nodeIndex];
            int childCount = node.lastChildIndex - node.firstChildIndex + 1;

            while (child < childCount)
            {
                int childIndex = node.firstChildIndex + child;
                NodeState result = TickDispatcher.TickNode(childIndex, ref ctx);
                ctx.nodeStates[childIndex] = result;

                if (result == NodeState.RUNNING)
                {
                    ctx.activeChildIndex[nodeIndex] = child;
                    return NodeState.RUNNING;
                }
                if (result == NodeState.SUCCESS)
                {
                    ctx.activeChildIndex[nodeIndex] = 0;
                    return NodeState.SUCCESS;
                }

                child++;
            }

            ctx.activeChildIndex[nodeIndex] = 0;
            return NodeState.FAILURE;
        }

        /// <summary>
        /// Priority: same as Selector but always re-evaluates from the first child
        /// every tick (resets activeChildIndex to 0 before ticking).
        /// </summary>
        internal static NodeState TickPriority(int nodeIndex, ref TickContext ctx)
        {
            ctx.activeChildIndex[nodeIndex] = 0;
            return TickSelector(nodeIndex, ref ctx);
        }
    }
}
