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

        /// <summary>
        /// Roles available in this squad (e.g. "Scout", "Flanker", "Defender").
        /// Agents pick one role. Commander trees iterate over roles.
        /// </summary>
        public List<string> availableRoles = new List<string>();

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
    }
}
