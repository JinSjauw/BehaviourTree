using BehaviourTree.Core;

namespace BehaviourTree.Runtime
{
    public class ActionHandler : INodeHandler
    {
        public bool Process(EvaluatorContext context)
        {
            ref NodeData node = ref context.CurrentNode;
            int nodeIndex = context.CurrentFrame.nodeIndex;
            NodeState result = context.EvaluateLeaf(nodeIndex, ref node);

            if (result == NodeState.RUNNING)
            {
                context.NodeStates[context.CurrentFrame.nodeIndex] = NodeState.RUNNING;
                context.CurrentNodeIndex = context.CurrentFrame.nodeIndex;
                context.ShouldBreak = true;
                context.SetStackRunning();
                return false;
            }
            else
            {
                context.PopAndNotifyParent(result);
                return false;
            }
        }
    }
}