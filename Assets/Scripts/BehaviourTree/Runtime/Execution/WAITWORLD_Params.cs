using System.Runtime.InteropServices;
using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Runtime
{
    [GenerateNodeFieldBindings]
    [StructLayout(LayoutKind.Sequential)]
    public partial struct WAITWORLD_NodeFields
    {
        [SharedVar]
        public int testTimedThreshhold;
        [SharedVar]
        public float timer;
    }
}
