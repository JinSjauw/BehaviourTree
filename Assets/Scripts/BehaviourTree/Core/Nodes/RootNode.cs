using UnityEngine;

namespace BehaviourTree.Core
{
    //[CreateAssetMenu(fileName = "RootNode", menuName = "Scriptable Objects/RootNode")]
    public class RootNode : BehaviourNode
    {
        public override BehaviourNodeType NodeType => BehaviourNodeType.ROOT;
    }
}
