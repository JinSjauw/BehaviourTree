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

        private BehaviourTreeEditorGraphView graphView;

        public void Initialize(BehaviourTreeEditorGraphView sourceGraphView)
        {
            graphView = sourceGraphView;

            actionMethods = new List<MethodID>();
            conditionMethods = new List<MethodID>();
            foreach (var field in typeof(MethodID).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                MethodCategoryAttribute attribute = field.GetCustomAttribute<MethodCategoryAttribute>();
                if (attribute == null) continue;

                if(attribute.nodeCategory == BehaviourNodeType.ACTION)
                {
                    actionMethods.Add((MethodID)field.GetValue(null));
                }
                else
                {
                    conditionMethods.Add((MethodID)field.GetValue(null));    
                }
            }
        }

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            List<SearchTreeEntry> searchList = new List<SearchTreeEntry>();
            searchList.Add(new SearchTreeGroupEntry(new GUIContent("Behaviour Nodes"), 0));
            searchList.Add(new SearchTreeGroupEntry(new GUIContent("Composites"), 1));
            searchList.Add(new SearchTreeEntry(new GUIContent("Selector"))
            {
                level = 2,
                userData = BehaviourNodeType.SELECTOR
            });
            searchList.Add(new SearchTreeEntry(new GUIContent("Sequence"))
            {
                level = 2,
                userData = BehaviourNodeType.SEQUENCE,
            });
            
            searchList.Add(new SearchTreeGroupEntry(new GUIContent("Actions"), 1));

            foreach(MethodID methodID in actionMethods)
            {
                searchList.Add(new SearchTreeEntry(new GUIContent(methodID.ToString()))
                {
                    level = 2,
                    userData = methodID,
                });
            }

            searchList.Add(new SearchTreeGroupEntry(new GUIContent("Conditionals"), 1));

            foreach(MethodID methodID in conditionMethods)
            {
                searchList.Add(new SearchTreeEntry(new GUIContent(methodID.ToString()))
                {
                    level = 2,
                    userData = methodID,
                });
            }

            return searchList;
        }

        public bool OnSelectEntry(SearchTreeEntry SearchTreeEntry, SearchWindowContext context)
        {
            switch (SearchTreeEntry.userData)
            {
                case BehaviourNodeType selector when selector == BehaviourNodeType.SELECTOR:
                {
                    graphView.CreateNode(typeof(SelectorNode));
                    return true;
                }
                case BehaviourNodeType sequence when sequence == BehaviourNodeType.SEQUENCE:
                {
                    graphView.CreateNode(typeof(SequenceNode));
                    return true;
                }
                case MethodID methodID:
                {
                    graphView.CreateLeafNode(methodID);
                    return true;
                }

                case Group _:

                break;
            }

            return true;
        }
    }
}


