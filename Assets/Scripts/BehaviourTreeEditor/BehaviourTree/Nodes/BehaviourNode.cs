using System.Collections.Generic;
using UnityEngine;

namespace BTreeEditor 
{
    public abstract class BehaviourNode : ScriptableObject
    {
        public List<BehaviourNode> children = new List<BehaviourNode>();
        public abstract BehaviourNodeType NodeType { get; }
        public int firstChildIndex;
        public int lastChildIndex;
    }
}

