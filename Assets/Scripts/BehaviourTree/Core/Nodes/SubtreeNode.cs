using UnityEngine;
using System;
using System.Collections.Generic;

namespace BehaviourTree.Core
{
    [Serializable]
    public struct SubtreeBinding
    {
        public string subtreeVariableName;
        public string parentVariableName;
    }

    
    public class SubtreeNode : BehaviourNode
    {
        public override BehaviourNodeType NodeType => BehaviourNodeType.SUBTREE;
        public UnityEngine.Object subTreeAsset;
        public List<SubtreeBinding> bindings = new List<SubtreeBinding>();

        public IBehaviourTreeAuthoringAsset SubTreeAsset => subTreeAsset as IBehaviourTreeAuthoringAsset;
    }
}
