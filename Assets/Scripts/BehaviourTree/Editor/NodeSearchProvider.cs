using System;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEditor.Graphs;
using UnityEngine;


namespace BehaviourTree.Editor
{
    public class NodeSearchProvider : ScriptableObject, ISearchWindowProvider
    {
        private BehaviourTreeEditorGraphView graphView;
        public void Initialize(BehaviourTreeEditorGraphView sourceGraphView)
        {
            graphView = sourceGraphView;
        }

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            List<SearchTreeEntry> searchList = new List<SearchTreeEntry>();
            searchList.Add(new SearchTreeGroupEntry(new GUIContent("Nodes"), 0));

            return searchList;
        }

        public bool OnSelectEntry(SearchTreeEntry SearchTreeEntry, SearchWindowContext context)
        {
            return true;
        }
    }
}


