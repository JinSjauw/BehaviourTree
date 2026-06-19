using System;
using System.Collections.Generic;
using System.Reflection;
using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Runtime
{
    [RequireComponent(typeof(BlackBoard))]
    public class AgentTreeRunner : BehaviourTreeRunnerBase
    {
        private bool runIndependently = true;
        public bool RunIndependently
        {
            get => runIndependently;
            set => runIndependently = value;
        }

        private List<IBlackboardDataProvider> dataProviders;

        /// <summary>User-curated list of tracked bindings grouped by tree asset.
        /// Only the group matching the currently active tree is resolved and pushed.</summary>
        [SerializeField] public List<TrackedBindingGroup> trackedBindingGroups = new();

        /// <summary>Runtime cache of resolved bindings for the active tree group. Populated in Initialize().</summary>
        [NonSerialized] private List<TrackedBinding> trackedBindingsToPush;

        private void Start()
        {
            if (!runIndependently) return;
            Initialize();
        }

        private void Update()
        {
            if (!runIndependently) return;

            PushDataProviders();
            PushTrackedBindings();
            Evaluate();
        }

        private void PushDataProviders()
        {
            if (dataProviders == null) return;

            for (int i = 0; i < dataProviders.Count; i++)
            {
                dataProviders[i].ProvideData(blackBoard);
            }
        }

        protected override void OnPostInitialize()
        {
            // Collect all data providers on this GameObject
            IBlackboardDataProvider[] providers = GetComponentsInChildren<IBlackboardDataProvider>();
            dataProviders = new List<IBlackboardDataProvider>(providers);

            ResolveTrackedBindings();
        }

        /// <summary>
        /// Resolves cached FieldInfo/PropertyInfo and variable indices for tracked bindings
        /// belonging to the group that matches the currently active tree asset.
        /// Called once during Initialize() after the blackboard is baked.
        /// </summary>
        private void ResolveTrackedBindings()
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

                // Match by GUID — works in both editor and build
                if (!string.IsNullOrEmpty(activeGuid) && group.targetTreeGuid == activeGuid)
                {
                    activeBindings = group.bindings;
                    break;
                }

#if UNITY_EDITOR
                // Fallback: match by direct reference (old data without GUIDs)
                if (activeBindings == null && authoringAsset != null && group.targetTree == authoringAsset)
                    activeBindings = group.bindings;
#endif

                // Fallback: use first group with no identity set (very old data)
#if UNITY_EDITOR
                if (activeBindings == null && group.targetTree == null && string.IsNullOrEmpty(group.targetTreeGuid))
#else
                if (activeBindings == null && string.IsNullOrEmpty(group.targetTreeGuid))
#endif
                    activeBindings = group.bindings;
            }

            // Last resort: use the first group
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

                binding.variableIndex = blackboardDefinition.GetVariableIndex(binding.blackboardVariableName);

                trackedBindingsToPush.Add(binding);
            }
        }

        /// <summary>
        /// Pushes current values from resolved tracked bindings into the blackboard.
        /// Called every frame in Update() after PushDataProviders().
        /// Only pushes bindings from the group matching the active tree asset.
        /// </summary>
        private void PushTrackedBindings()
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
    }
}
