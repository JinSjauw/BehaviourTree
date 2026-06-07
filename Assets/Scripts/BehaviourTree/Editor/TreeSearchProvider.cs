using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace BehaviourTree.Editor
{
    public class TreeSearchProvider : ScriptableObject, ISearchWindowProvider
    {
        private Texture2D identationIcon;

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            if (identationIcon == null)
            {
                identationIcon = new Texture2D(1, 1);
                identationIcon.SetPixel(0, 0, Color.clear);
                identationIcon.Apply();
            }

            var searchList = new List<SearchTreeEntry>();
            searchList.Add(new SearchTreeGroupEntry(new GUIContent("Behaviour Trees"), 0));

            string[] guids = AssetDatabase.FindAssets("t:BehaviourTreeAsset");

            if (guids.Length == 0)
            {
                searchList.Add(new SearchTreeEntry(new GUIContent("No trees found"))
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
                    BehaviourTreeAsset asset = AssetDatabase.LoadAssetAtPath<BehaviourTreeAsset>(path);
                    if (asset == null) continue;

                    searchList.Add(new SearchTreeEntry(new GUIContent(asset.name, identationIcon))
                    {
                        level = 1,
                        userData = asset,
                    });
                }
            }

            return searchList;
        }

        public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context)
        {
            if (entry.userData is BehaviourTreeAsset treeAsset)
            {
                Selection.activeObject = treeAsset;
                AssetDatabase.OpenAsset(treeAsset);
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
