using System.Runtime.InteropServices;
using BehaviourTree.Core;

namespace BehaviourTree.Runtime
{
    [StructLayout(LayoutKind.Sequential)]
    public partial struct REPEATER_Params
    {
        public int targetCount;

        [SharedVar]
        public int currentCount;
    }
}