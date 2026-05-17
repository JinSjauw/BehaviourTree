using System.Runtime.InteropServices;
using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Runtime
{
    [GenerateNodeFieldBindings]
    [StructLayout(LayoutKind.Sequential)]
    public partial struct INVERTER_NodeFields
    {
        public bool alwaysFailure;
        public bool alwaysSuccess;
    }
}
