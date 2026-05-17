using UnityEngine;
using System.Runtime.InteropServices;
using BehaviourTree.Core;

namespace BehaviourTree.Runtime
{
    /// <summary>
    /// Parameters for HELLOWORLD method. Fields with [SharedVar] are blackboard variables;
    /// fields without are constants.
    /// </summary>
    [GenerateNodeFieldBindings]
    [StructLayout(LayoutKind.Sequential)]
    public partial struct HELLOWORLD_NodeFields
    {
        public Vector2 Speed;

        [SharedVar]
        public Vector2 Velocity;

        [SharedVar]
        public int Health;

        [SharedVar]
        public float TestTime;
    }
}
