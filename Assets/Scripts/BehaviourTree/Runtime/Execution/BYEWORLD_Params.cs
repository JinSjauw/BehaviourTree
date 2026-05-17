using System.Runtime.InteropServices;
using BehaviourTree.Core;
using UnityEngine;

[GenerateNodeFieldBindings]
[StructLayout(LayoutKind.Sequential)]
public partial struct BYEWORLD_NodeFields
{
    public float waitingTime;
    [SharedVar]
    public int testTimedThreshhold;
    [SharedVar]
    public float timer;
}
