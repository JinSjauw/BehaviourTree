using UnityEngine;

namespace BehaviourTree.Core
{
    public class CompositeNode : BehaviourNode
    {
        [HideInInspector] [SerializeField] private BehaviourNodeType compositeType = BehaviourNodeType.SELECTOR;

        public override BehaviourNodeType NodeType => compositeType;

        public void SetCompositeType(BehaviourNodeType type)
        {
            if (type != BehaviourNodeType.SEQUENCE && type != BehaviourNodeType.SELECTOR && 
                type != BehaviourNodeType.PARALLEL && type != BehaviourNodeType.PRIORITY)
            {
                Debug.LogError($"Invalid composite type '{type}' assigned to CompositeNode. Ignoring.");
                return;
            }
            compositeType = type;
        }
    }
}
