using System.Runtime.InteropServices;
using BehaviourTree.Core;

namespace BehaviourTree.Runtime
{
    [GenerateNodeFieldBindings]
    [StructLayout(LayoutKind.Sequential)]
    public partial struct REPEATER_NodeFields
    {
        public int targetCount;

        [SharedVar]
        public int currentCount;
    }
}
