using System.Collections.Generic;
using UnityEngine;

namespace BehaviourTree 
{
    public abstract class BehaviourNode : ScriptableObject
    {
        public List<BehaviourNode> children = new List<BehaviourNode>();
        [HideInInspector] public abstract BehaviourNodeType NodeType { get; }
        [HideInInspector] public int firstChildIndex;
        [HideInInspector] public int lastChildIndex;

        [HideInInspector] public string guid;
        [HideInInspector] public Vector2 graphPosition;
    }
}

