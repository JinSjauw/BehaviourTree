using BehaviourTree.Core;

namespace BehaviourTree.Runtime
{
    public abstract class CompositeHandler : INodeHandler
    {
        protected abstract bool IsStopCondition(NodeState state);
        protected abstract NodeState StopState { get; }
        protected abstract NodeState ExhaustedState { get; }

        protected virtual void PushNextChildren(EvaluatorContext context, EvaluatorFrame frame, ref NodeData node, int childCount)
        {
            int nextChild = node.firstChildIndex + frame.childIndex;
            context.PushChild(nextChild);
        }

        public virtual bool Process(EvaluatorContext context)
        {
            EvaluatorFrame frame = context.CurrentFrame;
            ref NodeData node = ref context.CurrentNode;
            int childCount = node.lastChildIndex - node.firstChildIndex + 1;

            if (frame.lastChildStatus != NodeState.NONE)
            {
                if (IsStopCondition(frame.lastChildStatus))
                {
                    context.PopAndNotifyParent(StopState);
                    return false;
                }

                frame.childIndex++;
                frame.lastChildStatus = NodeState.NONE;

                if (frame.childIndex >= childCount)
                {
                    context.PopAndNotifyParent(ExhaustedState);
                    return false;
                }
            }

            context.CurrentNodeIndex = frame.nodeIndex;
            context.MarkCurrentNodeRunning();

            PushNextChildren(context, frame, ref node, childCount);
            return true;
        }
    }
}
