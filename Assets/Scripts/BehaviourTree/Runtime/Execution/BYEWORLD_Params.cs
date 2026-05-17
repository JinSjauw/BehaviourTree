using System.Runtime.InteropServices;
using BehaviourTree.Core;
using UnityEngine;

[GenerateParamsDeserializer]
[StructLayout(LayoutKind.Sequential)]
public partial struct BYEWORLD_Params
{
    public float waitingTime;
    [SharedVar]
    public int testTimedThreshhold;
    [SharedVar]
    public float timer;
}
