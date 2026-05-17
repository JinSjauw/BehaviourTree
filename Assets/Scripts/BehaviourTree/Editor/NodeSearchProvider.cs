using System.Collections.Generic;
using System.Reflection;
using BehaviourTree.Core;
using UnityEditor.Experimental.GraphView;
using UnityEngine;


namespace BehaviourTree.Editor
{
    public class NodeSearchProvider : ScriptableObject, ISearchWindowProvider
    {
        private List<MethodID> actionMethods;
        private List<MethodID> conditionMethods;
        private List<MethodID> decoratorMethods;
        private List<SearchTreeEntry> cachedSearchTree;
        private BehaviourTreeEditorGraphView graphView;
        private Vector2 creationPosition;

        private Texture2D identationIcon;

        public void Initialize(BehaviourTreeEditorGraphView sourceGraphView)
        {
            graphView = sourceGraphView;
            cachedSearchTree = null;

            identationIcon = new Texture2D(1, 1);
            identationIcon.SetPixel(0, 0, Color.clear);
            identationIcon.Apply();

            actionMethods = new List<MethodID>();
            conditionMethods = new List<MethodID>();
            decoratorMethods = new List<MethodID>();

            foreach (var field in typeof(MethodID).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                MethodCategoryAttribute attribute = field.GetCustomAttribute<MethodCategoryAttribute>();
                if (attribute == null) continue;

                if(attribute.nodeCategory == BehaviourNodeType.ACTION)
                {
                    actionMethods.Add((MethodID)field.GetValue(null));
                }
                else if(attribute.nodeCategory == BehaviourNodeType.CONDITION)
                {
                    conditionMethods.Add((MethodID)field.GetValue(null));    
                }
                else
                {
                    decoratorMethods.Add((MethodID)field.GetValue(null));
                }
            }
        }

        public void SetCreationPosition(Vector2 position)
        {
            creationPosition = position;
        }

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            if (cachedSearchTree != null)
            {
                return cachedSearchTree;
            }

            List<SearchTreeEntry> searchList = new List<SearchTreeEntry>();
            searchList.Add(new SearchTreeGroupEntry(new GUIContent("Behaviour Nodes"), 0));

            searchList.Add(new SearchTreeGroupEntry(new GUIContent("Composites"), 1));
            searchList.Add(new SearchTreeEntry(new GUIContent("Selector", identationIcon))
            {
                level = 2,
                userData = BehaviourNodeType.SELECTOR
            });
            searchList.Add(new SearchTreeEntry(new GUIContent("Sequence", identationIcon))
            {
                level = 2,
                userData = BehaviourNodeType.SEQUENCE,
            });

            searchList.Add(new SearchTreeGroupEntry(new GUIContent("Actions"), 1));
            AddUnprefixedEntries(searchList, 2, actionMethods);

            searchList.Add(new SearchTreeGroupEntry(new GUIContent("Conditionals"), 1));
            AddUnprefixedEntries(searchList, 2, conditionMethods);

            searchList.Add(new SearchTreeGroupEntry(new GUIContent("Decorators"), 1));
            foreach (MethodID methodID in decoratorMethods)
            {
                searchList.Add(new SearchTreeEntry(new GUIContent(methodID.ToString(), identationIcon))
                {
                    level = 2,
                    userData = methodID,
                });
            }

            searchList.Add(new SearchTreeGroupEntry(new GUIContent("Blackboard"), 1));
            AddPrefixedSubGroup(searchList, "Compare", 2, "BB_Compare", actionMethods, conditionMethods);
            AddPrefixedSubGroup(searchList, "Check", 2, "BB_Check", actionMethods, conditionMethods);
            AddMultiplePrefixedSubGroup(searchList, "Write", 2, new[] { "BB_Set", "BB_Clear", "BB_Toggle" }, actionMethods, conditionMethods);
            AddPrefixedSubGroup(searchList, "State", 2, "BB_HasChanged", actionMethods, conditionMethods);
            //AddPrefixedSubGroup(searchList, "State", 2, "BB_Edge", actionMethods, conditionMethods);
            AddPrefixedSubGroup(searchList, "Debug", 2, "BB_Log", actionMethods, conditionMethods);

