using System.Runtime.InteropServices;
using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Runtime
{
    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_CompareInt_Params
    {
        [SharedVar] public int A;
        [SharedVar] public int B;
        public NumericCompareOp Operation;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_CompareFloat_Params
    {
        [SharedVar] public float A;
        [SharedVar] public float B;
        public NumericCompareOp Operation;
        public float Epsilon;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_CompareBool_Params
    {
        [SharedVar] public bool A;
        [SharedVar] public bool B;
        public BoolCompareOp Operation;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_CompareVector2_Params
    {
        [SharedVar] public Vector2 A;
        [SharedVar] public Vector2 B;
        public VectorCompareOp Operation;
        public float Epsilon;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_CompareVector3_Params
    {
        [SharedVar] public Vector3 A;
        [SharedVar] public Vector3 B;
        public VectorCompareOp Operation;
        public float Epsilon;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_CompareGameObject_Params
    {
        [SharedVar] public GameObject A;
        [SharedVar] public GameObject B;
        public ObjectCompareOp Operation;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_CompareTransform_Params
    {
        [SharedVar] public Transform A;
        [SharedVar] public Transform B;
        public ObjectCompareOp Operation;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_SetInt_Params
    {
        [SharedVar] public int Target;
        [SharedVar] public int Value;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_SetFloat_Params
    {
        [SharedVar] public float Target;
        [SharedVar] public float Value;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_SetBool_Params
    {
        [SharedVar] public bool Target;
        [SharedVar] public bool Value;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_SetVector2_Params
    {
        [SharedVar] public Vector2 Target;
        [SharedVar] public Vector2 Value;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_SetVector3_Params
    {
        [SharedVar] public Vector3 Target;
        [SharedVar] public Vector3 Value;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_SetGameObject_Params
    {
        [SharedVar] public GameObject Target;
        [SharedVar] public GameObject Value;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_SetTransform_Params
    {
        [SharedVar] public Transform Target;
        [SharedVar] public Transform Value;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_ClearInt_Params
    {
        [SharedVar] public int Target;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_ClearFloat_Params
    {
        [SharedVar] public float Target;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_ClearBool_Params
    {
        [SharedVar] public bool Target;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_ClearVector2_Params
    {
        [SharedVar] public Vector2 Target;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_ClearVector3_Params
    {
        [SharedVar] public Vector3 Target;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_ClearGameObject_Params
    {
        [SharedVar] public GameObject Target;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_ClearTransform_Params
    {
        [SharedVar] public Transform Target;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_ToggleBool_Params
    {
        [SharedVar] public bool Target;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct WaitSeconds_Params
    {
        public float Duration;
        [SharedVar] public float Elapsed;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct Cooldown_Params
    {
        public float Duration;
        [SharedVar] public float Remaining;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_HasChangedInt_Params
    {
        [SharedVar] public int Current;
        [SharedVar] public int Previous;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_HasChangedFloat_Params
    {
        [SharedVar] public float Current;
        [SharedVar] public float Previous;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_HasChangedBool_Params
    {
        [SharedVar] public bool Current;
        [SharedVar] public bool Previous;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_HasChangedVector2_Params
    {
        [SharedVar] public Vector2 Current;
        [SharedVar] public Vector2 Previous;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_HasChangedVector3_Params
    {
        [SharedVar] public Vector3 Current;
        [SharedVar] public Vector3 Previous;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_HasChangedGameObject_Params
    {
        [SharedVar] public GameObject Current;
        [SharedVar] public GameObject Previous;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_HasChangedTransform_Params
    {
        [SharedVar] public Transform Current;
        [SharedVar] public Transform Previous;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_EdgeRisingBool_Params
    {
        [SharedVar] public bool Current;
        [SharedVar] public bool Previous;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_EdgeFallingBool_Params
    {
        [SharedVar] public bool Current;
        [SharedVar] public bool Previous;
    }
}
