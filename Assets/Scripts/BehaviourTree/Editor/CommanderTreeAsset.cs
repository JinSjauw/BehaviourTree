using System.Collections.Generic;
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
        /// <summary>
        /// The squad this commander uses to order its own agents.
        /// Distinct from squadConnections (which lists compatible squads the tree can join).
        /// Used by role dropdowns, variable binding resolution, and runtime agent communication.
        /// </summary>
        public SquadDefinition commanderSquad;

        /// <summary>Maximum number of agents this commander can lead. Drives stride on squad-data variables.</summary>
        [SerializeField, Min(1)] private int maxSquadSize = 8;
        public int MaxSquadSize
        {
            get => maxSquadSize;
            set
            {
                maxSquadSize = Mathf.Max(1, value);
                SyncStrideToBlackboard();
            }
        }

        /// <summary>Commander trees need the actual stride so baked slot offsets match storage.</summary>
        public override bool PreserveCommanderStride => true;

        public override void CreateBlackBoard()
        {
            if (commanderBlackboardDefinition != null)
            {
                // Existing commander tree — bridge to the base field
                blackboardDefinition = commanderBlackboardDefinition;
                EnsureCommanderChannels(blackboardDefinition);
                SyncStrideToBlackboard();
                EditorUtility.SetDirty(this);
                AssetDatabase.SaveAssets();
                return;
            }

            BlackboardDefinition created = CreateInstance<BlackboardDefinition>();
            created.name = name + "_CommanderBB";

            commanderBlackboardDefinition = created;
            blackboardDefinition = created;
            EnsureCommanderChannels(created);
            SyncStrideToBlackboard();
            AssetDatabase.AddObjectToAsset(created, this);
            AssetDatabase.SaveAssets();
        }

        private void OnValidate()
        {
            maxSquadSize = Mathf.Max(1, maxSquadSize);

            BlackboardDefinition bbDef = blackboardDefinition ?? commanderBlackboardDefinition;
            if (bbDef == null) return;

            if (BlackboardDefinition.EnsureBaseChannel<int>(bbDef, "AgentRoles", isSquadData: true)
                | BlackboardDefinition.EnsureBaseChannel<int>(bbDef, "AgentOrders", isSquadData: true))
            {
                EditorUtility.SetDirty(bbDef);
            }

            SyncStrideToBlackboard();
        }

        /// <summary>
        /// Syncs the stride of all squad-data variables in the commander BB definition
        /// to match maxSquadSize. Called from the setter, OnValidate, and CreateBlackBoard
        /// so baked slot offsets never diverge from the configured squad size.
        /// </summary>
        private void SyncStrideToBlackboard()
        {
            BlackboardDefinition bbDef = blackboardDefinition ?? commanderBlackboardDefinition;
            if (bbDef == null) return;

            IReadOnlyList<BlackboardVariableBase> vars = bbDef.GetAllVariables();
            bool changed = false;
            for (int i = 0; i < vars.Count; i++)
            {
                if (vars[i].isSquadData && vars[i].Stride != maxSquadSize)
                {
                    vars[i].Stride = maxSquadSize;
                    changed = true;
                }
            }
            if (changed)
                EditorUtility.SetDirty(bbDef);
        }

        private static void EnsureCommanderChannels(BlackboardDefinition bbDef)
        {
            BlackboardDefinition.EnsureBaseChannel<int>(bbDef, "AgentRoles", isSquadData: true);
            BlackboardDefinition.EnsureBaseChannel<int>(bbDef, "AgentOrders", isSquadData: true);
            EditorUtility.SetDirty(bbDef);
        }
    }
}
