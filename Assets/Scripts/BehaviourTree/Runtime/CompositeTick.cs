using BehaviourTree.Core;

namespace BehaviourTree.Runtime
{
    /// <summary>
    /// Tick function for composite nodes.
    /// Dispatches to the CompositeMethod instance stored in methodInstances.
    /// Handles ResolveInputs/WriteOutputs for BB-bound composite fields.
    /// </summary>
    internal static partial class TickFunctions
    {
        internal static NodeState TickComposite(int nodeIndex, ref TickContext ctx)
        {
            NodeMethod method = ctx.methodInstances[nodeIndex];
            if (method is CompositeMethod composite)
            {
                composite.ResolveInputs(ctx.blackBoard);
                NodeState result = composite.Execute(nodeIndex, ref ctx);
                composite.WriteOutputs(ctx.blackBoard);
                return result;
            }

            UnityEngine.Debug.LogError($"Composite method instance not found for node index {nodeIndex}. Returning FAILURE.");
            return NodeState.FAILURE;
        }
    }
}
