using UnityEngine;

namespace BehaviourTree.Editor
{
    [CreateAssetMenu(fileName = "NodeToolTipRegistry", menuName = "Scriptable Objects/NodeToolTipRegistry")]
    public class NodeToolTipRegistry : ScriptableObject
    {
        //Build Dictionary with MethodID key and string values
        //Build separate one for composites.
        //Build custom editor so you can input the tooltips directly via inspector.
    }
}
