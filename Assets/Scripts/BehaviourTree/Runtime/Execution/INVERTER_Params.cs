using System.Runtime.InteropServices;
using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Runtime
{
    /// <summary>
    /// Parameters for HELLOWORLD method. Fields with [SharedVar] are blackboard variables;
    /// fields without are constants.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public partial struct INVERTER_Params
    {
        public bool alwaysFailure;
        public bool alwaysSuccess;
    }
}