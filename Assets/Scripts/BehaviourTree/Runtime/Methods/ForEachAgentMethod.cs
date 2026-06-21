using BehaviourTree.Core;

namespace BehaviourTree.Runtime.Methods
{
    /// <summary>
    /// ForEachAgent iterates its children for every registered agent.
    /// Sets blackBoard.currentAgentOffset so leaves read/write the correct per-agent slot.
    /// Continues past child SUCCESS and FAILURE — only RUNNING pauses the loop.
    /// Resume position is saved in runningAgentIndex.
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
            int startIndex = ctx.runningAgentIndex[nodeIndex];
            int count = ctx.agentCount;

            for (int agentIndex = startIndex; agentIndex < count; agentIndex++)
            {
                bb.currentAgentOffset = agentIndex;

                NodeState result = TickDispatcher.TickNode(node.firstChildIndex, ref ctx);

                if (result == NodeState.RUNNING)
                {
                    ctx.runningAgentIndex[nodeIndex] = agentIndex;
                    bb.currentAgentOffset = savedOffset;
                    return NodeState.RUNNING;
                }

                // SUCCESS or FAILURE — continue to next agent
            }

            bb.currentAgentOffset = savedOffset;
            ctx.runningAgentIndex[nodeIndex] = 0;
            return NodeState.SUCCESS;
        }
    }
}
