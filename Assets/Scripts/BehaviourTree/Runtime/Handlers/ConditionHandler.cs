using BehaviourTree.Core;

namespace BehaviourTree.Runtime
{
    public class ConditionHandler : INodeHandler
    {
        public bool Process(EvaluatorContext context)
        {
            ref NodeData node = ref context.CurrentNode;
            NodeState result = context.EvaluateLeaf(ref node);

            // Conditions never return RUNNING; they succeed or fail immediately.
            context.PopAndNotifyParent(result);
            return false;
        }
    }
}