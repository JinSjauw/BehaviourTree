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
        public Vector2 speed;

        [SharedVar]
        public Vector2 velocity;

        [SharedVar]
        public int health;

        [SharedVar]
        public float testTime;
    }
}
