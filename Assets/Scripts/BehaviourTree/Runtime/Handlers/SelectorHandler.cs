using System.Diagnostics;
using BehaviourTree.Core;

namespace BehaviourTree.Runtime
{
    public class SelectorHandler : INodeHandler
    {
        public bool Process(EvaluatorContext context)
        {
            EvaluatorFrame frame = context.CurrentFrame;
            ref NodeData node = ref context.CurrentNode;
            int childCount = node.lastChildIndex - node.firstChildIndex + 1;

            // Process the result of the last child that just completed
            if (frame.lastChildStatus != NodeState.NONE)
            {
                if (frame.lastChildStatus == NodeState.SUCCESS)
                {
                    context.PopAndNotifyParent(NodeState.SUCCESS);
                    return false;
                }
                else // FAILURE
                {
                    frame.childIndex++;
                    frame.lastChildStatus = NodeState.NONE;

                    if (frame.childIndex >= childCount)
                    {
                        context.PopAndNotifyParent(NodeState.FAILURE);
                        return false;
                    }
                }
            }

            UnityEngine.Debug.Log("In Selector Handler!");

            // Still evaluating children
            context.CurrentNodeIndex = frame.nodeIndex;
            context.MarkCurrentNodeRunning();

            int nextChild = node.firstChildIndex + frame.childIndex;
            context.PushChild(nextChild);
            return true;
        }
    }
}