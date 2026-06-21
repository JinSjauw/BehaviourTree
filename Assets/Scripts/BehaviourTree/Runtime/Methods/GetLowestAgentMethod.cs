using BehaviourTree.Core;

namespace BehaviourTree.Runtime.Methods
{
    /// <summary>
    /// Scans all agents for the lowest value of a squad-data variable,
    /// selects that agent, then ticks children. On RUNNING, persists the
    /// selected agent so it doesn't change mid-execution.
    /// Returns FAILURE if no agent is found.
    /// </summary>
    [NodeMethod("GetLowestAgent", allowedTreeType = AllowedTreeType.Commander)]
    public sealed class GetLowestAgentMethod : CompositeMethod
    {
        /// <summary>BB slot for _targetAgentID (stride=1, transient). Written with selected agent ID.</summary>
        [SharedVar] public int targetAgentIDSlot;

        /// <summary>BB slot for the squad-data variable to compare (stride > 1).</summary>
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
                int savedAgentIndex = ctx.runningAgentIndex[nodeIndex] - 1; // stored as index+1
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

            // Scan for the agent with the lowest variable value
            int count = ctx.agentCount;
            int rawVariableSlot = GetSlotByName(nameof(variableValueSlot));
            int rawTargetSlot = GetSlotByName(nameof(targetAgentIDSlot));

            if (rawVariableSlot < 0 || rawTargetSlot < 0 || count <= 0)
                return NodeState.FAILURE;

            int bestAgentIndex = -1;
            float lowestValue = float.MaxValue;

            for (int agentIndex = 0; agentIndex < count; agentIndex++)
            {
                object boxed = bb.GetBoxed(rawVariableSlot + agentIndex);
                float value = ConvertToFloat(boxed);
                if (value < lowestValue)
                {
                    lowestValue = value;
                    bestAgentIndex = agentIndex;
                }
            }

            if (bestAgentIndex < 0)
                return NodeState.FAILURE;

            // Write selected agent ID to the transient _targetAgentID slot
            bb.SetBoxed(rawTargetSlot, bestAgentIndex);

            bb.currentAgentOffset = bestAgentIndex;
            NodeState result = TickDispatcher.TickNode(node.firstChildIndex, ref ctx);

            if (result == NodeState.RUNNING)
            {
                ctx.runningAgentIndex[nodeIndex] = bestAgentIndex + 1; // store as index+1
                bb.currentAgentOffset = savedOffset;
                return NodeState.RUNNING;
            }

            bb.currentAgentOffset = savedOffset;
            ctx.runningAgentIndex[nodeIndex] = 0;
            return result;
        }

        private static float ConvertToFloat(object value)
        {
            if (value is float floatValue) return floatValue;
            if (value is int intValue) return intValue;
            if (value is double doubleValue) return (float)doubleValue;
            return float.MaxValue;
        }
    }
}
