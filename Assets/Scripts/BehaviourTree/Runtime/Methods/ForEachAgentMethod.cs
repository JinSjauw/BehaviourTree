using BehaviourTree.Core;

namespace BehaviourTree.Runtime.Methods
{
    /// <summary>
    /// ForEachAgent iterates all children for every registered agent.
    /// Sets blackBoard.currentAgentOffset so leaves read/write the correct per-agent slot.
    /// Continues past child SUCCESS and FAILURE — only RUNNING pauses the loop.
    /// Resume position tracked via runningAgentIndex (agent) and activeChildIndex (child).
    /// </summary>
    [NodeMethod("ForEachAgent", allowedTreeType = AllowedTreeType.Commander)]
    public sealed class ForEachAgentMethod : CompositeMethod
    {
        public override NodeState Execute(int nodeIndex, ref TickContext ctx)
        {
            ref NodeData node = ref ctx.nodeDatas[nodeIndex];
            if (node.firstChildIndex < 0) return NodeState.SUCCESS;

            BlackBoard bb = ctx.blackBoard;
            int savedOffset = bb.currentAgentOffset;
            int agentIndex = ctx.runningAgentIndex[nodeIndex];
            int childIndex = ctx.activeChildIndex[nodeIndex];
            int agentCount = ctx.agentCount;
            int childCount = node.lastChildIndex - node.firstChildIndex + 1;

            for (; agentIndex < agentCount; agentIndex++)
            {
                bb.currentAgentOffset = agentIndex;

                for (; childIndex < childCount; childIndex++)
                {
                    int globalChildIndex = node.firstChildIndex + childIndex;
                    NodeState result = TickDispatcher.TickNode(globalChildIndex, ref ctx);

                    if (result == NodeState.RUNNING)
                    {
                        ctx.runningAgentIndex[nodeIndex] = agentIndex;
                        ctx.activeChildIndex[nodeIndex] = childIndex;
                        bb.currentAgentOffset = savedOffset;
                        return NodeState.RUNNING;
                    }

                    // SUCCESS or FAILURE — continue to next child
                }

                childIndex = 0;
            }

            bb.currentAgentOffset = savedOffset;
            ctx.runningAgentIndex[nodeIndex] = 0;
            ctx.activeChildIndex[nodeIndex] = 0;
            return NodeState.SUCCESS;
        }
    }
}
