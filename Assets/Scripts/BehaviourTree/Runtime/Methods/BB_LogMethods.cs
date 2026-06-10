using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Runtime
{
    public class BB_LogInt : ActionMethod
    {
        [SharedVar] public int value;
        private string methodName;
        public override NodeState Execute() { Debug.Log($"{value}"); return NodeState.SUCCESS; }
    }

    public class BB_LogFloat : ActionMethod
    {
        [SharedVar] public float value;
        public override NodeState Execute() { Debug.Log(value); return NodeState.SUCCESS; }
    }

    public class BB_LogBool : ActionMethod
    {
        [SharedVar] public bool value;
        public override NodeState Execute() { Debug.Log(value); return NodeState.SUCCESS; }
    }

    public class BB_LogVector2 : ActionMethod
    {
        [SharedVar] public Vector2 value;
        public override NodeState Execute() { Debug.Log(value); return NodeState.SUCCESS; }
    }

    public class BB_LogVector3 : ActionMethod
    {
        [SharedVar] public Vector3 value;
        public override NodeState Execute() { Debug.Log(value); return NodeState.SUCCESS; }
    }

    public class BB_LogGameObject : ActionMethod
    {
        [SharedVar] public GameObject value;
        public override NodeState Execute() { Debug.Log(value != null ? value.name : "null"); return NodeState.SUCCESS; }
    }

    public class BB_LogTransform : ActionMethod
    {
        [SharedVar] public Transform value;
        public override NodeState Execute() { Debug.Log(value != null ? value.name : "null"); return NodeState.SUCCESS; }
    }

    public class BB_LogArrayInt : ActionMethod
    {
        [SharedVar] public int array;

        public override NodeState Execute()
        {
            // Stride is packed as a constant in the FieldData following this field.
            // Access it via the BB directly since it's a framework detail.
            Debug.Log($"[BB_LogArrayInt] Array value: {array}");
            return NodeState.SUCCESS;
        }
    }
}
