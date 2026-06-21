using BehaviourTree.Core;
using BehaviourTree.Runtime;
using UnityEditor;
using UnityEngine;

namespace BehaviourTree.Editor.Propagation
{
    public class TrackedBindingsPropagationHandler : IVariableChangeHandler
    {
        public void HandleRename(PropagationContext ctx, string oldName, string newName)
        {
            for (int r = 0; r < ctx.Runners.Length; r++)
            {
                BehaviourTreeRunnerBase runner = ctx.Runners[r];
                if (runner == null || runner.trackedBindingGroups == null) continue;

                for (int g = 0; g < runner.trackedBindingGroups.Count; g++)
                {
                    TrackedBindingGroup group = runner.trackedBindingGroups[g];
                    if (group == null || group.bindings == null) continue;
                    if (!GroupMatchesDefinition(ctx.Definition, group)) continue;

                    for (int b = 0; b < group.bindings.Count; b++)
                    {
                        TrackedBinding binding = group.bindings[b];
                        if (binding != null && binding.blackboardVariableName == oldName)
                        {
                            binding.blackboardVariableName = newName;
                            EditorUtility.SetDirty(runner);
                        }
                    }
                }
            }
        }

        public void HandleDelete(PropagationContext ctx, string variableName, string variableTypeName)
        {
            for (int r = 0; r < ctx.Runners.Length; r++)
            {
                BehaviourTreeRunnerBase runner = ctx.Runners[r];
                if (runner == null || runner.trackedBindingGroups == null) continue;

                for (int g = 0; g < runner.trackedBindingGroups.Count; g++)
                {
                    TrackedBindingGroup group = runner.trackedBindingGroups[g];
                    if (group == null || group.bindings == null) continue;
                    if (!GroupMatchesDefinition(ctx.Definition, group)) continue;

                    for (int b = 0; b < group.bindings.Count; b++)
                    {
                        TrackedBinding binding = group.bindings[b];
                        if (binding != null && binding.blackboardVariableName == variableName)
                        {
                            binding.blackboardVariableName = string.Empty;
                            EditorUtility.SetDirty(runner);
                        }
                    }
                }
            }
        }

        public void HandleTypeChange(PropagationContext ctx, string variableName, string oldTypeName, string newTypeName)
        {
            // oldTypeName is null during the initial snapshot sync — not a real change.
            if (oldTypeName == null) return;

            for (int r = 0; r < ctx.Runners.Length; r++)
            {
                BehaviourTreeRunnerBase runner = ctx.Runners[r];
                if (runner == null || runner.trackedBindingGroups == null) continue;

                for (int g = 0; g < runner.trackedBindingGroups.Count; g++)
                {
                    TrackedBindingGroup group = runner.trackedBindingGroups[g];
                    if (group == null || group.bindings == null) continue;
                    if (!GroupMatchesDefinition(ctx.Definition, group)) continue;

                    for (int b = 0; b < group.bindings.Count; b++)
                    {
                        TrackedBinding binding = group.bindings[b];
                        if (binding == null || binding.blackboardVariableName != variableName) continue;

                        // Skip if the new type matches the member type — binding is still valid.
                        if (newTypeName == binding.memberTypeName) continue;

                        string memberPath = binding.memberName != null
                            ? $"{binding.memberTypeName ?? "?"}.{binding.memberName}"
                            : "(unset)";

                        string oldTypeDisplay = oldTypeName ?? "unknown";
                        Debug.LogWarning(
                            $"Variable '{variableName}' type changed ({oldTypeDisplay} → {newTypeName}). " +
                            $"Tracked binding '{memberPath}' on runner '{runner.name}' may be incompatible."
                        );
                    }
                }
            }
        }

        // ── Helpers ─────────────────────────────────────────────

        private static bool GroupMatchesDefinition(BlackboardDefinition definition, TrackedBindingGroup group)
        {
            if (group.targetTree != null)
                return group.targetTree.BlackboardDefinition == definition;

            if (!string.IsNullOrEmpty(group.targetTreeGuid))
            {
                string path = AssetDatabase.GUIDToAssetPath(group.targetTreeGuid);
                BehaviourTreeAssetBase tree = AssetDatabase.LoadAssetAtPath<BehaviourTreeAssetBase>(path);
                return tree != null && tree.BlackboardDefinition == definition;
            }

            return false;
        }
    }
}
