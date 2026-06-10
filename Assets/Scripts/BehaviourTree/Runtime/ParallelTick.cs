using System.Collections.Generic;
using BehaviourTree.Core;

namespace BehaviourTree.Runtime
{
    /// <summary>
    /// Tick function for Parallel nodes.
    /// All children are ticked every frame independently.
    /// Any FAILURE = overall FAILURE. All SUCCESS = overall SUCCESS.
    /// Any RUNNING = overall RUNNING.
    /// </summary>
    internal static partial class TickFunctions
    {
        internal static NodeState TickParallel(int nodeIndex, ref TickContext ctx)
        {
            ref NodeData node = ref ctx.nodeDatas[nodeIndex];
            if (node.firstChildIndex < 0)
                return NodeState.SUCCESS;

            int childCount = node.lastChildIndex - node.firstChildIndex + 1;

            Dictionary<int, ParallelChildState[]> states = ctx.parallelStates;

            if (!states.TryGetValue(nodeIndex, out ParallelChildState[] children))
            {
                children = new ParallelChildState[childCount];
                for (int i = 0; i < childCount; i++)
                    children[i] = new ParallelChildState { nodeIndex = node.firstChildIndex + i, result = NodeState.NONE };
                states[nodeIndex] = children;
            }

            // Tick all children (not just leaves — supports nested composites)
            for (int i = 0; i < childCount; i++)
            {
                ref ParallelChildState child = ref children[i];
                if (child.result == NodeState.SUCCESS || child.result == NodeState.FAILURE)
                    continue;

                NodeState result = TickDispatcher.TickNode(child.nodeIndex, ref ctx);
                child.result = result;
                ctx.nodeStates[child.nodeIndex] = result;
            }

            bool anyRunning = false;
            bool anyFailure = false;

            for (int i = 0; i < childCount; i++)
            {
                NodeState state = children[i].result;
                if (state == NodeState.RUNNING) anyRunning = true;
                if (state == NodeState.FAILURE) anyFailure = true;
            }

            if (anyFailure)
            {
                states.Remove(nodeIndex);
                return NodeState.FAILURE;
            }

            if (anyRunning)
                return NodeState.RUNNING;

            states.Remove(nodeIndex);
            return NodeState.SUCCESS;
        }
    }
}
