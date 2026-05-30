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
        public BehaviourTreeAssetBase subTreeAsset;
        public List<SubtreeBinding> bindings = new List<SubtreeBinding>();

        public BehaviourTreeAssetBase SubTreeAsset => subTreeAsset;
    }
}
