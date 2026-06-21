using System.Collections.Generic;
using System.Reflection;
using BehaviourTree.Core;
using BehaviourTree.Runtime;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace BehaviourTree.Editor
{
    public class NodeSearchProvider : ScriptableObject, ISearchWindowProvider
    {
        private List<string> actionMethods;
        private List<string> conditionMethods;
        private List<string> decoratorMethods;
        private List<string> compositeMethods;
        private List<SearchTreeEntry> cachedSearchTree;
        private BehaviourTreeEditorGraphView graphView;
        private Vector2 creationPosition;
        private Port pendingConnectionPort;
        private Texture2D identationIcon;

        /// <summary>Set before opening the search window to enable per-tree-type node filtering.</summary>
        public BaseEditorTreeAsset currentTreeAsset;

        /// <summary>Tracks the last tree type we built method lists for, so we rebuild on change.</summary>
        private AllowedTreeType lastBuiltTreeType = AllowedTreeType.Any;

        private void OnDestroy()
        {
            if (identationIcon != null)
            {
                DestroyImmediate(identationIcon);
                identationIcon = null;
            }
        }

        public void Initialize(BehaviourTreeEditorGraphView sourceGraphView)
        {
            graphView = sourceGraphView;
            cachedSearchTree = null;

            identationIcon = new Texture2D(1, 1);
            identationIcon.SetPixel(0, 0, Color.clear);
            identationIcon.Apply();

            BuildMethodLists();
            MethodRegistry.OnRegistryRebuilt -= OnRegistryRebuilt;
            MethodRegistry.OnRegistryRebuilt += OnRegistryRebuilt;
        }

        private void OnRegistryRebuilt()
        {
            cachedSearchTree = null;
            BuildMethodLists();
        }

        private void BuildMethodLists()
        {
            actionMethods = new List<string>();
            conditionMethods = new List<string>();
            decoratorMethods = new List<string>();
            compositeMethods = new List<string>();

            // Determine which tree type to filter for
            AllowedTreeType allowedFor = currentTreeAsset is CommanderTreeAsset
                ? AllowedTreeType.Commander
                : AllowedTreeType.Agent;

            foreach (string methodName in MethodRegistry.GetMethodNames())
            {
                // Skip methods not allowed for this tree type
                if (!MethodRegistry.IsMethodAllowed(methodName, allowedFor)) continue;

                BehaviourNodeType category = MethodRegistry.GetCategory(methodName);
                switch (category)
                {
                    case BehaviourNodeType.ACTION:
                        actionMethods.Add(methodName);
                        break;
                    case BehaviourNodeType.CONDITION:
                        conditionMethods.Add(methodName);
                        break;
                    case BehaviourNodeType.DECORATOR:
                        decoratorMethods.Add(methodName);
                        break;
                    case BehaviourNodeType.COMPOSITE:
                        compositeMethods.Add(methodName);
                        break;
                }
            }

            lastBuiltTreeType = allowedFor;
        }

        public void SetCreationPosition(Vector2 position) => creationPosition = position;
        public void SetPendingConnection(Port port) => pendingConnectionPort = port;
        public void ClearPendingConnection() => pendingConnectionPort = null;

        /// <summary>Invalidates cached method lists and search tree. Call when tree type changes.</summary>
        public void InvalidateCache()
        {
            cachedSearchTree = null;
            BuildMethodLists();
        }

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            if (cachedSearchTree != null)
                return cachedSearchTree;

            List<SearchTreeEntry> searchList = new List<SearchTreeEntry>();
            searchList.Add(new SearchTreeGroupEntry(new GUIContent("Behaviour Nodes"), 0));

            // Composites (dynamic — built-in and custom)
            if (compositeMethods.Count > 0)
            {
                searchList.Add(new SearchTreeGroupEntry(new GUIContent("Composites"), 1));
                for (int i = 0; i < compositeMethods.Count; i++)
                {
                    string methodName = compositeMethods[i];
                    string displayName = ToDisplayName(methodName);
                    searchList.Add(new SearchTreeEntry(new GUIContent(displayName, identationIcon))
                        { level = 2, userData = methodName });
                }
            }
            searchList.Add(new SearchTreeEntry(new GUIContent("Subtree", identationIcon))
                { level = 1, userData = BehaviourNodeType.SUBTREE });

            // Actions
            searchList.Add(new SearchTreeGroupEntry(new GUIContent("Actions"), 1));
            AddMethodEntries(searchList, 2, actionMethods, "BB_", "WaitSeconds");

            // Conditionals
            searchList.Add(new SearchTreeGroupEntry(new GUIContent("Conditionals"), 1));
            AddMethodEntries(searchList, 2, conditionMethods, "BB_", "Cooldown");

            // Decorators
            searchList.Add(new SearchTreeGroupEntry(new GUIContent("Decorators"), 1));
            foreach (string methodName in decoratorMethods)
            {
                searchList.Add(new SearchTreeEntry(new GUIContent(methodName, identationIcon))
                    { level = 2, userData = methodName });
            }

            // Blackboard subgroup
            searchList.Add(new SearchTreeGroupEntry(new GUIContent("Blackboard"), 1));
            AddPrefixedSubGroup(searchList, "Compare", 2, "BB_Compare", actionMethods, conditionMethods);
            AddPrefixedSubGroup(searchList, "Check", 2, "BB_Check", actionMethods, conditionMethods);
            AddMultiplePrefixedSubGroup(searchList, "Write", 2, new[] { "BB_Set", "BB_Clear", "BB_Toggle" }, actionMethods, conditionMethods);
            AddPrefixedSubGroup(searchList, "State", 2, "BB_HasChanged", actionMethods, conditionMethods);
            AddPrefixedSubGroup(searchList, "Debug", 2, "BB_Log", actionMethods, conditionMethods);

            // Time subgroup
            searchList.Add(new SearchTreeGroupEntry(new GUIContent("Time"), 1));
            AddExactEntries(searchList, 2, actionMethods, "WaitSeconds");
            AddExactEntries(searchList, 2, conditionMethods, "Cooldown");

            cachedSearchTree = searchList;
            return cachedSearchTree;
        }

        private void AddMethodEntries(List<SearchTreeEntry> searchList, int level, List<string> methods, params string[] excludePrefixes)
        {
            for (int i = 0; i < methods.Count; i++)
            {
                string name = methods[i];
                bool exclude = false;
                for (int j = 0; j < excludePrefixes.Length; j++)
                {
                    if (name.StartsWith(excludePrefixes[j])) { exclude = true; break; }
                }
                if (exclude) continue;

                searchList.Add(new SearchTreeEntry(new GUIContent(name, identationIcon))
                    { level = level, userData = name });
            }
        }

        private void AddPrefixedSubGroup(List<SearchTreeEntry> searchList, string groupName, int groupLevel, string prefix, List<string> actions, List<string> conditions)
        {
            List<string> matches = new List<string>();
            CollectPrefixed(matches, actions, prefix);
            CollectPrefixed(matches, conditions, prefix);
            if (matches.Count == 0) return;

            searchList.Add(new SearchTreeGroupEntry(new GUIContent(groupName), groupLevel));
            AddEntries(searchList, groupLevel + 1, matches);
        }

        private void AddMultiplePrefixedSubGroup(List<SearchTreeEntry> searchList, string groupName, int groupLevel, string[] prefixes, List<string> actions, List<string> conditions)
        {
            List<string> matches = new List<string>();
            for (int i = 0; i < prefixes.Length; i++)
            {
                CollectPrefixed(matches, actions, prefixes[i]);
                CollectPrefixed(matches, conditions, prefixes[i]);
            }
            if (matches.Count == 0) return;

            searchList.Add(new SearchTreeGroupEntry(new GUIContent(groupName), groupLevel));
            AddEntries(searchList, groupLevel + 1, matches);
        }

        private void AddEntries(List<SearchTreeEntry> searchList, int level, List<string> entries)
        {
            entries.Sort((a, b) => string.CompareOrdinal(a, b));
            for (int i = 0; i < entries.Count; i++)
            {
                string methodName = entries[i];
                searchList.Add(new SearchTreeEntry(new GUIContent(methodName, identationIcon))
                    { level = level, userData = methodName });
            }
        }

        private static void CollectPrefixed(List<string> dst, List<string> src, string prefix)
        {
            for (int i = 0; i < src.Count; i++)
            {
                if (src[i].StartsWith(prefix))
                    dst.Add(src[i]);
            }
        }

        private void AddExactEntries(List<SearchTreeEntry> searchList, int level, List<string> list, string name)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == name)
                {
                    searchList.Add(new SearchTreeEntry(new GUIContent(name, identationIcon))
                        { level = level, userData = name });
                }
            }
        }

        private static string ToDisplayName(string methodName)
        {
            if (string.IsNullOrEmpty(methodName))
                return methodName;

            if (methodName == methodName.ToUpperInvariant())
            {
                // All-caps → Title Case (e.g., "SELECTOR" → "Selector")
                return char.ToUpperInvariant(methodName[0]) + methodName.Substring(1).ToLowerInvariant();
            }

            return methodName;
        }

        public bool OnSelectEntry(SearchTreeEntry SearchTreeEntry, SearchWindowContext context)
        {
            if (graphView == null || !graphView.HasTree)
            {
                pendingConnectionPort = null;
                return true;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("(BTree) Create Node");

            BehaviourNodeView createdNodeView = null;
            try
            {
                switch (SearchTreeEntry.userData)
                {
                    case BehaviourNodeType compositeType when compositeType == BehaviourNodeType.SUBTREE:
                    {
                        createdNodeView = graphView.CreateSubtreeNode(creationPosition);
                        break;
                    }
                    case string methodName:
                    {
                        if (compositeMethods.Contains(methodName))
                            createdNodeView = graphView.CreateCompositeNode(methodName, creationPosition);
                        else if (decoratorMethods.Contains(methodName))
                            createdNodeView = graphView.CreateDecoratorNode(methodName, creationPosition);
                        else if (conditionMethods.Contains(methodName))
                            createdNodeView = graphView.CreateLeafNode(methodName, creationPosition, BehaviourNodeType.CONDITION);
                        else if (actionMethods.Contains(methodName))
                            createdNodeView = graphView.CreateLeafNode(methodName, creationPosition, BehaviourNodeType.ACTION);
                        break;
                    }
                    case Group _:
                        break;
                }

                if (pendingConnectionPort != null && createdNodeView != null)
                {
                    if (pendingConnectionPort.direction == Direction.Output && createdNodeView.input != null)
                        graphView.TryConnectPorts(pendingConnectionPort, createdNodeView.input);
                }
            }
            finally
            {
                pendingConnectionPort = null;
                Undo.CollapseUndoOperations(undoGroup);
            }
            return true;
        }
    }
}
