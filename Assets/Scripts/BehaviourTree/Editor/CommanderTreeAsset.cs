using BehaviourTree.Core;
using UnityEditor;
using UnityEngine;

namespace BehaviourTree.Editor
{
    /// <summary>
    /// A behaviour tree asset specifically for commander trees.
    /// Inherits node management from BaseEditorTreeAsset.
    /// Owns its BlackboardDefinition for squad-data variables (isSquadData)
    /// alongside transient scalars (_agentCount, _targetAgentID, etc.).
    /// Type distinguishes commander trees from agent trees in the editor.
    /// </summary>
    [CreateAssetMenu(menuName = "BehaviourTree/Commander Tree")]
    public class CommanderTreeAsset : BaseEditorTreeAsset
    {
        public override void CreateBlackBoard()
        {
            if (commanderBlackboardDefinition != null)
            {
                // Existing commander tree — bridge to the base field
                blackboardDefinition = commanderBlackboardDefinition;
                EditorUtility.SetDirty(this);
                AssetDatabase.SaveAssets();
                return;
            }

            BlackboardDefinition created = CreateInstance<BlackboardDefinition>();
            created.name = name + "_CommanderBB";

            commanderBlackboardDefinition = created;
            blackboardDefinition = created;
            AssetDatabase.AddObjectToAsset(created, this);
            AssetDatabase.SaveAssets();
        }
    }
}
