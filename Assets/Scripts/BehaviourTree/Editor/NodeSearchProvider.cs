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
        private BehaviourTreeEditorGraphView graphView;

        private Texture2D identationIcon;

        public void Initialize(BehaviourTreeEditorGraphView sourceGraphView)
        {
            graphView = sourceGraphView;

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

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
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

            foreach(MethodID methodID in actionMethods)
            {
                searchList.Add(new SearchTreeEntry(new GUIContent(methodID.ToString(), identationIcon))
                {
                    level = 2,
                    userData = methodID,
                });
            }

            searchList.Add(new SearchTreeGroupEntry(new GUIContent("Conditionals"), 1));

            foreach(MethodID methodID in conditionMethods)
            {
                searchList.Add(new SearchTreeEntry(new GUIContent(methodID.ToString(), identationIcon))
                {
                    level = 2,
                    userData = methodID,
                });
            }

            searchList.Add(new SearchTreeGroupEntry(new GUIContent("Decorators"), 1));

            foreach(MethodID methodID in decoratorMethods)
            {
                searchList.Add(new SearchTreeEntry(new GUIContent(methodID.ToString(), identationIcon))
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
                    graphView.CreateNode(typeof(SelectorNode), selector.ToString());
                    return true;
                }
                case BehaviourNodeType sequence when sequence == BehaviourNodeType.SEQUENCE:
                {
                    graphView.CreateNode(typeof(SequenceNode), sequence.ToString());
                    return true;
                }
                case MethodID methodID when decoratorMethods.Contains(methodID):
                {
                    graphView.CreateDecoratorNode(methodID, methodID.ToString());
                    return true;
                }
                case MethodID methodID:
                {
                    graphView.CreateLeafNode(methodID, methodID.ToString());
                    return true;
                }

                case Group _:

                break;
            }

            return true;
        }
    }
}


