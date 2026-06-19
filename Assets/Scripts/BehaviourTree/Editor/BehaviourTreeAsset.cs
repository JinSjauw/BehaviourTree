using BehaviourTree.Core;
using UnityEditor;
using UnityEngine;

namespace BehaviourTree.Editor
{
    public class AgentTreeAsset : BaseEditorTreeAsset
    {
        public override void CreateBlackBoard()
        {
            BlackboardDefinition createdBlackboard = CreateInstance<BlackboardDefinition>();
            createdBlackboard.name = this.name + "_BB_Definition";

            blackboardDefinition = createdBlackboard;
            AssetDatabase.AddObjectToAsset(createdBlackboard, this);
            AssetDatabase.SaveAssets();
        }
    }
}
