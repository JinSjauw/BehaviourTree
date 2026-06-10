using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Runtime
{
    public class BB_CompareInt : ConditionMethod
    {
        [SharedVar] public int a;
        [SharedVar(true)] public int b;
        public NumericCompareOp operation;

        public override NodeState Execute()
        {
            bool result = operation switch
            {
                NumericCompareOp.Equal          => a == b,
                NumericCompareOp.NotEqual       => a != b,
                NumericCompareOp.Less           => a < b,
                NumericCompareOp.LessOrEqual    => a <= b,
                NumericCompareOp.Greater        => a > b,
                NumericCompareOp.GreaterOrEqual => a >= b,
                _ => false
            };
            return result ? NodeState.SUCCESS : NodeState.FAILURE;
        }
    }

    public class BB_CompareFloat : ConditionMethod
    {
        [SharedVar] public float a;
        [SharedVar(true)] public float b;
        public NumericCompareOp operation;

        public override NodeState Execute()
        {
            bool result = operation switch
            {
                NumericCompareOp.Equal          => a == b,
                NumericCompareOp.NotEqual       => a != b,
                NumericCompareOp.Less           => a < b,
                NumericCompareOp.LessOrEqual    => a <= b,
                NumericCompareOp.Greater        => a > b,
                NumericCompareOp.GreaterOrEqual => a >= b,
                _ => false
            };
            return result ? NodeState.SUCCESS : NodeState.FAILURE;
        }
    }

    public class BB_CompareBool : ConditionMethod
    {
        [SharedVar] public bool a;
        [SharedVar(true)] public bool b;
        public BoolCompareOp operation;

        public override NodeState Execute()
        {
            bool result = operation switch
            {
                BoolCompareOp.Equal    => a == b,
                BoolCompareOp.NotEqual => a != b,
                _ => false
            };
            return result ? NodeState.SUCCESS : NodeState.FAILURE;
        }
    }

    public class BB_CompareVector2 : ConditionMethod
    {
        [SharedVar] public Vector2 a;
        [SharedVar] public Vector2 b;
        public VectorCompareOp operation;

        public override NodeState Execute()
        {
            float aMag = a.magnitude;
            float bMag = b.magnitude;

            bool result = operation switch
            {
                VectorCompareOp.Equal                 => a == b,
                VectorCompareOp.NotEqual              => a != b,
                VectorCompareOp.MagnitudeLess         => aMag < bMag,
                VectorCompareOp.MagnitudeLessOrEqual  => aMag <= bMag,
                VectorCompareOp.MagnitudeGreater      => aMag > bMag,
                VectorCompareOp.MagnitudeGreaterOrEqual => aMag >= bMag,
                _ => false
            };
            return result ? NodeState.SUCCESS : NodeState.FAILURE;
        }
    }

    public class BB_CompareVector3 : ConditionMethod
    {
        [SharedVar] public Vector3 a;
        [SharedVar] public Vector3 b;
        public VectorCompareOp operation;

        public override NodeState Execute()
        {
            float aMag = a.magnitude;
            float bMag = b.magnitude;

            bool result = operation switch
            {
                VectorCompareOp.Equal                 => a == b,
                VectorCompareOp.NotEqual              => a != b,
                VectorCompareOp.MagnitudeLess         => aMag < bMag,
                VectorCompareOp.MagnitudeLessOrEqual  => aMag <= bMag,
                VectorCompareOp.MagnitudeGreater      => aMag > bMag,
                VectorCompareOp.MagnitudeGreaterOrEqual => aMag >= bMag,
                _ => false
            };
            return result ? NodeState.SUCCESS : NodeState.FAILURE;
        }
    }

    public class BB_CompareGameObject : ConditionMethod
    {
        [SharedVar] public GameObject a;
        [SharedVar] public GameObject b;
        public ObjectCompareOp operation;

        public override NodeState Execute()
        {
            bool result = operation switch
            {
                ObjectCompareOp.Equal    => a == b,
                ObjectCompareOp.NotEqual => a != b,
                _ => false
            };
            return result ? NodeState.SUCCESS : NodeState.FAILURE;
        }
    }

    public class BB_CompareTransform : ConditionMethod
    {
        [SharedVar] public Transform a;
        [SharedVar] public Transform b;
        public ObjectCompareOp operation;

        public override NodeState Execute()
        {
            bool result = operation switch
            {
                ObjectCompareOp.Equal    => a == b,
                ObjectCompareOp.NotEqual => a != b,
                _ => false
            };
            return result ? NodeState.SUCCESS : NodeState.FAILURE;
        }
    }
}
