using UnityEngine;

namespace BehaviourTree.Core
{
    public class CompositeNode : BehaviourNode
    {
        [HideInInspector] [SerializeField] private BehaviourNodeType compositeType = BehaviourNodeType.SELECTOR;

        public override BehaviourNodeType NodeType => compositeType;

        public void SetCompositeType(BehaviourNodeType type)
        {
            compositeType = type;
        }
    }
}
