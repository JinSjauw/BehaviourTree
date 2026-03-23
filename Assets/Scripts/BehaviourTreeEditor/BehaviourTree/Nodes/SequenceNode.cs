using UnityEngine;

namespace BTreeEditor
{
    [CreateAssetMenu(fileName = "Sequence Node", menuName = "Scriptable Objects/BT Nodes/Sequence Node")]
    public class SequenceNode : BehaviourNode
    {
        public override BehaviourNodeType NodeType => BehaviourNodeType.SEQUENCE;
    }
}
