using BehaviourTree.Core;

namespace BehaviourTree.Runtime
{
    public class SubtreeHandler : INodeHandler
    {
        public bool Process(EvaluatorContext context)
        {
            EvaluatorFrame frame = context.CurrentFrame;
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

            context.PopAndNotifyParent(frame.lastChildStatus);
            return false;
        }
    }
}
