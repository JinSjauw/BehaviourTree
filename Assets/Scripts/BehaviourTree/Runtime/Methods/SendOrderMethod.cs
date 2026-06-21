using BehaviourTree.Core;

namespace BehaviourTree.Runtime.Methods
{
    /// <summary>
    /// Writes an order index to the AgentOrders squad-data variable at the
    /// current agent offset (set by enclosing ForEachRole / ForEachAgent).
    /// Agent trees receive the value through the binding bridge as AgentReceivedOrder.
    /// </summary>
    [NodeMethod("SendOrder", allowedTreeType = AllowedTreeType.Commander)]
    public sealed class SendOrderMethod : ActionMethod
    {
        /// <summary>Baked slot offset of the AgentOrders squad-data variable.
        /// Auto-bound to "AgentOrders" by convention — not visible in the inspector.</summary>
        [SharedVar(IsHidden = true, AutoVariableName = "AgentOrders")]
        public int ordersSlot;

        /// <summary>Order to send. Toggle OFF: order search dropdown picks an order
        /// name stored as stringValue, resolved to index at bake time.
        /// Toggle ON: reads from a BB int variable (dynamic per-frame).</summary>
        [SharedVar(isToggleVariable: true, IsOrderDropdown = true)]
        public int orderValue;

        public override NodeState Execute()
        {
            ordersSlot = orderValue;
            return NodeState.SUCCESS;
        }
    }
}
