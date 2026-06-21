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
    /// SquadDefinition's binding groups to build flat copy arrays.
    /// 
    /// Copy arrays use triplets: [srcSlot, dstSlot, stride]. Stride > 1 means
    /// the squad-side slot is per-agent data and needs an agentOffset applied.
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
            {
                // OnValidate handles stride in editor but NOT at runtime.
                // Serialized stride defaults to 1 — we must apply maxAgents stride
                // before initializing the BB so per-agent storage is sized correctly.
                definition.EnsureStrideApplied();
                blackBoard.Initialize(definition.blackboardDefinition);
            }

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
                if (group.treeAsset == null) continue;

                // Editor: fast path via ScriptableObject reference equality
                if (treeDef.sourceTreeAsset != null && group.treeAsset == treeDef.sourceTreeAsset)
                {
                    matchedGroup = group;
                    break;
                }
                // Build / baked: fall back to GUID matching
                if (!string.IsNullOrEmpty(group.treeAssetGuid) &&
                    group.treeAssetGuid == treeDef.sourceTreeGuid)
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

                IReadOnlyList<BlackboardVariableBase> squadVars = squadDef.GetAllVariables();
                int squadStride = 1;
                if (squadVarIndex >= 0 && squadVarIndex < squadVars.Count)
                {
                    BlackboardVariableBase squadVar = squadVars[squadVarIndex];
                    squadStride = (squadVar.Stride > 1) ? squadVar.Stride : 1;
                }

                // FromSquad: squad → tree (triplet: srcSlot, dstSlot, stride)
                if (binding.direction == BindingDirection.FromSquad || binding.direction == BindingDirection.Both)
                {
                    toTree.Add(squadBaseSlot);
                    toTree.Add(treeBaseSlot);
                    toTree.Add(squadStride);
                }

                // ToSquad: tree → squad (triplet: srcSlot, dstSlot, stride)
                if (binding.direction == BindingDirection.ToSquad || binding.direction == BindingDirection.Both)
                {
                    fromTree.Add(treeBaseSlot);
                    fromTree.Add(squadBaseSlot);
                    fromTree.Add(squadStride);
                }
            }

            copyToCache[treeDef] = toTree.ToArray();
            copyFromCache[treeDef] = fromTree.ToArray();
        }

        /// <summary>
        /// Copies squad BB values to the given tree's BB, respecting binding directions.
        /// Only copies FromSquad and Both bindings.
        /// When agentOffset is >= 0, squad-side slots for stride > 1 variables are offset
        /// by agentOffset to read the correct per-agent data (agent tree, stride=1 on tree side).
        /// When agentOffset is -1 (commander case), all stride slots are copied —
        /// both sides have stride > 1 and need a full sync.
        /// </summary>
        public void CopyToBB(BlackBoard treeBB, BlackboardDefinition treeDef, int agentOffset = -1)
        {
            if (!copyToCache.TryGetValue(treeDef, out int[] triplets) || triplets.Length == 0)
                return;

            for (int i = 0; i < triplets.Length; i += 3)
            {
                int srcSlot = triplets[i];
                int dstSlot = triplets[i + 1];
                int stride = triplets[i + 2];

                if (agentOffset >= 0)
                {
                    // Per-agent copy: offset only the squad-side slot
                    int offset = (stride > 1 && agentOffset < stride) ? agentOffset : 0;
                    treeBB.SetBoxed(dstSlot, blackBoard.GetBoxed(srcSlot + offset));
                }
                else if (stride > 1)
                {
                    // Commander sync: copy all stride slots (both sides have stride > 1)
                    for (int j = 0; j < stride; j++)
                        treeBB.SetBoxed(dstSlot + j, blackBoard.GetBoxed(srcSlot + j));
                }
                else
                {
                    treeBB.SetBoxed(dstSlot, blackBoard.GetBoxed(srcSlot));
                }
            }
        }

        /// <summary>
        /// Copies tree BB values back to the squad BB, respecting binding directions.
        /// Only copies ToSquad and Both bindings.
        /// When agentOffset is >= 0, squad-side slots for stride > 1 variables are offset
        /// by agentOffset to write to the correct per-agent slot (agent tree, stride=1 on tree side).
        /// When agentOffset is -1 (commander case), all stride slots are copied —
        /// both sides have stride > 1 and need a full sync.
        /// </summary>
        public void CopyFromBB(BlackBoard treeBB, BlackboardDefinition treeDef, int agentOffset = -1)
        {
            if (!copyFromCache.TryGetValue(treeDef, out int[] triplets) || triplets.Length == 0)
                return;

            for (int i = 0; i < triplets.Length; i += 3)
            {
                int srcSlot = triplets[i];
                int dstSlot = triplets[i + 1];
                int stride = triplets[i + 2];

                if (agentOffset >= 0)
                {
                    // Per-agent copy: offset only the squad-side slot
                    int offset = (stride > 1 && agentOffset < stride) ? agentOffset : 0;
                    blackBoard.SetBoxed(dstSlot + offset, treeBB.GetBoxed(srcSlot));
                }
                else if (stride > 1)
                {
                    // Commander sync: copy all stride slots (both sides have stride > 1)
                    for (int j = 0; j < stride; j++)
                        blackBoard.SetBoxed(dstSlot + j, treeBB.GetBoxed(srcSlot + j));
                }
                else
                {
                    blackBoard.SetBoxed(dstSlot, treeBB.GetBoxed(srcSlot));
                }
            }
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
