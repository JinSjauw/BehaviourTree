using System.Runtime.InteropServices;
using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Runtime
{
    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_CompareInt_NodeFields
    {
        [SharedVar] public int a;
        [SharedVar(true)] public int b;
        public NumericCompareOp operation;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_CompareFloat_NodeFields
    {
        [SharedVar] public float a;
        [SharedVar(true)] public float b;
        public NumericCompareOp operation;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_CompareBool_NodeFields
    {
        [SharedVar] public bool a;
        [SharedVar(true)] public bool b;
        public BoolCompareOp operation;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_CompareVector2_NodeFields
    {
        [SharedVar] public Vector2 a;
        [SharedVar] public Vector2 b;
        public VectorCompareOp operation;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_CompareVector3_NodeFields
    {
        [SharedVar] public Vector3 a;
        [SharedVar] public Vector3 b;
        public VectorCompareOp operation;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_CompareGameObject_NodeFields
    {
        [SharedVar] public GameObject a;
        [SharedVar] public GameObject b;
        public ObjectCompareOp operation;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_CompareTransform_NodeFields
    {
        [SharedVar] public Transform a;
        [SharedVar] public Transform b;
        public ObjectCompareOp operation;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_CheckBool_NodeFields
    {
        [SharedVar] public bool value;
        public BoolCheckOp operation;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_CheckGameObject_NodeFields
    {
        [SharedVar] public GameObject value;
        public ObjectCheckOp operation;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_CheckTransform_NodeFields
    {
        [SharedVar] public Transform value;
        public ObjectCheckOp operation;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_CheckVector2_NodeFields
    {
        [SharedVar] public Vector2 value;
        public VectorCheckOp operation;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_CheckVector3_NodeFields
    {
        [SharedVar] public Vector3 value;
        public VectorCheckOp operation;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_Log_NodeFields
    {
        public string message;
        public bool useVariable;
        [SharedVar] public string messageVar;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_SetInt_NodeFields
    {
        [SharedVar] public int target;
        [SharedVar(true)] public int value;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_SetFloat_NodeFields
    {
        [SharedVar] public float target;
        [SharedVar(true)] public float value;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_SetBool_NodeFields
    {
        [SharedVar] public bool target;
        [SharedVar(true)] public bool value;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_SetVector2_NodeFields
    {
        [SharedVar] public Vector2 target;
        [SharedVar] public Vector2 value;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_SetVector3_NodeFields
    {
        [SharedVar] public Vector3 target;
        [SharedVar] public Vector3 value;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_SetGameObject_NodeFields
    {
        [SharedVar] public GameObject target;
        [SharedVar] public GameObject value;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_SetTransform_NodeFields
    {
        [SharedVar] public Transform target;
        [SharedVar] public Transform value;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_SetVector2FromTransform_NodeFields
    {
        [SharedVar] public Vector2 target;
        [SharedVar] public Transform source;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_SetVector3FromTransform_NodeFields
    {
        [SharedVar] public Vector3 target;
        [SharedVar] public Transform source;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_ClearInt_NodeFields
    {
        [SharedVar] public int target;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_ClearFloat_NodeFields
    {
        [SharedVar] public float target;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_ClearBool_NodeFields
    {
        [SharedVar] public bool target;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_ClearVector2_NodeFields
    {
        [SharedVar] public Vector2 target;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_ClearVector3_NodeFields
    {
        [SharedVar] public Vector3 target;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_ClearGameObject_NodeFields
    {
        [SharedVar] public GameObject target;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_ClearTransform_NodeFields
    {
        [SharedVar] public Transform target;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_ToggleBool_NodeFields
    {
        [SharedVar] public bool target;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct WaitSeconds_NodeFields
    {
        public float duration;
        [SharedVar] public float elapsed;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct Cooldown_NodeFields
    {
        public float duration;
        [SharedVar] public float remaining;
        public bool useCustomTick;
        public float customTickValue;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_HasChangedInt_NodeFields
    {
        [SharedVar] public int current;
        [SharedVar] public int previous;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_HasChangedFloat_NodeFields
    {
        [SharedVar] public float current;
        [SharedVar] public float previous;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_HasChangedBool_NodeFields
    {
        [SharedVar] public bool current;
        [SharedVar] public bool previous;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_HasChangedVector2_NodeFields
    {
        [SharedVar] public Vector2 current;
        [SharedVar] public Vector2 previous;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_HasChangedVector3_NodeFields
    {
        [SharedVar] public Vector3 current;
        [SharedVar] public Vector3 previous;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_HasChangedGameObject_NodeFields
    {
        [SharedVar] public GameObject current;
        [SharedVar] public GameObject previous;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_HasChangedTransform_NodeFields
    {
        [SharedVar] public Transform current;
        [SharedVar] public Transform previous;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_EdgeRisingBool_NodeFields
    {
        [SharedVar] public bool current;
        [SharedVar] public bool previous;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_EdgeFallingBool_NodeFields
    {
        [SharedVar] public bool current;
        [SharedVar] public bool previous;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_LogInt_NodeFields
    {
        [SharedVar] public int value;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_LogFloat_NodeFields
    {
        [SharedVar] public float value;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_LogBool_NodeFields
    {
        [SharedVar] public bool value;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_LogVector2_NodeFields
    {
        [SharedVar] public Vector2 value;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_LogVector3_NodeFields
    {
        [SharedVar] public Vector3 value;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_LogGameObject_NodeFields
    {
        [SharedVar] public GameObject value;
    }

    [StructLayout(LayoutKind.Sequential)]
    public partial struct BB_LogTransform_NodeFields
    {
        [SharedVar] public Transform value;
    }
}
