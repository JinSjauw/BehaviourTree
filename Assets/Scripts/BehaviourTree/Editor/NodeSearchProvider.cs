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
        private Vector2 creationPosition;

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

        public void SetCreationPosition(Vector2 position)
        {
            creationPosition = position;
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


