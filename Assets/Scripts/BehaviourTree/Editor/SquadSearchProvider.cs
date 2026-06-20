using System;
using System.Collections.Generic;
using BehaviourTree.Core;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace BehaviourTree.Editor
{
    public class SquadSearchProvider : ScriptableObject, ISearchWindowProvider
    {
        public Action<SquadDefinition> onSquadSelected;
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
            searchList.Add(new SearchTreeGroupEntry(new GUIContent("Squad Definitions"), 0));

            string[] guids = AssetDatabase.FindAssets("t:SquadDefinition");

            if (guids.Length == 0)
            {
                searchList.Add(new SearchTreeEntry(new GUIContent("No squads found"))
                {
                    level = 1,
                    userData = null,
                });
            }
            else
            {
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    SquadDefinition squad = AssetDatabase.LoadAssetAtPath<SquadDefinition>(path);
                    if (squad == null) continue;

                    searchList.Add(new SearchTreeEntry(new GUIContent(squad.name, identationIcon))
                    {
                        level = 1,
                        userData = squad,
                    });
                }
            }

            return searchList;
        }

        public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context)
        {
            if (entry.userData is SquadDefinition squad)
            {
                onSquadSelected?.Invoke(squad);
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
