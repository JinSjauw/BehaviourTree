using UnityEngine;
using System.Runtime.InteropServices;
using BehaviourTree.Core;

namespace BehaviourTree.Runtime
{
    /// <summary>
    /// Parameters for HELLOWORLD method. Fields with [SharedVar] are blackboard variables;
    /// fields without are constants.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public partial struct HELLOWORLD_Params
    {
        //public float Speed;

        public Vector2 testVector;

        [SharedVar]
        public int Health;

        [SharedVar]
        public float TestTime;
    }
}