using System.Collections.Generic;
using BehaviourTree.Core;

namespace BehaviourTree.Runtime
{
    /// <summary>
    /// Runtime state for a single Parallel node's children.
    /// </summary>
    internal struct ParallelChildState
    {
        public int nodeIndex;
        public NodeState result;
    }

    /// <summary>
    /// Immutable per-frame snapshot for tick evaluation.
    /// All mutable arrays (nodeStates, activeChildIndex, parallelStates) are
    /// stored on TreeEvaluator and passed by ref through this struct.
    /// </summary>
    internal struct TickContext
    {
        public NodeData[] nodeDatas;
        public NodeMethod[] methodInstances;
        public NodeState[] nodeStates;
        public int[] activeChildIndex;
        public Dictionary<int, ParallelChildState[]> parallelStates;
        public BlackBoard blackBoard;
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
            handlers = new TickHandler[9];
            handlers[(int)BehaviourNodeType.ACTION] = TickFunctions.TickLeaf;
            handlers[(int)BehaviourNodeType.CONDITION] = TickFunctions.TickLeaf;
            handlers[(int)BehaviourNodeType.SEQUENCE] = TickFunctions.TickSequence;
            handlers[(int)BehaviourNodeType.SELECTOR] = TickFunctions.TickSelector;
            handlers[(int)BehaviourNodeType.DECORATOR] = TickFunctions.TickDecorator;
            handlers[(int)BehaviourNodeType.PARALLEL] = TickFunctions.TickParallel;
            handlers[(int)BehaviourNodeType.PRIORITY] = TickFunctions.TickPriority;
            handlers[(int)BehaviourNodeType.SUBTREE] = TickFunctions.TickSubtree;
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
            if (handler != null) return handler(nodeIndex, ref ctx);
            
            return NodeState.FAILURE;
        }
    }
}
