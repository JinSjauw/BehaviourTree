using BehaviourTree.Core;

namespace BehaviourTree.Runtime.Methods
{
    /// <summary>
    /// Priority: same as Selector but always re-evaluates from the first child
    /// every tick (resets activeChildIndex to 0 before ticking).
    /// </summary>
    [NodeMethod("PRIORITY")]
    public sealed class PriorityMethod : CompositeMethod
    {

        public override NodeState Execute(int nodeIndex, ref TickContext ctx)
        {
            ref NodeData node = ref ctx.nodeDatas[nodeIndex];
            if (node.firstChildIndex < 0) return NodeState.FAILURE;

            ctx.activeChildIndex[nodeIndex] = 0;

            int childCount = node.lastChildIndex - node.firstChildIndex + 1;

            for (int child = 0; child < childCount; child++)
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
            }

            ctx.activeChildIndex[nodeIndex] = 0;
            return NodeState.FAILURE;
        }
    }
}
