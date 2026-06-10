using System;
using System.Diagnostics;
using BehaviourTree.Core;

namespace BehaviourTree.Runtime
{
    public class DecoratorHandler : INodeHandler
    {
        public bool Process(EvaluatorContext context)
        {
            ref EvaluatorFrame frame = ref context.CurrentFrame;
            ref NodeData node = ref context.CurrentNode;
            int childIndex = node.firstChildIndex;

            if (childIndex < 0)
            {
                context.PopAndNotifyParent(NodeState.FAILURE);
                return false;
            }

            if (frame.lastChildStatus == NodeState.NONE)
            {
                context.MarkCurrentNodeRunning();
                context.CurrentNodeIndex = frame.nodeIndex;
                context.PushChild(childIndex);
                return true;
            }

            // Child has returned a result — run the decorator method
            NodeMethod instance = context.GetMethodInstance(frame.nodeIndex);
            if (instance is BehaviourTree.Core.DecoratorMethod decoratorInstance)
            {
                decoratorInstance.ResolveInputs(context.BlackBoard);
                NodeState transformed = decoratorInstance.Execute(frame.lastChildStatus);
                decoratorInstance.WriteOutputs(context.BlackBoard);

                if (transformed == NodeState.RUNNING)
                {
                    frame.lastChildStatus = NodeState.NONE;
                    context.PushChild(childIndex);
                    return true;
                }

                context.PopAndNotifyParent(transformed);
                return false;
            }

            // No method registered — pass-through
            context.PopAndNotifyParent(frame.lastChildStatus);
            return false;
        }
    }
}