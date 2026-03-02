using UnityEngine;

[CreateAssetMenu(fileName = "Selector Node", menuName = "Scriptable Objects/BT Nodes/Selector Node")]
public class SelectorNode : BehaviourNode
{
    public override BehaviourNodeType NodeType => BehaviourNodeType.SELECTOR;
}
