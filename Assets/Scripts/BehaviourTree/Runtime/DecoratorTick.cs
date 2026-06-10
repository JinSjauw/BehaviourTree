using BehaviourTree.Core;

namespace BehaviourTree.Runtime
{
    /// <summary>
    /// Tick function for Decorator nodes.
    /// Ticks the child, passes the result through the decorator method,
    /// and returns the transformed result.
    /// </summary>
    internal static partial class TickFunctions
    {
        internal static NodeState TickDecorator(int nodeIndex, ref TickContext ctx)
        {
            ref NodeData node = ref ctx.nodeDatas[nodeIndex];
            int childIndex = node.firstChildIndex;

            if (childIndex < 0) return NodeState.FAILURE;

            NodeState childResult = TickDispatcher.TickNode(childIndex, ref ctx);

            NodeMethod instance = ctx.methodInstances[nodeIndex];
            if (instance is DecoratorMethod decoratorInstance)
            {
                decoratorInstance.ResolveInputs(ctx.blackBoard);
                NodeState transformed = decoratorInstance.Execute(childResult);
                decoratorInstance.WriteOutputs(ctx.blackBoard);
                return transformed;
            }

            // No decorator method registered — pass-through
            return childResult;
        }
    }
}
