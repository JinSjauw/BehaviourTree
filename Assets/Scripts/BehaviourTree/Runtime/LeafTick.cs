using BehaviourTree.Core;

namespace BehaviourTree.Runtime
{
    /// <summary>
    /// Tick functions for leaf nodes (Action and Condition).
    /// Resolves inputs from the blackboard, executes the method, then writes outputs back.
    /// </summary>
    internal static partial class TickFunctions
    {
        internal static NodeState TickLeaf(int nodeIndex, ref TickContext ctx)
        {
            NodeMethod method = ctx.methodInstances[nodeIndex];
            if (method == null)
            {
                UnityEngine.Debug.LogError($"Method instance not found for node index {nodeIndex}. Returning FAILURE.");
                return NodeState.FAILURE;
            }

            method.ResolveInputsGeneric(ctx.blackBoard);

            NodeState result;
            if (method is ActionMethod action)
                result = action.Execute();
            else if (method is ConditionMethod condition)
                result = condition.Execute();
            else
            {
                result = NodeState.FAILURE;
            }

            method.WriteOutputsGeneric(ctx.blackBoard);
            return result;
        }
    }
}
