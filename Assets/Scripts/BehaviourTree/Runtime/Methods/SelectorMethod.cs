using BehaviourTree.Core;

namespace BehaviourTree.Runtime.Methods
{
    /// <summary>
    /// Selector: ticks children left-to-right. Stops on SUCCESS, saves index on RUNNING,
    /// returns FAILURE when all children fail. Resets activeChildIndex on completion.
    /// </summary>
    [NodeMethod("SELECTOR")]
    public sealed class SelectorMethod : CompositeMethod
    {

        public override NodeState Execute(int nodeIndex, ref TickContext ctx)
        {
            ref NodeData node = ref ctx.nodeDatas[nodeIndex];
            if (node.firstChildIndex < 0) return NodeState.FAILURE;

            int child = ctx.activeChildIndex[nodeIndex];
            int childCount = node.lastChildIndex - node.firstChildIndex + 1;

            while (child < childCount)
            {
                int childIndex = node.firstChildIndex + child;
                NodeState result = TickDispatcher.TickNode(childIndex, ref ctx);

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
    }
}
