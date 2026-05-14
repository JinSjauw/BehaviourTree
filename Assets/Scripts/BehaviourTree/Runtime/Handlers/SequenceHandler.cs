using BehaviourTree.Core;

namespace BehaviourTree.Runtime
{
    public class SequenceHandler : CompositeHandler
    {
        protected override bool IsStopCondition(NodeState state) =>
            state == NodeState.FAILURE;

        protected override NodeState StopState => NodeState.FAILURE;
        protected override NodeState ExhaustedState => NodeState.SUCCESS;
    }
}