using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Runtime
{
    public class BB_CheckBool : ConditionMethod
    {
        [SharedVar] public bool value;
        public BoolCheckOp operation;

        public override NodeState Execute()
        {
            bool result = operation switch
            {
                BoolCheckOp.IsTrue  => value,
                BoolCheckOp.IsFalse => !value,
                _ => false
            };
            return result ? NodeState.SUCCESS : NodeState.FAILURE;
        }
    }

    public class BB_CheckGameObject : ConditionMethod
    {
        [SharedVar] public GameObject value;
        public ObjectCheckOp operation;

        public override NodeState Execute()
        {
            bool result = operation switch
            {
                ObjectCheckOp.IsNull     => value == null,
                ObjectCheckOp.IsNotNull  => value != null,
                ObjectCheckOp.IsActive   => value != null && value.activeInHierarchy,
                ObjectCheckOp.IsInactive => value == null || !value.activeInHierarchy,
                _ => false
            };
            return result ? NodeState.SUCCESS : NodeState.FAILURE;
        }
    }

    public class BB_CheckTransform : ConditionMethod
    {
        [SharedVar] public Transform value;
        public ObjectCheckOp operation;

        public override NodeState Execute()
        {
            bool result = operation switch
            {
                ObjectCheckOp.IsNull     => value == null,
                ObjectCheckOp.IsNotNull  => value != null,
                ObjectCheckOp.IsActive   => value != null && value.gameObject.activeInHierarchy,
                ObjectCheckOp.IsInactive => value == null || !value.gameObject.activeInHierarchy,
                _ => false
            };
            return result ? NodeState.SUCCESS : NodeState.FAILURE;
        }
    }

    public class BB_CheckVector2 : ConditionMethod
    {
        [SharedVar] public Vector2 value;
        public VectorCheckOp operation;

        public override NodeState Execute()
        {
            bool result = operation switch
            {
                VectorCheckOp.IsZero    => value.sqrMagnitude < 0.0001f,
                VectorCheckOp.IsNotZero => value.sqrMagnitude >= 0.0001f,
                _ => false
            };
            return result ? NodeState.SUCCESS : NodeState.FAILURE;
        }
    }

    public class BB_CheckVector3 : ConditionMethod
    {
        [SharedVar] public Vector3 value;
        public VectorCheckOp operation;

        public override NodeState Execute()
        {
            bool result = operation switch
            {
                VectorCheckOp.IsZero    => value.sqrMagnitude < 0.0001f,
                VectorCheckOp.IsNotZero => value.sqrMagnitude >= 0.0001f,
                _ => false
            };
            return result ? NodeState.SUCCESS : NodeState.FAILURE;
        }
    }

    // ── Edge detection ─────────────────────────────────────────────

    public class BB_EdgeRisingBool : ConditionMethod
    {
        [SharedVar] public bool current;
        [SharedVar] public bool previous;

        public override NodeState Execute()
        {
            bool result = current && !previous;
            previous = current;
            return result ? NodeState.SUCCESS : NodeState.FAILURE;
        }
    }

    public class BB_EdgeFallingBool : ConditionMethod
    {
        [SharedVar] public bool current;
        [SharedVar] public bool previous;

        public override NodeState Execute()
        {
            bool result = !current && previous;
            previous = current;
            return result ? NodeState.SUCCESS : NodeState.FAILURE;
        }
    }

    // ── HasChanged ─────────────────────────────────────────────────

    public class BB_HasChangedInt : ConditionMethod
    {
        [SharedVar] public int current;
        [SharedVar] public int previous;

        public override NodeState Execute()
        {
            bool changed = current != previous;
            previous = current;
            return changed ? NodeState.SUCCESS : NodeState.FAILURE;
        }
    }

    public class BB_HasChangedFloat : ConditionMethod
    {
        [SharedVar] public float current;
        [SharedVar] public float previous;

        public override NodeState Execute()
        {
            bool changed = current != previous;
            previous = current;
            return changed ? NodeState.SUCCESS : NodeState.FAILURE;
        }
    }

    public class BB_HasChangedBool : ConditionMethod
    {
        [SharedVar] public bool current;
        [SharedVar] public bool previous;

        public override NodeState Execute()
        {
            bool changed = current != previous;
            previous = current;
            return changed ? NodeState.SUCCESS : NodeState.FAILURE;
        }
    }

    public class BB_HasChangedVector2 : ConditionMethod
    {
        [SharedVar] public Vector2 current;
        [SharedVar] public Vector2 previous;

        public override NodeState Execute()
        {
            bool changed = current != previous;
            previous = current;
            return changed ? NodeState.SUCCESS : NodeState.FAILURE;
        }
    }

    public class BB_HasChangedVector3 : ConditionMethod
    {
        [SharedVar] public Vector3 current;
        [SharedVar] public Vector3 previous;

        public override NodeState Execute()
        {
            bool changed = current != previous;
            previous = current;
            return changed ? NodeState.SUCCESS : NodeState.FAILURE;
        }
    }

    public class BB_HasChangedGameObject : ConditionMethod
    {
        [SharedVar] public GameObject current;
        [SharedVar] public GameObject previous;

        public override NodeState Execute()
        {
            bool changed = current != previous;
            previous = current;
            return changed ? NodeState.SUCCESS : NodeState.FAILURE;
        }
    }

    public class BB_HasChangedTransform : ConditionMethod
    {
        [SharedVar] public Transform current;
        [SharedVar] public Transform previous;

        public override NodeState Execute()
        {
            bool changed = current != previous;
            previous = current;
            return changed ? NodeState.SUCCESS : NodeState.FAILURE;
        }
    }
}
