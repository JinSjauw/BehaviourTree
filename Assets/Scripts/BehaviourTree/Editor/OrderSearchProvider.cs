using System;
using System.Collections.Generic;
using BehaviourTree.Core;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace BehaviourTree.Editor
{
    public class OrderSearchProvider : ScriptableObject, ISearchWindowProvider
    {
        public OrderRegistry registry;
        public Action<string> onOrderSelected;
        private Texture2D identationIcon;

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            if (identationIcon == null)
            {
                identationIcon = new Texture2D(1, 1);
                identationIcon.SetPixel(0, 0, Color.clear);
                identationIcon.Apply();
            }

            List<SearchTreeEntry> searchList = new List<SearchTreeEntry>();
            searchList.Add(new SearchTreeGroupEntry(new GUIContent("Orders"), 0));

            if (registry == null || registry.orderNames == null || registry.orderNames.Count == 0)
            {
                searchList.Add(new SearchTreeEntry(new GUIContent("No orders defined"))
                {
                    level = 1,
                    userData = null,
                });
            }
            else
            {
                for (int i = 0; i < registry.orderNames.Count; i++)
                {
                    searchList.Add(new SearchTreeEntry(new GUIContent(registry.orderNames[i], identationIcon))
                    {
                        level = 1,
                        userData = registry.orderNames[i],
                    });
                }
            }

            return searchList;
        }

        public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context)
        {
            if (entry.userData is string orderName)
            {
                onOrderSelected?.Invoke(orderName);
            }
            return true;
        }

        private void OnDestroy()
        {
            if (identationIcon != null)
            {
                DestroyImmediate(identationIcon);
                identationIcon = null;
            }
        }
    }
}
