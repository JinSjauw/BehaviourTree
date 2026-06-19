using BehaviourTree.Core;
using UnityEditor;
using UnityEngine;

namespace BehaviourTree.Editor
{
    /// <summary>
    /// A behaviour tree asset specifically for commander trees.
    /// Inherits node management from BaseEditorTreeAsset.
    /// Owns its CommanderBlackboardDefinition (the shared protocol).
    /// Type distinguishes commander trees from agent trees in the editor.
    /// </summary>
    [CreateAssetMenu(menuName = "BehaviourTree/Commander Tree")]
    public class CommanderTreeAsset : BaseEditorTreeAsset
    {
        public override void CreateBlackBoard()
        {
            CommanderBlackboardDefinition created = CreateInstance<CommanderBlackboardDefinition>();
            created.name = name + "_CommanderBB";

            commanderBlackboardDefinition = created;
            AssetDatabase.AddObjectToAsset(created, this);
            AssetDatabase.SaveAssets();
        }
    }
}
