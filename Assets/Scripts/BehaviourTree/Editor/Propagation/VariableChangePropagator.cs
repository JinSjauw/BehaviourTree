using System;
using System.Collections.Generic;
using BehaviourTree.Core;
using BehaviourTree.Runtime;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BehaviourTree.Editor.Propagation
{
    public class PropagationContext
    {
        public BlackboardDefinition Definition;
        public BehaviourTreeAssetBase[] MatchingTrees;
        public BehaviourTreeRunnerBase[] Runners;
        public SquadDefinition[] SquadDefs;
    }

    public class VariableChangePropagator
    {
        public static event Action ChangesFlushed;

        private readonly List<IVariableChangeHandler> handlers = new();
        private PropagationContext cachedContext;
        private BlackboardDefinition cachedDefinition;
        private bool contextValid;

        public void Register(IVariableChangeHandler handler)
        {
            handlers.Add(handler);
        }

        /// <summary>
        /// Sets the definition being edited. Invalidates the cached context if the
        /// definition changed, so the next change builds a fresh context.
        /// </summary>
        public void SetDefinition(BlackboardDefinition definition)
        {
            if (cachedDefinition != definition)
                contextValid = false;
            cachedDefinition = definition;
        }

        public void Rename(string oldName, string newName)
        {
            EnsureContext();
            for (int i = 0; i < handlers.Count; i++)
                handlers[i].HandleRename(cachedContext, oldName, newName);
        }

        public void Delete(string variableName, string variableTypeName)
        {
            EnsureContext();
            for (int i = 0; i < handlers.Count; i++)
                handlers[i].HandleDelete(cachedContext, variableName, variableTypeName);
        }

        public void TypeChange(string variableName, string oldTypeName, string newTypeName)
        {
            EnsureContext();
            for (int i = 0; i < handlers.Count; i++)
                handlers[i].HandleTypeChange(cachedContext, variableName, oldTypeName, newTypeName);
        }

        /// <summary>
        /// Flushes the batch: fires the VariableRenamed event so UI tabs can refresh,
        /// then invalidates the cached context.
        /// </summary>
        public void Flush()
        {
            if (contextValid)
                ChangesFlushed?.Invoke();
            contextValid = false;
            cachedContext = null;
        }

        private void EnsureContext()
        {
            if (contextValid) return;

            cachedContext = new PropagationContext
            {
                Definition = cachedDefinition,
                MatchingTrees = FindMatchingTrees(cachedDefinition),
                Runners = Object.FindObjectsByType<BehaviourTreeRunnerBase>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None),
                SquadDefs = FindAllSquadDefs()
            };
            contextValid = true;
        }

        private static BehaviourTreeAssetBase[] FindMatchingTrees(BlackboardDefinition definition)
        {
            string[] guids = AssetDatabase.FindAssets("t:BehaviourTreeAssetBase");
            List<BehaviourTreeAssetBase> results = new();
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                BehaviourTreeAssetBase tree = AssetDatabase.LoadAssetAtPath<BehaviourTreeAssetBase>(path);
                if (tree != null && tree.BlackboardDefinition == definition)
                    results.Add(tree);
            }
            return results.ToArray();
        }

        private static SquadDefinition[] FindAllSquadDefs()
        {
            string[] guids = AssetDatabase.FindAssets("t:SquadDefinition");
            List<SquadDefinition> results = new();
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                SquadDefinition squad = AssetDatabase.LoadAssetAtPath<SquadDefinition>(path);
                if (squad != null)
                    results.Add(squad);
            }
            return results.ToArray();
        }
    }
}
