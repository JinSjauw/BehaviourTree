using BehaviourTree.Core;

namespace BehaviourTree.Runtime.Methods
{
    /// <summary>
    /// Sequence: ticks children left-to-right. Stops on FAILURE, saves index on RUNNING,
    /// returns SUCCESS when all children succeed. Resets activeChildIndex on completion.
    /// </summary>
    [NodeMethod("SEQUENCE")]
    public sealed class SequenceMethod : CompositeMethod
    {

        public override NodeState Execute(int nodeIndex, ref TickContext ctx)
        {
            ref NodeData node = ref ctx.nodeDatas[nodeIndex];
            if (node.firstChildIndex < 0) return NodeState.SUCCESS;

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
    }
}
