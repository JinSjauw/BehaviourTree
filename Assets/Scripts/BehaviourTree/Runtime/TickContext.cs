using BehaviourTree.Core;

namespace BehaviourTree.Runtime
{
    /// <summary>
    /// Per-child completion state for Parallel nodes. Tracks
    /// whether each child has finished (SUCCESS/FAILURE) or is still running.
    /// </summary>
    internal struct ParallelChildState
    {
        public NodeState result;
    }

    /// <summary>
    /// Per-frame snapshot for tick evaluation. Mutable arrays (nodeStates,
    /// activeChildIndex) are stored on TreeEvaluator and passed by ref.
    /// nodeStates is populated by TickDispatcher.TickNode() as a side-effect;
    /// tick functions do not read it.
    /// </summary>
    public struct TickContext
    {
        public NodeData[] nodeDatas;
        public NodeMethod[] methodInstances;
        public NodeState[] nodeStates;
        public int[] activeChildIndex;
        public BlackBoard blackBoard;

        /// <summary>
        /// Per-node last condition result for conditional abort transition detection.
        /// Indexed the same as nodeDatas. false = condition was not met last time it was checked.
        /// </summary>
        public bool[] lastConditionResult;
    }

    /// <summary>
    /// Signature for a static tick function. Each node type has one.
    /// </summary>
    internal delegate NodeState TickHandler(int nodeIndex, ref TickContext ctx);

    /// <summary>
    /// Dispatches TickNode calls to the appropriate static tick function
    /// based on node type. Uses a fixed-size delegate table for speed.
    /// </summary>
    internal static class TickDispatcher
    {
        private static readonly TickHandler[] handlers;

        static TickDispatcher()
        {
            handlers = new TickHandler[6];
            handlers[(int)BehaviourNodeType.ACTION] = TickFunctions.TickLeaf;
            handlers[(int)BehaviourNodeType.CONDITION] = TickFunctions.TickLeaf;
            handlers[(int)BehaviourNodeType.DECORATOR] = TickFunctions.TickDecorator;
            handlers[(int)BehaviourNodeType.SUBTREE] = TickFunctions.TickSubtree;
            // COMPOSITE, ROOT — left null,
            // TickNode falls through to TickComposite via methodInstances.
        }

        /// <summary>
        /// Ticks the node at nodeIndex, returning its result.
        /// Dispatches to the registered tick handler for the node's type.
        /// </summary>
        public static NodeState TickNode(int nodeIndex, ref TickContext ctx)
        {
            if(nodeIndex < 0 || nodeIndex >= ctx.nodeDatas.Length) return NodeState.FAILURE;
            BehaviourNodeType nodeType = ctx.nodeDatas[nodeIndex].nodeType;
            TickHandler handler = handlers[(int)nodeType];
            NodeState result;
            if (handler != null)
            {
                result = handler(nodeIndex, ref ctx);
            }
            else
            {
                result = TickFunctions.TickComposite(nodeIndex, ref ctx);
            }
            ctx.nodeStates[nodeIndex] = result;
            return result;
        }
    }
}
