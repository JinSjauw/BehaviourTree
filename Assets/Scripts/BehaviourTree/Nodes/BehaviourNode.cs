using System.Collections.Generic;
using UnityEngine;

//[CreateAssetMenu(fileName = "BehaviourNode", menuName = "Scriptable Objects/BehaviourNode")]
public abstract class BehaviourNode : ScriptableObject
{
    public List<BehaviourNode> children = new List<BehaviourNode>();
    public abstract BehaviourNodeType NodeType { get; }
}
