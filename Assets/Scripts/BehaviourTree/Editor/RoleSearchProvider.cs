using System;
using System.Collections.Generic;
using BehaviourTree.Core;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace BehaviourTree.Editor
{
    /// <summary>
    /// SearchWindow provider that lists available roles from a SquadDefinition,
    /// filtering out roles already assigned to the connection.
    /// </summary>
    public class RoleSearchProvider : ScriptableObject, ISearchWindowProvider
    {
        public Action<string> onRoleSelected;
        public List<SquadRole> availableRoles;
        public HashSet<string> excludeRoles;
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
            searchList.Add(new SearchTreeGroupEntry(new GUIContent("Available Roles"), 0));

            List<SquadRole> filtered = new List<SquadRole>();
            if (availableRoles != null)
            {
                for (int i = 0; i < availableRoles.Count; i++)
                {
                    if (excludeRoles == null || !excludeRoles.Contains(availableRoles[i].name))
                        filtered.Add(availableRoles[i]);
                }
            }

            if (filtered.Count == 0)
            {
                searchList.Add(new SearchTreeEntry(new GUIContent("No available roles"))
                {
                    level = 1,
                    userData = null,
                });
            }
            else
            {
                for (int i = 0; i < filtered.Count; i++)
                {
                    SquadRole role = filtered[i];
                    searchList.Add(new SearchTreeEntry(new GUIContent(role.name, identationIcon))
                    {
                        level = 1,
                        userData = role.name,
                    });
                }
            }

            return searchList;
        }

        public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context)
        {
            if (entry.userData is string roleName)
                onRoleSelected?.Invoke(roleName);
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
