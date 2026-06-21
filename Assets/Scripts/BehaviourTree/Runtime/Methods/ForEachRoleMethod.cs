using BehaviourTree.Core;

namespace BehaviourTree.Runtime.Methods
{
    /// <summary>
    /// ForEachRole ticks all children for agents whose TacticalRole matches the target
    /// role read from the squad BB. All three values come from squad-def variables.
    /// Continues past child SUCCESS and FAILURE — only RUNNING pauses the loop.
    /// Resume position tracked via runningAgentIndex (agent) and activeChildIndex (child).
    /// </summary>
    [NodeMethod("ForEachRole", allowedTreeType = AllowedTreeType.Commander)]
    public sealed class ForEachRoleMethod : CompositeMethod
    {
        /// <summary>Baked slot offset of the AgentRoles squad-data variable.
        /// Auto-bound to "AgentRoles" by convention — not visible in the inspector.
        /// After ResolveInputsGeneric the field holds the base value;
        /// bindings[0].bbSlotIndex gives the raw offset for per-agent reads.</summary>
        [SharedVar(IsHidden = true, AutoVariableName = "AgentRoles")]
        public int agentRoleSlot;

        /// <summary>Target role to match. When toggle is OFF: TacticalRole enum dropdown (baked as constant).
        /// When toggle is ON: reads from a squad BB int variable (dynamic per-frame).</summary>
        [SharedVar(isToggleVariable: true, IsRoleDropdown = true)]
        public int targetRoleSlot;

        public override NodeState Execute(int nodeIndex, ref TickContext ctx)
        {
            ref NodeData node = ref ctx.nodeDatas[nodeIndex];
            if (node.firstChildIndex < 0) return NodeState.SUCCESS;

            BlackBoard bb = ctx.blackBoard;
            int savedOffset = bb.currentAgentOffset;

            int roleSlot = GetSlotByName(nameof(agentRoleSlot));
            if (roleSlot < 0) return NodeState.FAILURE;

            int targetRoleInt = targetRoleSlot;

            int agentCount = ctx.agentCount;
            if (agentCount <= 0) return NodeState.FAILURE;

            int agentIndex = ctx.runningAgentIndex[nodeIndex];
            int childIndex = ctx.activeChildIndex[nodeIndex];
            int childCount = node.lastChildIndex - node.firstChildIndex + 1;

            for (; agentIndex < agentCount; agentIndex++)
            {
                object roleBoxed = bb.GetBoxed(roleSlot + agentIndex);
                int roleInt = roleBoxed is int roleVal ? roleVal : 0;
                if (roleInt != targetRoleInt)
                {
                    continue;
                }

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