            searchList.Add(new SearchTreeGroupEntry(new GUIContent("Time"), 1));
            AddExactEntries(searchList, 2, actionMethods, "WaitSeconds");
            AddExactEntries(searchList, 2, conditionMethods, "Cooldown");

            cachedSearchTree = searchList;
            return cachedSearchTree;
        }

        private void AddMultiplePrefixedSubGroup(List<SearchTreeEntry> searchList, string groupName, int groupLevel, string[] prefixes, List<MethodID> actions, List<MethodID> conditions)
        {
            List<MethodID> matches = new List<MethodID>();
            for (int i = 0; i < prefixes.Length; i++)
            {
                CollectPrefixed(matches, actions, prefixes[i]);
                CollectPrefixed(matches, conditions, prefixes[i]);
            }
            if (matches.Count == 0) return;

            searchList.Add(new SearchTreeGroupEntry(new GUIContent(groupName), groupLevel));
            AddEntries(searchList, groupLevel + 1, matches);
        }

        private void AddPrefixedSubGroup(List<SearchTreeEntry> searchList, string groupName, int groupLevel, string prefix, List<MethodID> actions, List<MethodID> conditions)
        {
            List<MethodID> matches = new List<MethodID>();
            CollectPrefixed(matches, actions, prefix);
            CollectPrefixed(matches, conditions, prefix);
            if (matches.Count == 0) return;

            searchList.Add(new SearchTreeGroupEntry(new GUIContent(groupName), groupLevel));
            AddEntries(searchList, groupLevel + 1, matches);
        }

        private void AddEntries(List<SearchTreeEntry> searchList, int level, List<MethodID> entries)
        {
            entries.Sort((a, b) => string.CompareOrdinal(a.ToString(), b.ToString()));
            for (int i = 0; i < entries.Count; i++)
            {
                MethodID methodID = entries[i];
                searchList.Add(new SearchTreeEntry(new GUIContent(methodID.ToString(), identationIcon))
                {
                    level = level,
                    userData = methodID,
                });
            }
        }

        private static void CollectPrefixed(List<MethodID> dst, List<MethodID> src, string prefix)
        {
            for (int i = 0; i < src.Count; i++)
            {
                MethodID id = src[i];
                if (id.ToString().StartsWith(prefix))
                {
                    dst.Add(id);
                }
            }
        }

        private void AddExactEntries(List<SearchTreeEntry> searchList, int level, List<MethodID> list, string name)
        {
            for (int i = 0; i < list.Count; i++)
            {
                MethodID id = list[i];
                if (id.ToString() == name)
                {
                    searchList.Add(new SearchTreeEntry(new GUIContent(id.ToString(), identationIcon))
                    {
                        level = level,
                        userData = id,
                    });
                }
            }
        }

        private void AddUnprefixedEntries(List<SearchTreeEntry> searchList, int level, List<MethodID> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                MethodID id = list[i];
                string name = id.ToString();
                if (name.StartsWith("BB_")) continue;
                if (name == "WaitSeconds") continue;
                if (name == "Cooldown") continue;
                searchList.Add(new SearchTreeEntry(new GUIContent(name, identationIcon))
                {
                    level = level,
                    userData = id,
                });
            }
        }

        public bool OnSelectEntry(SearchTreeEntry SearchTreeEntry, SearchWindowContext context)
        {

            switch (SearchTreeEntry.userData)
            {
                case BehaviourNodeType compositeType when compositeType == BehaviourNodeType.SELECTOR || compositeType == BehaviourNodeType.SEQUENCE:
                {
                    graphView.CreateCompositeNode(compositeType, creationPosition);
                    return true;
                }
                case MethodID methodID when decoratorMethods.Contains(methodID):
                {
                    graphView.CreateDecoratorNode(methodID, creationPosition);
                    return true;
                }
                case MethodID methodID when conditionMethods.Contains(methodID):
                {
                    graphView.CreateLeafNode(methodID, creationPosition, BehaviourNodeType.CONDITION);
                    return true;
                }
                case MethodID methodID when actionMethods.Contains(methodID):
                {
                    graphView.CreateLeafNode(methodID, creationPosition, BehaviourNodeType.ACTION);
                    return true;
                }

                case Group _:

                break;
            }

            return true;
        }
    }
}

