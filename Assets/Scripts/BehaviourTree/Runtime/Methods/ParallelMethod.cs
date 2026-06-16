using BehaviourTree.Core;

namespace BehaviourTree.Runtime.Methods
{
    /// <summary>
    /// Parallel: all children are ticked every frame independently.
    /// Any FAILURE = overall FAILURE. All SUCCESS = overall SUCCESS.
    /// Any RUNNING = overall RUNNING.
    /// Child completion state is stored on the instance (one per node).
    /// </summary>
    [NodeMethod("PARALLEL")]
    public sealed class ParallelMethod : CompositeMethod
    {

        private ParallelChildState[] children;

        public override NodeState Execute(int nodeIndex, ref TickContext ctx)
        {
            ref NodeData node = ref ctx.nodeDatas[nodeIndex];
            if (node.firstChildIndex < 0)
                return NodeState.SUCCESS;

            // ── Conditional abort check ──
            int abortResult = TickFunctions.CheckConditionalAbort(nodeIndex, ref ctx);
            if (abortResult != ctx.activeChildIndex[nodeIndex])
                children = null; // Reset per-child state on abort

            int childCount = node.lastChildIndex - node.firstChildIndex + 1;

            if (children == null)
            {
                children = new ParallelChildState[childCount];
                for (int i = 0; i < childCount; i++)
                    children[i] = new ParallelChildState { result = NodeState.NONE };
            }

            // Tick all children (not just leaves — supports nested composites)
            for (int i = 0; i < childCount; i++)
            {
                ref ParallelChildState child = ref children[i];
                if (child.result == NodeState.SUCCESS || child.result == NodeState.FAILURE)
                    continue;

                int childIndex = node.firstChildIndex + i;
                NodeState result = TickDispatcher.TickNode(childIndex, ref ctx);
                child.result = result;
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
                children = null;
                return NodeState.FAILURE;
            }

            if (anyRunning) return NodeState.RUNNING;

            children = null;
            return NodeState.SUCCESS;
        }
    }
}
