using System.Runtime.InteropServices;
using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Runtime
{
    [GenerateParamsDeserializer]
    [StructLayout(LayoutKind.Sequential)]
    public partial struct WAITWORLD_Params
    {
        [SharedVar]
        public int testTimedThreshhold;
        [SharedVar]
        public float timer;
    }
}
