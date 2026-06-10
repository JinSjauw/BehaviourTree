using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Runtime
{
    public class WaitSeconds : ActionMethod
    {
        public float duration;
        private float elapsed;

        public override NodeState Execute()
        {
            elapsed += Time.deltaTime;
            if (elapsed >= duration)
            {
                elapsed = 0f;
                return NodeState.SUCCESS;
            }
            return NodeState.RUNNING;
        }
    }

    public class Cooldown : ConditionMethod
    {
        public float duration;
        [SharedVar] public float remaining;
        public bool useCustomTick;
        public float customTickValue;

        public override NodeState Execute()
        {
            float tickAmount = useCustomTick ? customTickValue : Time.deltaTime;

            if (remaining > 0f)
            {
                remaining = Mathf.Max(0f, remaining - tickAmount);
                return NodeState.FAILURE;
            }

            remaining = duration;
            return NodeState.SUCCESS;
        }
    }
}
