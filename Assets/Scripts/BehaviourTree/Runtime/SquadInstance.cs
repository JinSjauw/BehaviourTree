using System.Collections.Generic;
using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Runtime
{
    /// <summary>
    /// Runtime MonoBehaviour that holds a squad's own BlackBoard and handles
    /// bidirectional data copying between the squad BB and any connected tree's BB.
    /// 
    /// Bindings are resolved lazily: when a tree registers with this squad,
    /// EnsureResolved() matches the tree's BlackboardDefinition against the
    /// SquadDefinition's binding groups to build flat [srcSlot, dstSlot] copy arrays.
    /// </summary>
    [RequireComponent(typeof(BlackBoard))]
    public class SquadInstance : MonoBehaviour
    {
        [SerializeField] private SquadDefinition definition;
        [SerializeField] private BlackBoard blackBoard;

        /// <summary>Per-tree-definition cache for squad→tree copy pairs: [squadSlot, treeSlot, ...].</summary>
        private Dictionary<BlackboardDefinition, int[]> copyToCache;

        /// <summary>Per-tree-definition cache for tree→squad copy pairs: [treeSlot, squadSlot, ...].</summary>
        private Dictionary<BlackboardDefinition, int[]> copyFromCache;

        public BlackBoard BlackBoard => blackBoard;
        public SquadDefinition Definition => definition;

        /// <summary>
        /// Initializes the squad's own BlackBoard from the SquadDefinition's schema.
        /// </summary>
        public void Initialize(SquadDefinition squadDefinition)
        {
            definition = squadDefinition;
            if (blackBoard == null)
                blackBoard = GetComponent<BlackBoard>();

            if (definition != null)
                blackBoard.Initialize(definition.blackboardDefinition);

            copyToCache = new Dictionary<BlackboardDefinition, int[]>();
            copyFromCache = new Dictionary<BlackboardDefinition, int[]>();
        }

        /// <summary>
        /// Resolves variable bindings for a tree, identified by its BlackboardDefinition.
        /// Idempotent — subsequent calls with the same definition are no-ops.
        /// 
        /// Matches the treeDef against SquadBindingGroups by comparing the tree asset's
        /// BlackboardDefinition with the given treeDef.
        /// </summary>
        public void EnsureResolved(BlackboardDefinition treeDef)
        {
            if (copyToCache.ContainsKey(treeDef))
                return;

            if (definition == null || definition.bindingGroups == null)
            {
                copyToCache[treeDef] = new int[0];
                copyFromCache[treeDef] = new int[0];
                return;
            }

            // Find the SquadBindingGroup whose tree asset owns this BlackboardDefinition
            SquadBindingGroup matchedGroup = null;
            for (int i = 0; i < definition.bindingGroups.Count; i++)
            {
                SquadBindingGroup group = definition.bindingGroups[i];
                if (group.treeAsset != null && group.treeAsset.BlackboardDefinition == treeDef)
                {
                    matchedGroup = group;
                    break;
                }
            }

            if (matchedGroup == null || matchedGroup.bindings == null || matchedGroup.bindings.Count == 0)
            {
                copyToCache[treeDef] = new int[0];
                copyFromCache[treeDef] = new int[0];
                return;
            }

            BlackboardDefinition squadDef = definition.blackboardDefinition;
            if (squadDef == null)
            {
                copyToCache[treeDef] = new int[0];
                copyFromCache[treeDef] = new int[0];
                return;
            }

            List<int> toTree = new List<int>();
            List<int> fromTree = new List<int>();

            for (int i = 0; i < matchedGroup.bindings.Count; i++)
            {
                VariableBinding binding = matchedGroup.bindings[i];
                if (binding == null) continue;

                int squadVarIndex = squadDef.GetVariableIndex(binding.squadVariableName);
                int treeVarIndex = treeDef.GetVariableIndex(binding.treeVariableName);

                if (squadVarIndex < 0 || treeVarIndex < 0)
                    continue;

                int squadBaseSlot = ComputeBaseSlot(squadDef, squadVarIndex);
                int treeBaseSlot = ComputeBaseSlot(treeDef, treeVarIndex);

                // FromSquad: squad → tree
                if (binding.direction == BindingDirection.FromSquad || binding.direction == BindingDirection.Both)
                {
                    toTree.Add(squadBaseSlot);
                    toTree.Add(treeBaseSlot);
                }

                // ToSquad: tree → squad
                if (binding.direction == BindingDirection.ToSquad || binding.direction == BindingDirection.Both)
                {
                    fromTree.Add(treeBaseSlot);
                    fromTree.Add(squadBaseSlot);
                }
            }

            copyToCache[treeDef] = toTree.ToArray();
            copyFromCache[treeDef] = fromTree.ToArray();
        }

        /// <summary>
        /// Copies squad BB values to the given tree's BB, respecting binding directions.
        /// Only copies FromSquad and Both bindings.
        /// </summary>
        public void CopyToBB(BlackBoard treeBB, BlackboardDefinition treeDef)
        {
            if (!copyToCache.TryGetValue(treeDef, out int[] pairs) || pairs.Length == 0)
                return;

            for (int i = 0; i < pairs.Length; i += 2)
                treeBB.SetBoxed(pairs[i + 1], blackBoard.GetBoxed(pairs[i]));
        }

        /// <summary>
        /// Copies tree BB values back to the squad BB, respecting binding directions.
        /// Only copies ToSquad and Both bindings.
        /// </summary>
        public void CopyFromBB(BlackBoard treeBB, BlackboardDefinition treeDef)
        {
            if (!copyFromCache.TryGetValue(treeDef, out int[] pairs) || pairs.Length == 0)
                return;

            for (int i = 0; i < pairs.Length; i += 2)
                blackBoard.SetBoxed(pairs[i + 1], treeBB.GetBoxed(pairs[i]));
        }

        /// <summary>
        /// Computes the base slot offset for a variable at the given index
        /// by summing strides of all preceding variables.
        /// </summary>
        private static int ComputeBaseSlot(BlackboardDefinition def, int variableIndex)
        {
            if (def == null) return 0;

            IReadOnlyList<BlackboardVariableBase> vars = def.GetAllVariables();
            int baseSlot = 0;
            for (int i = 0; i < variableIndex && i < vars.Count; i++)
            {
                if (vars[i] == null) continue;
                int stride = vars[i].Stride;
                baseSlot += (stride > 1) ? stride : 1;
            }
            return baseSlot;
        }
    }
}
