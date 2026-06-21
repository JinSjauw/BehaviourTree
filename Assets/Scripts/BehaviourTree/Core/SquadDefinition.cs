using System;
using System.Collections.Generic;
using UnityEngine;

namespace BehaviourTree.Core
{
    /// <summary>
    /// Direction of data flow for a variable binding between a tree and a squad.
    /// </summary>
    public enum BindingDirection
    {
        ToSquad,    // tree → squad
        FromSquad,  // squad → tree
        Both        // bidirectional
    }

    /// <summary>
    /// A single variable mapping between a tree's blackboard and a squad's blackboard.
    /// Lives on SquadDefinition inside a SquadBindingGroup.
    /// </summary>
    [Serializable]
    public class VariableBinding
    {
        /// <summary>Variable name in the tree's BlackboardDefinition.</summary>
        public string treeVariableName;

        /// <summary>Variable name in the squad's BlackboardDefinition.</summary>
        public string squadVariableName;

        /// <summary>Direction of data flow.</summary>
        public BindingDirection direction = BindingDirection.Both;
    }

    /// <summary>
    /// Groups all variable bindings between one specific tree asset and this squad.
    /// One group exists per tree that connects to the squad.
    /// </summary>
    [Serializable]
    public class SquadBindingGroup
    {
        /// <summary>The tree asset this binding group maps to.</summary>
        public BehaviourTreeAssetBase treeAsset;

        /// <summary>GUID of the tree asset. Populated in OnValidate for build-safe matching.</summary>
        public string treeAssetGuid;

        /// <summary>Variable bindings between the tree and the squad.</summary>
        public List<VariableBinding> bindings = new List<VariableBinding>();
    }

    /// <summary>
    /// ScriptableObject that defines a squad: its shared data schema (blackboard),
    /// available role names, and per-tree variable bindings.
    /// </summary>
    [CreateAssetMenu(menuName = "BehaviourTree/Squad Definition")]
    public class SquadDefinition : ScriptableObject
    {
        /// <summary>The squad's own blackboard schema. Defines all shared squad data variables.</summary>
        public BlackboardDefinition blackboardDefinition;

        /// <summary>Maximum number of agents this squad can hold. Drives stride on all squad-data variables.</summary>
        [SerializeField, Min(1)] private int maxAgents = 8;
        public int MaxAgents => maxAgents;

        /// <summary>
        /// Roles available in this squad, with colour, max amount, and fallback settings.
        /// Agents pick one role. Commander trees iterate over roles.
        /// </summary>
        public List<SquadRole> availableRoles = new List<SquadRole>();

        /// <summary>
        /// One binding group per tree that connects to this squad.
        /// Keyed by treeAsset — both the squad inspector and the tree's squads tab
        /// edit the same data through this list.
        /// </summary>
        public List<SquadBindingGroup> bindingGroups = new List<SquadBindingGroup>();

        /// <summary>
        /// Finds or creates a binding group for the given tree asset.
        /// </summary>
        public SquadBindingGroup GetOrCreateBindingGroup(BehaviourTreeAssetBase treeAsset)
        {
            for (int i = 0; i < bindingGroups.Count; i++)
            {
                if (bindingGroups[i].treeAsset == treeAsset)
                    return bindingGroups[i];
            }

            SquadBindingGroup newGroup = new SquadBindingGroup
            {
                treeAsset = treeAsset
            };
#if UNITY_EDITOR
            string path = UnityEditor.AssetDatabase.GetAssetPath(treeAsset);
            newGroup.treeAssetGuid = UnityEditor.AssetDatabase.AssetPathToGUID(path);
#endif
            bindingGroups.Add(newGroup);
            return newGroup;
        }

        /// <summary>
        /// Returns the binding group for a tree, or null if not connected.
        /// </summary>
        public SquadBindingGroup GetBindingGroup(BehaviourTreeAssetBase treeAsset)
        {
            for (int i = 0; i < bindingGroups.Count; i++)
            {
                if (bindingGroups[i].treeAsset == treeAsset)
                    return bindingGroups[i];
            }
            return null;
        }

