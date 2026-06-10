using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Runtime
{
    public class Inverter : BehaviourTree.Core.DecoratorMethod
    {
        public bool alwaysFailure;
        public bool alwaysSuccess;

        public override NodeState Execute(NodeState childResult)
        {
            if (alwaysFailure) return NodeState.FAILURE;
            if (alwaysSuccess) return NodeState.SUCCESS;

            return childResult switch
            {
                NodeState.SUCCESS => NodeState.FAILURE,
                NodeState.FAILURE  => NodeState.SUCCESS,
                _                  => childResult
            };
        }
    }

    public class Repeater : BehaviourTree.Core.DecoratorMethod
    {
        public int targetCount;
        private int currentCount;

        public override NodeState Execute(NodeState childResult)
        {
            if (childResult == NodeState.RUNNING)
                return NodeState.RUNNING;

            if (childResult == NodeState.SUCCESS && currentCount < targetCount - 1)
            {
                currentCount++;
                return NodeState.RUNNING;
            }

            currentCount = 0;
            return childResult;
        }
    }
}
