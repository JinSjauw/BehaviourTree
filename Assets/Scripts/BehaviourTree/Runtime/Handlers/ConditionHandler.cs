using BehaviourTree.Core;

namespace BehaviourTree.Runtime
{
    public class ConditionHandler : INodeHandler
    {
        public bool Process(EvaluatorContext context)
        {
            ref NodeData node = ref context.CurrentNode;
            int nodeIndex = context.CurrentFrame.nodeIndex;
            NodeState result = context.EvaluateLeaf(nodeIndex, ref node);

            // Conditions never return RUNNING; they succeed or fail immediately.
            context.PopAndNotifyParent(result);
            return false;
        }
    }
}