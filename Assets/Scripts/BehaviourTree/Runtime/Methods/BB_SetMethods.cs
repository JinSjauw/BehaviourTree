using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Runtime
{
    public class BB_SetInt : ActionMethod
    {
        [SharedVar]        public int target;
        [SharedVar(true)]  public int value;

        public override NodeState Execute()
        {
            target = value;
            return NodeState.SUCCESS;
        }
    }

    public class BB_SetFloat : ActionMethod
    {
        [SharedVar]        public float target;
        [SharedVar(true)]  public float value;

        public override NodeState Execute()
        {
            target = value;
            return NodeState.SUCCESS;
        }
    }

    public class BB_SetBool : ActionMethod
    {
        [SharedVar]        public bool target;
        [SharedVar(true)]  public bool value;

        public override NodeState Execute()
        {
            target = value;
            return NodeState.SUCCESS;
        }
    }

    public class BB_SetVector2 : ActionMethod
    {
        [SharedVar] public Vector2 target;
        [SharedVar] public Vector2 value;

        public override NodeState Execute()
        {
            target = value;
            return NodeState.SUCCESS;
        }
    }

    public class BB_SetVector3 : ActionMethod
    {
        [SharedVar] public Vector3 target;
        [SharedVar] public Vector3 value;

        public override NodeState Execute()
        {
            target = value;
            return NodeState.SUCCESS;
        }
    }

    public class BB_SetGameObject : ActionMethod
    {
        [SharedVar] public GameObject target;
        [SharedVar] public GameObject value;

        public override NodeState Execute()
        {
            target = value;
            return NodeState.SUCCESS;
        }
    }

    public class BB_SetTransform : ActionMethod
    {
        [SharedVar] public Transform target;
        [SharedVar] public Transform value;

        public override NodeState Execute()
        {
            target = value;
            return NodeState.SUCCESS;
        }
    }

    public class BB_SetVector2FromTransform : ActionMethod
    {
        [SharedVar] public Vector2 target;
        [SharedVar] public Transform source;

        public override NodeState Execute()
        {
            if (source == null) return NodeState.FAILURE;
            target = source.position;
            return NodeState.SUCCESS;
        }
    }

    public class BB_SetVector3FromTransform : ActionMethod
    {
        [SharedVar] public Vector3 target;
        [SharedVar] public Transform source;

        public override NodeState Execute()
        {
            if (source == null) return NodeState.FAILURE;
            target = source.position;
            return NodeState.SUCCESS;
        }
    }

    // ── Clear ──────────────────────────────────────────────────────

    public class BB_ClearInt : ActionMethod
    {
        [SharedVar] public int target;
        public override NodeState Execute() { target = 0; return NodeState.SUCCESS; }
    }

    public class BB_ClearFloat : ActionMethod
    {
        [SharedVar] public float target;
        public override NodeState Execute() { target = 0f; return NodeState.SUCCESS; }
    }

    public class BB_ClearBool : ActionMethod
    {
        [SharedVar] public bool target;
        public override NodeState Execute() { target = false; return NodeState.SUCCESS; }
    }

    public class BB_ClearVector2 : ActionMethod
    {
        [SharedVar] public Vector2 target;
        public override NodeState Execute() { target = Vector2.zero; return NodeState.SUCCESS; }
    }

    public class BB_ClearVector3 : ActionMethod
    {
        [SharedVar] public Vector3 target;
        public override NodeState Execute() { target = Vector3.zero; return NodeState.SUCCESS; }
    }

    public class BB_ClearGameObject : ActionMethod
    {
        [SharedVar] public GameObject target;
        public override NodeState Execute() { target = null; return NodeState.SUCCESS; }
    }

    public class BB_ClearTransform : ActionMethod
    {
        [SharedVar] public Transform target;
        public override NodeState Execute() { target = null; return NodeState.SUCCESS; }
    }

    // ── Toggle ─────────────────────────────────────────────────────

    public class BB_ToggleBool : ActionMethod
    {
        [SharedVar] public bool target;

        public override NodeState Execute()
        {
            target = !target;
            return NodeState.SUCCESS;
        }
    }
}
