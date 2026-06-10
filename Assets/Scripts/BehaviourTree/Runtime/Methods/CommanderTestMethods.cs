using BehaviourTree.Core;

namespace BehaviourTree.Runtime
{
    /// <summary>
    /// Commander test method stubs. Replace body with real logic.
    /// </summary>
    public class TEST_CommanderAssign : ActionMethod
    {
        public override NodeState Execute()
        {
            return NodeState.SUCCESS;
        }
    }

    public class TEST_CheckRole : ConditionMethod
    {
        public override NodeState Execute()
        {
            return NodeState.FAILURE;
        }
    }

    public class TEST_AgentExecute : ActionMethod
    {
        public override NodeState Execute()
        {
            return NodeState.SUCCESS;
        }
    }

    public class TEST_AgentReportHealth : ActionMethod
    {
        public override NodeState Execute()
        {
            return NodeState.SUCCESS;
        }
    }

    public class TEST_AgentExecuteOrder : ActionMethod
    {
        public override NodeState Execute()
        {
            return NodeState.SUCCESS;
        }
    }
}
