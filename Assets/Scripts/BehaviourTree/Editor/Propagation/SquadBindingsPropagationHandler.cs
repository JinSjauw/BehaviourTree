using BehaviourTree.Core;
using UnityEditor;
using UnityEngine;

namespace BehaviourTree.Editor.Propagation
{
    public class SquadBindingsPropagationHandler : IVariableChangeHandler
    {
        public void HandleRename(PropagationContext ctx, string oldName, string newName)
        {
            for (int s = 0; s < ctx.SquadDefs.Length; s++)
            {
                SquadDefinition squad = ctx.SquadDefs[s];
                if (squad == null || squad.bindingGroups == null) continue;

                bool changed = false;
                for (int bg = 0; bg < squad.bindingGroups.Count; bg++)
                {
                    SquadBindingGroup group = squad.bindingGroups[bg];
                    if (group == null || group.bindings == null) continue;
                    if (!GroupMatchesDefinition(ctx.Definition, group)) continue;

                    for (int b = 0; b < group.bindings.Count; b++)
                    {
                        VariableBinding binding = group.bindings[b];
                        if (binding == null) continue;

                        if (binding.treeVariableName == oldName)
                        {
                            binding.treeVariableName = newName;
                            changed = true;
                        }
                        if (binding.squadVariableName == oldName)
                        {
                            binding.squadVariableName = newName;
                            changed = true;
                        }
                    }
                }

                if (changed)
                    EditorUtility.SetDirty(squad);
            }
        }

        public void HandleDelete(PropagationContext ctx, string variableName, string variableTypeName)
        {
            for (int s = 0; s < ctx.SquadDefs.Length; s++)
            {
                SquadDefinition squad = ctx.SquadDefs[s];
                if (squad == null || squad.bindingGroups == null) continue;

                bool changed = false;
                for (int bg = 0; bg < squad.bindingGroups.Count; bg++)
                {
                    SquadBindingGroup group = squad.bindingGroups[bg];
                    if (group == null || group.bindings == null) continue;
                    if (!GroupMatchesDefinition(ctx.Definition, group)) continue;

                    for (int b = 0; b < group.bindings.Count; b++)
                    {
                        VariableBinding binding = group.bindings[b];
                        if (binding == null) continue;

                        if (binding.treeVariableName == variableName)
                        {
                            binding.treeVariableName = string.Empty;
                            changed = true;
                        }
                        if (binding.squadVariableName == variableName)
                        {
                            binding.squadVariableName = string.Empty;
                            changed = true;
                        }
                    }
                }

                if (changed)
                    EditorUtility.SetDirty(squad);
            }
        }

        public void HandleTypeChange(PropagationContext ctx, string variableName, string oldTypeName, string newTypeName)
        {
            // oldTypeName is null during the initial snapshot sync — not a real change.
            if (oldTypeName == null) return;

            for (int s = 0; s < ctx.SquadDefs.Length; s++)
            {
                SquadDefinition squad = ctx.SquadDefs[s];
                if (squad == null || squad.bindingGroups == null) continue;

                for (int bg = 0; bg < squad.bindingGroups.Count; bg++)
                {
                    SquadBindingGroup group = squad.bindingGroups[bg];
                    if (group == null || group.bindings == null) continue;
                    if (!GroupMatchesDefinition(ctx.Definition, group)) continue;

                    for (int b = 0; b < group.bindings.Count; b++)
                    {
                        VariableBinding binding = group.bindings[b];
                        if (binding == null) continue;

                        if (binding.treeVariableName == variableName || binding.squadVariableName == variableName)
                        {
                            string side = binding.treeVariableName == variableName ? "tree" : "squad";
                            string oldTypeDisplay = oldTypeName ?? "unknown";
                            Debug.LogWarning(
                                $"Variable '{variableName}' type changed ({oldTypeDisplay} → {newTypeName}). " +
                                $"Squad binding '{binding.treeVariableName} ↔ {binding.squadVariableName}' ({side} side) " +
                                $"in squad '{squad.name}' may be incompatible."
                            );
                        }
                    }
                }
            }
        }

        // ── Helpers ─────────────────────────────────────────────

        private static bool GroupMatchesDefinition(BlackboardDefinition definition, SquadBindingGroup group)
        {
            if (group.treeAsset != null)
                return group.treeAsset.BlackboardDefinition == definition;

            if (!string.IsNullOrEmpty(group.treeAssetGuid))
            {
                string path = AssetDatabase.GUIDToAssetPath(group.treeAssetGuid);
                BehaviourTreeAssetBase tree = AssetDatabase.LoadAssetAtPath<BehaviourTreeAssetBase>(path);
                return tree != null && tree.BlackboardDefinition == definition;
            }

            return false;
        }
    }
}
