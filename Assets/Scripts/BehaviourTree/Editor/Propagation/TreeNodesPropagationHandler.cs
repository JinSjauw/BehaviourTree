using BehaviourTree.Core;
using UnityEditor;
using UnityEngine;

namespace BehaviourTree.Editor.Propagation
{
    public class TreeNodesPropagationHandler : IVariableChangeHandler
    {
        public void HandleRename(PropagationContext ctx, string oldName, string newName)
        {
            for (int t = 0; t < ctx.MatchingTrees.Length; t++)
                UpdateTreeNodes(ctx.MatchingTrees[t], oldName, newName);
        }

        public void HandleDelete(PropagationContext ctx, string variableName, string variableTypeName)
        {
            for (int t = 0; t < ctx.MatchingTrees.Length; t++)
                ClearVariableFromTree(ctx.MatchingTrees[t], variableName);
        }

        public void HandleTypeChange(PropagationContext ctx, string variableName, string oldTypeName, string newTypeName)
        {
            for (int t = 0; t < ctx.MatchingTrees.Length; t++)
                UpdateTreeNodesType(ctx.MatchingTrees[t], variableName, newTypeName);
        }

        // ── Rename ──────────────────────────────────────────────

        private static void UpdateTreeNodes(BehaviourTreeAssetBase treeAsset, string oldName, string newName)
        {
            string assetPath = AssetDatabase.GetAssetPath(treeAsset);
            if (string.IsNullOrEmpty(assetPath)) return;

            Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            foreach (Object obj in subAssets)
            {
                bool changed = false;

                if (obj is LeafNode leaf && leaf.fieldEntries != null)
                {
                    for (int i = 0; i < leaf.fieldEntries.Count; i++)
                    {
                        NodeFieldEntry entry = leaf.fieldEntries[i];
                        if (entry.variableName == oldName)
                        {
                            entry.variableName = newName;
                            leaf.fieldEntries[i] = entry;
                            changed = true;
                        }
                    }
                }
                else if (obj is DecoratorNode decorator && decorator.fieldEntries != null)
                {
                    for (int i = 0; i < decorator.fieldEntries.Count; i++)
                    {
                        NodeFieldEntry entry = decorator.fieldEntries[i];
                        if (entry.variableName == oldName)
                        {
                            entry.variableName = newName;
                            decorator.fieldEntries[i] = entry;
                            changed = true;
                        }
                    }
                }
                else if (obj is SubtreeNode subtree && subtree.bindings != null)
                {
                    for (int i = 0; i < subtree.bindings.Count; i++)
                    {
                        SubtreeBinding binding = subtree.bindings[i];
                        if (binding.parentVariableName == oldName)
                        {
                            binding.parentVariableName = newName;
                            subtree.bindings[i] = binding;
                            changed = true;
                        }
                    }
                }

                if (changed)
                    EditorUtility.SetDirty(obj);
            }
        }

        // ── Delete ──────────────────────────────────────────────

        private static void ClearVariableFromTree(BehaviourTreeAssetBase treeAsset, string variableName)
        {
            string assetPath = AssetDatabase.GetAssetPath(treeAsset);
            if (string.IsNullOrEmpty(assetPath)) return;

            Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            foreach (Object obj in subAssets)
            {
                bool changed = false;

                if (obj is LeafNode leaf && leaf.fieldEntries != null)
                {
                    for (int i = 0; i < leaf.fieldEntries.Count; i++)
                    {
                        NodeFieldEntry entry = leaf.fieldEntries[i];
                        if (entry.variableName == variableName)
                        {
                            entry.variableName = string.Empty;
                            leaf.fieldEntries[i] = entry;
                            changed = true;
                        }
                    }
                }
                else if (obj is DecoratorNode decorator && decorator.fieldEntries != null)
                {
                    for (int i = 0; i < decorator.fieldEntries.Count; i++)
                    {
                        NodeFieldEntry entry = decorator.fieldEntries[i];
                        if (entry.variableName == variableName)
                        {
                            entry.variableName = string.Empty;
                            decorator.fieldEntries[i] = entry;
                            changed = true;
                        }
                    }
                }
                else if (obj is SubtreeNode subtree && subtree.bindings != null)
                {
                    for (int i = 0; i < subtree.bindings.Count; i++)
                    {
                        SubtreeBinding binding = subtree.bindings[i];
                        if (binding.parentVariableName == variableName)
                        {
                            binding.parentVariableName = string.Empty;
                            subtree.bindings[i] = binding;
                            changed = true;
                        }
                    }
                }

                if (changed)
                    EditorUtility.SetDirty(obj);
            }
        }

        // ── Type change ─────────────────────────────────────────

        private static void UpdateTreeNodesType(BehaviourTreeAssetBase treeAsset, string variableName, string newTypeName)
        {
            string assetPath = AssetDatabase.GetAssetPath(treeAsset);
            if (string.IsNullOrEmpty(assetPath)) return;

            Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            foreach (Object obj in subAssets)
            {
                bool changed = false;

                if (obj is LeafNode leaf && leaf.fieldEntries != null)
                {
                    for (int i = 0; i < leaf.fieldEntries.Count; i++)
                    {
                        NodeFieldEntry entry = leaf.fieldEntries[i];
                        if (entry.variableName == variableName && entry.isVariable)
                        {
                            entry.fieldTypeName = newTypeName;
                            leaf.fieldEntries[i] = entry;
                            changed = true;
                        }
                    }
                }
                else if (obj is DecoratorNode decorator && decorator.fieldEntries != null)
                {
                    for (int i = 0; i < decorator.fieldEntries.Count; i++)
                    {
                        NodeFieldEntry entry = decorator.fieldEntries[i];
                        if (entry.variableName == variableName && entry.isVariable)
                        {
                            entry.fieldTypeName = newTypeName;
                            decorator.fieldEntries[i] = entry;
                            changed = true;
                        }
                    }
                }
                else if (obj is CompositeNode composite && composite.fieldEntries != null)
                {
                    for (int i = 0; i < composite.fieldEntries.Count; i++)
                    {
                        NodeFieldEntry entry = composite.fieldEntries[i];
                        if (entry.variableName == variableName && entry.isVariable)
                        {
                            entry.fieldTypeName = newTypeName;
                            composite.fieldEntries[i] = entry;
                            changed = true;
                        }
                    }
                }

                if (changed)
                    EditorUtility.SetDirty(obj);
            }
        }
    }
}