        /// <summary>
        /// Applies maxAgents as the stride to all squad-data variables in the blackboard definition.
        /// Called by OnValidate (editor) and SquadInstance.Initialize (runtime) to ensure
        /// per-agent storage is correctly sized regardless of serialized stride values.
        /// The YAML stays at stride=1 intentionally — definitions are editor schemas;
        /// stride is a runtime sizing concern applied before BB initialization.
        /// </summary>
        public void EnsureStrideApplied()
        {
            if (blackboardDefinition == null) return;

            IReadOnlyList<BlackboardVariableBase> vars = blackboardDefinition.GetAllVariables();
            for (int i = 0; i < vars.Count; i++)
            {
                if (vars[i].isSquadData)
                    vars[i].Stride = maxAgents;
            }
        }

        private void OnValidate()
        {
            maxAgents = Mathf.Max(1, maxAgents);

            // Ensure squad BB has base channel variables
            if (blackboardDefinition != null)
            {
                BlackboardDefinition.EnsureBaseChannel<int>(blackboardDefinition, "AgentRoles",
                    isSquadData: true);
                BlackboardDefinition.EnsureBaseChannel<int>(blackboardDefinition, "AgentOrders",
                    isSquadData: true);

                EnsureStrideApplied();
            }

            // Ensure auto-bindings for each connected tree
            if (bindingGroups != null)
            {
                for (int groupIndex = 0; groupIndex < bindingGroups.Count; groupIndex++)
                {
                    SquadBindingGroup group = bindingGroups[groupIndex];
                    if (group?.treeAsset == null) continue;

                    bool isCommander = group.treeAsset.CommanderBlackboardDefinition != null;
                    string roleTreeVar = isCommander ? "AgentRoles" : "AgentAssignedRole";
                    string orderTreeVar = isCommander ? "AgentOrders" : "AgentReceivedOrder";
                    BindingDirection orderDir = isCommander
                        ? BindingDirection.Both
                        : BindingDirection.FromSquad;

                    EnsureBinding(group, "AgentRoles", roleTreeVar, BindingDirection.FromSquad);
                    EnsureBinding(group, "AgentOrders", orderTreeVar, orderDir);
                }
            }

#if UNITY_EDITOR
            // Backfill GUID on binding groups for build-safe matching
            if (bindingGroups != null)
            {
                for (int groupIndex = 0; groupIndex < bindingGroups.Count; groupIndex++)
                {
                    SquadBindingGroup group = bindingGroups[groupIndex];
                    if (group?.treeAsset != null && string.IsNullOrEmpty(group.treeAssetGuid))
                    {
                        string path = UnityEditor.AssetDatabase.GetAssetPath(group.treeAsset);
                        group.treeAssetGuid = UnityEditor.AssetDatabase.AssetPathToGUID(path);
                    }
                }
            }
#endif
        }

        private static void EnsureBinding(
            SquadBindingGroup group, string squadVar, string treeVar, BindingDirection direction)
        {
            if (group.bindings == null)
                group.bindings = new List<VariableBinding>();

            for (int bindingIndex = 0; bindingIndex < group.bindings.Count; bindingIndex++)
            {
                VariableBinding binding = group.bindings[bindingIndex];
                if (binding != null && binding.squadVariableName == squadVar)
                {
                    // Update existing binding if values differ
                    if (binding.treeVariableName != treeVar || binding.direction != direction)
                    {
                        binding.treeVariableName = treeVar;
                        binding.direction = direction;
                        Debug.LogWarning(
                            $"[BaseChannel] Repaired binding: squad.{squadVar} → tree.{treeVar} ({direction}).");
                    }
                    return;
                }
            }

            group.bindings.Add(new VariableBinding
            {
                squadVariableName = squadVar,
                treeVariableName = treeVar,
                direction = direction
            });
            Debug.LogWarning(
                $"[BaseChannel] Auto-created binding: squad.{squadVar} → tree.{treeVar} ({direction}).");
        }
    }
}
