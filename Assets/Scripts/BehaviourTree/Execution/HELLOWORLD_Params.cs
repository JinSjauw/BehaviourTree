using System.Runtime.InteropServices;
using BehaviourTree.Core;

namespace BehaviourTree.Execution
{
    /// <summary>
    /// Parameters for HELLOWORLD method. Fields with [BTreeVar] are blackboard variables;
    /// fields without are constants.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public partial struct HELLOWORLD_Params
    {
        public float Speed;

        [BTreeVar]
        public int Health;

        [BTreeVar]
        public float TestTime;
    }
}