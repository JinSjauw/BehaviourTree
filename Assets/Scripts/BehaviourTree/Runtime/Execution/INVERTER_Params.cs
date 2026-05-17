using System.Runtime.InteropServices;
using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Runtime
{
    [GenerateParamsDeserializer]
    [StructLayout(LayoutKind.Sequential)]
    public partial struct INVERTER_Params
    {
        public bool alwaysFailure;
        public bool alwaysSuccess;
    }
}
