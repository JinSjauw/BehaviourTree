using System;
using System.Diagnostics;
using BehaviourTree.Core;

namespace BehaviourTree.Runtime
{
    public class DecoratorHandler : INodeHandler
    {
        public bool Process(EvaluatorContext context)
        {
            EvaluatorFrame frame = context.CurrentFrame;
            ref NodeData node = ref context.CurrentNode;
            int childIndex = node.firstChildIndex;

            // First entry — push the single child
            if (frame.lastChildStatus == NodeState.NONE)
            {
                context.MarkCurrentNodeRunning();
                context.CurrentNodeIndex = frame.nodeIndex;
                context.PushChild(childIndex);
                return true;
            }

            // Child has returned a result — run the decorator method
            var method = MethodRegistry.GetDecoratorMethod(node.methodID);
            if (method == null)
            {
                // No method registered — pass-through
                context.PopAndNotifyParent(frame.lastChildStatus);
                return false;
            }

            ReadOnlySpan<FieldData> fields = context.GetNodeFields(node);
            NodeState transformed = method.Invoke(frame.lastChildStatus, context.BlackBoard, fields);

            if (transformed == NodeState.RUNNING)
            {
                // Repeater / Loop pattern: re-push child
                frame.lastChildStatus = NodeState.NONE;
                context.PushChild(childIndex);
                return true;
            }

            context.PopAndNotifyParent(transformed);
            return false;
        }
    }
}