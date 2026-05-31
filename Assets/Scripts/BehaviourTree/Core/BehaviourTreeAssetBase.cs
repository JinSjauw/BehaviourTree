using UnityEngine;

namespace BehaviourTree.Core
{
    public class BehaviourTreeAssetBase : ScriptableObject
    {
        [HideInInspector] public BehaviourNode root;
        [HideInInspector] public BlackboardDefinition blackboardDefinition;

        public BehaviourNode Root => root;
        public BlackboardDefinition BlackboardDefinition => blackboardDefinition;
        public string DisplayName => name ?? "NO NAME GIVEN";
    }
}
