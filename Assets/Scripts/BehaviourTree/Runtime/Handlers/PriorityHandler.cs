using BehaviourTree.Core;

namespace BehaviourTree.Runtime
{
    public class PriorityHandler : INodeHandler
    {
        public bool Process(EvaluatorContext context)
        {
            ref EvaluatorFrame frame = ref context.CurrentFrame;
            ref NodeData node = ref context.CurrentNode;

            if (node.firstChildIndex < 0)
            {
                context.PopAndNotifyParent(NodeState.FAILURE);
                return false;
            }

            int childCount = node.lastChildIndex - node.firstChildIndex + 1;

            context.MarkCurrentNodeRunning();
            context.CurrentNodeIndex = frame.nodeIndex;

            if (frame.lastChildStatus != NodeState.NONE)
            {
                NodeState childResult = frame.lastChildStatus;
                frame.lastChildStatus = NodeState.NONE;

                if (childResult == NodeState.SUCCESS)
                {
                    context.PopAndNotifyParent(NodeState.SUCCESS);
                    return false;
                }

                if (childResult == NodeState.FAILURE)
                {
                    frame.childIndex++;
                }
            }

            if (frame.childIndex < childCount)
            {
                int childNodeIndex = node.firstChildIndex + frame.childIndex;
                context.PushChild(childNodeIndex);
                return true;
            }

            context.PopAndNotifyParent(NodeState.FAILURE);
            return false;
        }
    }
}
