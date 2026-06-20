using System;
using System.Collections.Generic;
using System.Linq;
using BehaviourTree.Core;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace BehaviourTree.Editor
{
    /// <summary>
    /// SearchWindow provider that lists all BehaviourTreeAssetBase assets in the project,
    /// filtering out trees that already have a SquadBindingGroup in the given SquadDefinition.
    /// </summary>
    public class TreeAssetSearchProvider : ScriptableObject, ISearchWindowProvider
    {
        public Action<BehaviourTreeAssetBase> onTreeSelected;
        public SquadDefinition excludeSquad;
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
            searchList.Add(new SearchTreeGroupEntry(new GUIContent("Tree Assets"), 0));

            // Build a set of tree assets that already have bindings in this squad
            HashSet<BehaviourTreeAssetBase> excludedAssets = new HashSet<BehaviourTreeAssetBase>();
            if (excludeSquad != null && excludeSquad.bindingGroups != null)
            {
                for (int i = 0; i < excludeSquad.bindingGroups.Count; i++)
                {
                    if (excludeSquad.bindingGroups[i].treeAsset != null)
                        excludedAssets.Add(excludeSquad.bindingGroups[i].treeAsset);
                }
            }

            string[] guids = AssetDatabase.FindAssets("t:BehaviourTreeAssetBase");
            List<BehaviourTreeAssetBase> availableTrees = new List<BehaviourTreeAssetBase>();

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                BehaviourTreeAssetBase tree = AssetDatabase.LoadAssetAtPath<BehaviourTreeAssetBase>(path);
                if (tree != null && !excludedAssets.Contains(tree))
                    availableTrees.Add(tree);
            }

            availableTrees = availableTrees.OrderBy(t => t.name).ToList();

            if (availableTrees.Count == 0)
            {
                searchList.Add(new SearchTreeEntry(new GUIContent("No available trees"))
                {
                    level = 1,
                    userData = null,
                });
            }
            else
            {
                for (int i = 0; i < availableTrees.Count; i++)
                {
                    BehaviourTreeAssetBase tree = availableTrees[i];
                    searchList.Add(new SearchTreeEntry(new GUIContent(tree.name, identationIcon))
                    {
                        level = 1,
                        userData = tree,
                    });
                }
            }

            return searchList;
        }

        public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context)
        {
            if (entry.userData is BehaviourTreeAssetBase tree)
            {
                onTreeSelected?.Invoke(tree);
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
