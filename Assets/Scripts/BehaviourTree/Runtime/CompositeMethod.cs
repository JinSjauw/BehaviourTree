using BehaviourTree.Core;

namespace BehaviourTree.Runtime
{
    /// <summary>
    /// Base class for composite nodes (Sequence, Selector, Priority, Parallel).
    /// Receives the full TickContext to tick children and manage activeChildIndex.
    /// </summary>
    public abstract class CompositeMethod : NodeMethod
    {
        /// <summary>
        /// Tick this composite node. The implementation ticks children via
        /// TickDispatcher.TickNode(), reads/writes activeChildIndex from ctx,
        /// and returns the overall result.
        /// </summary>
        public abstract NodeState Execute(int nodeIndex, ref TickContext ctx);
    }
}
