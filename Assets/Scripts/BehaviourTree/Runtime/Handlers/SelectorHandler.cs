using BehaviourTree.Core;

namespace BehaviourTree.Runtime
{
    public class SelectorHandler : CompositeHandler
    {
        protected override bool IsStopCondition(NodeState state) =>
            state == NodeState.SUCCESS;

        protected override NodeState StopState => NodeState.SUCCESS;
        protected override NodeState ExhaustedState => NodeState.FAILURE;
    }
}