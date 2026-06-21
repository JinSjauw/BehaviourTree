using BehaviourTree.Core;

namespace BehaviourTree.Runtime.Methods
{
    /// <summary>
    /// SelectAgent ticks children for a single agent specified by _targetAgentID.
    /// Sets blackBoard.currentAgentOffset = targetAgentID so leaves read/write
    /// the correct per-agent slot. Invalid agentID → FAILURE without ticking children.
    /// </summary>
    [NodeMethod("SelectAgent", allowedTreeType = AllowedTreeType.Commander)]
    public sealed class SelectAgentMethod : CompositeMethod
    {
        /// <summary>Agent ID to select, read from _targetAgentID BB variable.</summary>
        [SharedVar] public int targetAgentID;

        public override NodeState Execute(int nodeIndex, ref TickContext ctx)
        {
            ref NodeData node = ref ctx.nodeDatas[nodeIndex];
            if (node.firstChildIndex < 0) return NodeState.SUCCESS;

            int agentID = targetAgentID;
            if (agentID < 0 || agentID >= ctx.agentCount)
                return NodeState.FAILURE;

            BlackBoard bb = ctx.blackBoard;
            int savedOffset = bb.currentAgentOffset;
            bb.currentAgentOffset = agentID;

            NodeState result = TickDispatcher.TickNode(node.firstChildIndex, ref ctx);

            if (result == NodeState.RUNNING)
            {
                // runningAgentIndex stores the selected agent ID for resume context
                ctx.runningAgentIndex[nodeIndex] = agentID;
                bb.currentAgentOffset = savedOffset;
                return NodeState.RUNNING;
            }

            bb.currentAgentOffset = savedOffset;
            ctx.runningAgentIndex[nodeIndex] = 0;
            return result;
        }
    }
}
