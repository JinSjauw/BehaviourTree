using System.Runtime.InteropServices;
using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Runtime
{
    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_CompareInt_NodeFields
    {
        [SharedVar] public int A;
        [SharedVar] public int B;
        public NumericCompareOp Operation;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_CompareFloat_NodeFields
    {
        [SharedVar] public float A;
        [SharedVar] public float B;
        public NumericCompareOp Operation;
        public float Epsilon;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_CompareBool_NodeFields
    {
        [SharedVar] public bool A;
        [SharedVar] public bool B;
        public BoolCompareOp Operation;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_CompareVector2_NodeFields
    {
        [SharedVar] public Vector2 A;
        [SharedVar] public Vector2 B;
        public VectorCompareOp Operation;
        public float Epsilon;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_CompareVector3_NodeFields
    {
        [SharedVar] public Vector3 A;
        [SharedVar] public Vector3 B;
        public VectorCompareOp Operation;
        public float Epsilon;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_CompareGameObject_NodeFields
    {
        [SharedVar] public GameObject A;
        [SharedVar] public GameObject B;
        public ObjectCompareOp Operation;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_CompareTransform_NodeFields
    {
        [SharedVar] public Transform A;
        [SharedVar] public Transform B;
        public ObjectCompareOp Operation;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_CheckBool_NodeFields
    {
        [SharedVar] public bool Value;
        public BoolCheckOp Operation;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_CheckGameObject_NodeFields
    {
        [SharedVar] public GameObject Value;
        public NullCheckOp Operation;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_CheckTransform_NodeFields
    {
        [SharedVar] public Transform Value;
        public NullCheckOp Operation;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_Log_NodeFields
    {
        public string Message;
        public bool UseVariable;
        [SharedVar] public string MessageVar;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_SetInt_NodeFields
    {
        [SharedVar] public int Target;
        [SharedVar] public int Value;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_SetFloat_NodeFields
    {
        [SharedVar] public float Target;
        [SharedVar] public float Value;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_SetBool_NodeFields
    {
        [SharedVar] public bool Target;
        [SharedVar] public bool Value;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_SetVector2_NodeFields
    {
        [SharedVar] public Vector2 Target;
        [SharedVar] public Vector2 Value;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_SetVector3_NodeFields
    {
        [SharedVar] public Vector3 Target;
        [SharedVar] public Vector3 Value;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_SetGameObject_NodeFields
    {
        [SharedVar] public GameObject Target;
        [SharedVar] public GameObject Value;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_SetTransform_NodeFields
    {
        [SharedVar] public Transform Target;
        [SharedVar] public Transform Value;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_ClearInt_NodeFields
    {
        [SharedVar] public int Target;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_ClearFloat_NodeFields
    {
        [SharedVar] public float Target;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_ClearBool_NodeFields
    {
        [SharedVar] public bool Target;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_ClearVector2_NodeFields
    {
        [SharedVar] public Vector2 Target;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_ClearVector3_NodeFields
    {
        [SharedVar] public Vector3 Target;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_ClearGameObject_NodeFields
    {
        [SharedVar] public GameObject Target;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_ClearTransform_NodeFields
    {
        [SharedVar] public Transform Target;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_ToggleBool_NodeFields
    {
        [SharedVar] public bool Target;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct WaitSeconds_NodeFields
    {
        public float Duration;
        [SharedVar] public float Elapsed;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct Cooldown_NodeFields
    {
        public float Duration;
        [SharedVar] public float Remaining;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_HasChangedInt_NodeFields
    {
        [SharedVar] public int Current;
        [SharedVar] public int Previous;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_HasChangedFloat_NodeFields
    {
        [SharedVar] public float Current;
        [SharedVar] public float Previous;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_HasChangedBool_NodeFields
    {
        [SharedVar] public bool Current;
        [SharedVar] public bool Previous;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_HasChangedVector2_NodeFields
    {
        [SharedVar] public Vector2 Current;
        [SharedVar] public Vector2 Previous;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_HasChangedVector3_NodeFields
    {
        [SharedVar] public Vector3 Current;
        [SharedVar] public Vector3 Previous;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_HasChangedGameObject_NodeFields
    {
        [SharedVar] public GameObject Current;
        [SharedVar] public GameObject Previous;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_HasChangedTransform_NodeFields
    {
        [SharedVar] public Transform Current;
        [SharedVar] public Transform Previous;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_EdgeRisingBool_NodeFields
    {
        [SharedVar] public bool Current;
        [SharedVar] public bool Previous;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_EdgeFallingBool_NodeFields
    {
        [SharedVar] public bool Current;
        [SharedVar] public bool Previous;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_LogInt_NodeFields
    {
        [SharedVar] public int Value;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_LogFloat_NodeFields
    {
        [SharedVar] public float Value;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_LogBool_NodeFields
    {
        [SharedVar] public bool Value;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_LogVector2_NodeFields
    {
        [SharedVar] public Vector2 Value;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_LogVector3_NodeFields
    {
        [SharedVar] public Vector3 Value;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_LogGameObject_NodeFields
    {
        [SharedVar] public GameObject Value;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_LogTransform_NodeFields
    {
        [SharedVar] public Transform Value;
    }
}
