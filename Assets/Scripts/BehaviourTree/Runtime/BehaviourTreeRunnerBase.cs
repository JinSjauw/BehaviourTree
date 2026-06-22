using System;
using System.Collections.Generic;
using System.Reflection;
using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Runtime
{
    /// <summary>
    /// Shared base class for AgentTreeRunner and CommanderTreeRunner.
    /// Handles baking, BB initialization, evaluator creation, and debug provider setup.
    /// Also owns tracked bindings — component field/property → blackboard variable mappings.
    /// </summary>
    [RequireComponent(typeof(BlackBoard))]
    public abstract class BehaviourTreeRunnerBase : MonoBehaviour
    {
        [SerializeField] protected BlackBoard blackBoard;

        /// <summary>Public accessor for the blackboard. Used by squad copy helpers.</summary>
        public BlackBoard BlackBoard => blackBoard;
        [SerializeField] protected RuntimeBehaviourTreeAsset runtimeAsset;
#if UNITY_EDITOR
        [SerializeField] protected BehaviourTreeAssetBase authoringAsset;
#endif

        protected TreeEvaluator evaluator;
        protected RuntimeDebugProvider debugProvider;
        protected bool initialized;

        /// <summary>User-curated list of tracked bindings grouped by tree asset.
        /// Only the group matching the currently active tree is resolved and pushed.</summary>
        [SerializeField] public List<TrackedBindingGroup> trackedBindingGroups = new();

        /// <summary>Runtime cache of resolved bindings for the active tree group. Populated in Initialize().</summary>
        [NonSerialized] private List<TrackedBinding> trackedBindingsToPush;

        /// <summary>
        /// Template Method: bakes/copies the runtime asset, initializes the BB,
        /// creates the evaluator and debug provider, then calls OnPostInitialize().
        /// </summary>
        public virtual void Initialize()
        {
            if (initialized) return;

            runtimeAsset = RuntimeAssetHelper.GetOrBake(runtimeAsset,
#if UNITY_EDITOR
                authoringAsset,
#else
                null,
#endif
                GetType().Name);
            if (runtimeAsset == null) return;

            if (blackBoard == null)
            {
                Debug.LogError($"[{GetType().Name}] BlackBoard is null.");
                return;
            }

            blackBoard.Initialize(runtimeAsset.blackboardDefinition);
#if UNITY_EDITOR
            runtimeAsset.blackboardDefinition.sourceTreeAsset = runtimeAsset.sourceTree as BehaviourTreeAssetBase;
#endif
            runtimeAsset.blackboardDefinition.sourceTreeGuid = runtimeAsset.sourceTreeGuid;
            evaluator = new TreeEvaluator(runtimeAsset.runtimeNodeData, runtimeAsset.runtimeFieldData, runtimeAsset.fieldTypeNames, runtimeAsset.boxedConstants, runtimeAsset.maxTreeDepth);

            debugProvider = GetComponent<RuntimeDebugProvider>();
            if (debugProvider == null) debugProvider = gameObject.AddComponent<RuntimeDebugProvider>();

            OnPostInitialize();
            initialized = true;
        }

        /// <summary>Subclass hook called after base initialization is complete.</summary>
        protected virtual void OnPostInitialize() { }

        /// <summary>Evaluates the tree once against the blackboard.</summary>
        public void Evaluate()
        {
            if (evaluator == null || blackBoard == null) return;

            evaluator.Evaluate(blackBoard);

            if (debugProvider != null)
            {
                debugProvider.currentNodeStates = evaluator.nodeStates;
                debugProvider.activeNodeIndex = evaluator.currentNodeIndex;
                debugProvider.currentNodeGuids = runtimeAsset != null ? runtimeAsset.runtimeNodeGuids : null;
            }
        }

        /// <summary>
        /// Resolves cached FieldInfo/PropertyInfo and variable indices for tracked bindings
        /// belonging to the group that matches the currently active tree asset.
        /// Called once during Initialize() after the blackboard is baked.
        /// </summary>
        protected void ResolveTrackedBindings()
        {
            if (trackedBindingGroups == null || trackedBindingGroups.Count == 0) return;

            BlackboardDefinition blackboardDefinition = blackBoard?.Definition;
            if (blackboardDefinition == null) return;

            string activeGuid = runtimeAsset?.sourceTreeGuid;

            List<TrackedBinding> activeBindings = null;

            for (int i = 0; i < trackedBindingGroups.Count; i++)
            {
                TrackedBindingGroup group = trackedBindingGroups[i];
                if (group == null || group.bindings == null) continue;

                if (!string.IsNullOrEmpty(activeGuid) && group.targetTreeGuid == activeGuid)
                {
                    activeBindings = group.bindings;
                    break;
                }

#if UNITY_EDITOR
                if (activeBindings == null && authoringAsset != null && group.targetTree == authoringAsset)
                    activeBindings = group.bindings;
#endif

#if UNITY_EDITOR
                if (activeBindings == null && group.targetTree == null && string.IsNullOrEmpty(group.targetTreeGuid))
#else
                if (activeBindings == null && string.IsNullOrEmpty(group.targetTreeGuid))
#endif
                    activeBindings = group.bindings;
            }

            if (activeBindings == null && trackedBindingGroups.Count > 0)
                activeBindings = trackedBindingGroups[0].bindings;

            if (activeBindings == null || activeBindings.Count == 0) return;

            trackedBindingsToPush = new List<TrackedBinding>();

            for (int i = 0; i < activeBindings.Count; i++)
            {
                TrackedBinding binding = activeBindings[i];
                if (binding.targetComponent == null) continue;

                Type componentType = binding.targetComponent.GetType();

                if (binding.isProperty)
                {
                    binding.cachedPropertyInfo = componentType.GetProperty(binding.memberName,
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                }
                else
                {
                    binding.cachedFieldInfo = componentType.GetField(binding.memberName,
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                }

                int varIndex = blackboardDefinition.GetVariableIndex(binding.blackboardVariableName);
                binding.variableIndex = ComputeSlotOffset(blackboardDefinition, varIndex);

                trackedBindingsToPush.Add(binding);
            }
        }

        /// <summary>
        /// Pushes current values from resolved tracked bindings into the blackboard.
        /// Called every frame before tree evaluation.
        /// Only pushes bindings from the group matching the active tree asset.
        /// </summary>
        internal void PushTrackedBindings()
        {
            if (trackedBindingsToPush == null || trackedBindingsToPush.Count == 0) return;
            if (blackBoard == null) return;

            for (int i = 0; i < trackedBindingsToPush.Count; i++)
            {
                TrackedBinding binding = trackedBindingsToPush[i];
                if (binding.variableIndex < 0 || binding.targetComponent == null) continue;

                object value = binding.isProperty
                    ? binding.cachedPropertyInfo?.GetValue(binding.targetComponent)
                    : binding.cachedFieldInfo?.GetValue(binding.targetComponent);

                blackBoard.SetBoxed(binding.variableIndex, value);
            }
        }

        /// <summary>
        /// Converts a variable index (position in the definition's variable list) to a slot offset
        /// in the flat blackboard storage, accounting for stride of all preceding variables.
        /// </summary>
        private static int ComputeSlotOffset(BlackboardDefinition definition, int varIndex)
        {
            if (varIndex < 0) return -1;

            IReadOnlyList<BlackboardVariableBase> vars = definition.GetAllVariables();
            int slotOffset = 0;
            for (int i = 0; i < varIndex && i < vars.Count; i++)
            {
                int stride = vars[i].Stride;
                slotOffset += (stride > 1) ? stride : 1;
            }
            return slotOffset;
        }

        protected virtual void OnDestroy()
        {
            if (runtimeAsset == null) return;
            Destroy(runtimeAsset);
            runtimeAsset = null;
        }

        protected virtual void OnDisable()
        {
            initialized = false;
        }

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            if (runtimeAsset != null)
            {
                blackBoard?.BuildSerializedReferences(runtimeAsset.blackboardDefinition);
            }
            else
            {
                BehaviourTreeAssetBase authoring = authoringAsset;
                if (authoring != null)
                {
                    blackBoard?.BuildSerializedReferences(authoring.BlackboardDefinition);
                }
            }

            if (runtimeAsset == null && authoringAsset == null)
            {
                blackBoard?.ClearSerializedReferences();
            }
        }

        public UnityEngine.Object GetSourceTree()
        {
            if (runtimeAsset != null && runtimeAsset.sourceTree != null) return runtimeAsset.sourceTree;
            return authoringAsset ?? null;
        }
#endif
    }
}
