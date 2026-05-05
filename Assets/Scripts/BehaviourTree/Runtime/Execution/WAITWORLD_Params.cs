using System.Runtime.InteropServices;
using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Runtime
{
    [StructLayout(LayoutKind.Sequential)]
    public partial struct WAITWORLD_Params
    {
        public Vector3 test;

        [SharedVar]
        public GameObject testObject;
    }
}
