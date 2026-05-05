using UnityEditor;
using UnityEngine;

namespace BehaviourTree.Editor
{
    public class BTreeAssetRenameHandler : AssetModificationProcessor
    {
        private static AssetMoveResult OnWillMoveAsset(string oldPath, string newPath)
        {
            // 1. Load the asset at oldPath
            if (AssetDatabase.LoadMainAssetAtPath(oldPath) is BehaviourTreeAsset so)
            {
                string oldName = System.IO.Path.GetFileNameWithoutExtension(oldPath);
                string newName = System.IO.Path.GetFileNameWithoutExtension(newPath);
                
                if (oldName != newName)
                {
                    so.blackboardDefinition.name = newName + "_BB_Definition";
                    EditorUtility.SetDirty(so.blackboardDefinition);
                    AssetDatabase.SaveAssets();
                }
            }

            return AssetMoveResult.DidNotMove;
        }
    }
}

