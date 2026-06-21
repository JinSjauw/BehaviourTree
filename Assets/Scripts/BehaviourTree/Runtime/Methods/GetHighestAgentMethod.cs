using BehaviourTree.Core;

namespace BehaviourTree.Runtime.Methods
{
    /// <summary>
    /// Scans all agents for the highest value of a squad-data variable,
    /// selects that agent, then ticks children. On RUNNING, persists the
    /// selected agent so it doesn't change mid-execution.
    /// Returns FAILURE if no agent is found.
    /// </summary>
    [NodeMethod("GetHighestAgent", allowedTreeType = AllowedTreeType.Commander)]
    public sealed class GetHighestAgentMethod : CompositeMethod
    {
        [SharedVar] public int targetAgentIDSlot;
        [SharedVar] public int variableValueSlot;

        public override NodeState Execute(int nodeIndex, ref TickContext ctx)
        {
            ref NodeData node = ref ctx.nodeDatas[nodeIndex];
            if (node.firstChildIndex < 0) return NodeState.SUCCESS;

            BlackBoard bb = ctx.blackBoard;
            int savedOffset = bb.currentAgentOffset;

            // Resume from saved state if children were RUNNING last tick
            if (ctx.runningAgentIndex[nodeIndex] != 0)
            {
                int savedAgentIndex = ctx.runningAgentIndex[nodeIndex] - 1;
                bb.currentAgentOffset = savedAgentIndex;
                NodeState resumeResult = TickDispatcher.TickNode(node.firstChildIndex, ref ctx);
                if (resumeResult == NodeState.RUNNING)
                {
                    bb.currentAgentOffset = savedOffset;
                    return NodeState.RUNNING;
                }

                bb.currentAgentOffset = savedOffset;
                ctx.runningAgentIndex[nodeIndex] = 0;
                return resumeResult;
            }

            int count = ctx.agentCount;
            int rawVariableSlot = GetSlotByName(nameof(variableValueSlot));
            int rawTargetSlot = GetSlotByName(nameof(targetAgentIDSlot));

            if (rawVariableSlot < 0 || rawTargetSlot < 0 || count <= 0)
                return NodeState.FAILURE;

            int bestAgentIndex = -1;
            float highestValue = float.MinValue;

            for (int agentIndex = 0; agentIndex < count; agentIndex++)
            {
                object boxed = bb.GetBoxed(rawVariableSlot + agentIndex);
                float value = boxed is float floatValue ? floatValue : boxed is int intValue ? intValue : float.MinValue;
                if (value > highestValue)
                {
                    highestValue = value;
                    bestAgentIndex = agentIndex;
                }
            }

            if (bestAgentIndex < 0)
                return NodeState.FAILURE;

            bb.SetBoxed(rawTargetSlot, bestAgentIndex);

            bb.currentAgentOffset = bestAgentIndex;
            NodeState result = TickDispatcher.TickNode(node.firstChildIndex, ref ctx);

            if (result == NodeState.RUNNING)
            {
                ctx.runningAgentIndex[nodeIndex] = bestAgentIndex + 1;
                bb.currentAgentOffset = savedOffset;
                return NodeState.RUNNING;
            }

            bb.currentAgentOffset = savedOffset;
            ctx.runningAgentIndex[nodeIndex] = 0;
            return result;
        }
    }
}
