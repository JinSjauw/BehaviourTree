using System.Runtime.InteropServices;
using BehaviourTree.Core;
using UnityEngine;

[StructLayout(LayoutKind.Sequential)]
public partial struct BYEWORLD_Params
{
    public int indexValue;
    public float wow;

    [SharedVar]
    public Transform playerObject;
}
