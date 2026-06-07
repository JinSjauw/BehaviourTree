using System.Runtime.InteropServices;
using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Runtime
{
    [GenerateNodeFieldBindings]
    [StructLayout(LayoutKind.Sequential)]
    public partial struct Enemy_MoveTo_NodeFields
    {
        [SharedVar]
        public Vector3 targetMovePosition;
    }

    [GenerateNodeFieldBindings]
    [StructLayout(LayoutKind.Sequential)]
    public partial struct Enemy_SelectPatrolPoint_NodeFields
    {
        [SharedVar]
        public Vector3 targetMovePosition;
        [SharedVar]
        public Transform patrolPointsParent;
        public PatrolPointSelection selectionMode;
    }

    [GenerateNodeFieldBindings]
    [StructLayout(LayoutKind.Sequential)]
    public partial struct Enemy_SelectEngagePosition_NodeFields
    {
        [SharedVar]
        public Vector3 targetMovePosition;
    }

    [GenerateNodeFieldBindings]
    [StructLayout(LayoutKind.Sequential)]
    public partial struct Enemy_SelectDetectedTarget_NodeFields
    {
        public SelectionStrategy strategy;
        [SharedVar]
        public Transform selectedTarget;
    }

    [GenerateNodeFieldBindings]
    [StructLayout(LayoutKind.Sequential)]
    public partial struct Enemy_IsInFiringRange_NodeFields
    {
        [SharedVar]
        public Transform selectedTarget;
    }

    [GenerateNodeFieldBindings]
    [StructLayout(LayoutKind.Sequential)]
    public partial struct Enemy_HasLineOfSight_NodeFields
    {
        [SharedVar]
        public Transform selectedTarget;
    }

    [GenerateNodeFieldBindings]
    [StructLayout(LayoutKind.Sequential)]
    public partial struct Enemy_SetAiming_NodeFields
    {
        public bool aiming;
    }

    [GenerateNodeFieldBindings]
    [StructLayout(LayoutKind.Sequential)]
    public partial struct Enemy_FireSequence_NodeFields
    {
        public float reloadDuration;
        public float firingDelay;
        [SharedVar]
        public Transform selectedTarget;
    }

    [GenerateNodeFieldBindings]
    [StructLayout(LayoutKind.Sequential)]
    public partial struct Enemy_MoveTo_Transform_NodeFields
    {
        [SharedVar]
        public Transform target;
    }
}
