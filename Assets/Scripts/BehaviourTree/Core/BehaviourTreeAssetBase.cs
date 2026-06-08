using UnityEngine;

namespace BehaviourTree.Core
{
    public class BehaviourTreeAssetBase : ScriptableObject
    {
        [HideInInspector] public BehaviourNode root;
        [HideInInspector] public BlackboardDefinition blackboardDefinition;
        [HideInInspector] public CommanderBlackboardDefinition commanderBlackboardDefinition;

        public BehaviourNode Root => root;
        public BlackboardDefinition BlackboardDefinition => blackboardDefinition;
        public CommanderBlackboardDefinition CommanderBlackboardDefinition => commanderBlackboardDefinition;
        public string DisplayName => name ?? "NO NAME GIVEN";
    }
}
