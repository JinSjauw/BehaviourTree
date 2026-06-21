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

        private void OnValidate()
        {
            if (squadConnections == null || squadConnections.Count == 0) return;

            BlackboardDefinition bbDef = blackboardDefinition;
            if (bbDef == null) return;

            if (BlackboardDefinition.EnsureBaseChannel<int>(bbDef, "AgentAssignedRole", isSquadData: false)
                | BlackboardDefinition.EnsureBaseChannel<int>(bbDef, "AgentReceivedOrder", isSquadData: false))
            {
                EditorUtility.SetDirty(bbDef);
            }
        }
    }
}
