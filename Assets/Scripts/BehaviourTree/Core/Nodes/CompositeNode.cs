using UnityEngine;

namespace BehaviourTree.Core
{
    public class CompositeNode : BehaviourNode
    {
        private BehaviourNodeType compositeType;

        public override BehaviourNodeType NodeType => compositeType;

        public void SetCompositeType(BehaviourNodeType type)
        {
            compositeType = type;
        }
    }
}
