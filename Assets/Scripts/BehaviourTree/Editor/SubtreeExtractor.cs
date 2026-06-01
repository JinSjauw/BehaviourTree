using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using BehaviourTree.Core;

namespace BehaviourTree.Editor
{
    public class SubtreeExtractor
    {
        private readonly BehaviourTreeEditorGraphView graphView;

        public SubtreeExtractor(BehaviourTreeEditorGraphView graphView)
        {
            this.graphView = graphView;
        }

        public bool CanExtract(List<ISelectable> selection, BehaviourTreeAsset tree)
        {
            if (tree == null) return false;
            if (EditorApplication.isPlaying) return false;

            List<BehaviourNodeView> selected = selection
                .OfType<BehaviourNodeView>()
                .Where(v => v.NodeSO != null &&
                v.NodeSO.NodeType != BehaviourNodeType.ROOT &&
                v.NodeSO is not RootNode).ToList();

            if (selected.Count == 0) return false;

            return true;
        }

        public void Extract(List<ISelectable> selection, BehaviourTreeAsset tree, Action<BehaviourTreeAsset> populateAction)
        {
            if (!CanExtract(selection, tree)) return;

            List<BehaviourNodeView> selectedViews = selection
                .OfType<BehaviourNodeView>()
                .Where(node => node.NodeSO != null &&
                node.NodeSO.NodeType != BehaviourNodeType.ROOT &&
                node.NodeSO is not RootNode)
                .ToList();

            if (selectedViews.Count == 0) return;

            HashSet<BehaviourNode> selectedNodes = new HashSet<BehaviourNode>(selectedViews.Select(node => node.NodeSO));
            List<BehaviourNode> rootCandidates = FindSelectionRootCandidates(selectedNodes, tree);

            string path = EditorUtility.SaveFilePanelInProject("Create Subtree", "NewSubtree", "asset", "Create a new BehaviourTreeAsset for the subtree");
            if (string.IsNullOrEmpty(path)) return;

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("(BTree) Extract Selection To Subtree");

            BehaviourTreeAsset subtreeAsset = ScriptableObject.CreateInstance<BehaviourTreeAsset>();
            subtreeAsset.name = System.IO.Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(subtreeAsset, path);

            subtreeAsset.nodesList = new List<BehaviourNode>();
            subtreeAsset.CreateBlackBoard();

            RootNode subtreeRoot = (RootNode)subtreeAsset.CreateNode(typeof(RootNode));
            subtreeRoot.name = "ROOT";
            subtreeRoot.graphPosition = Vector2.zero;
            subtreeAsset.root = subtreeRoot;
            subtreeAsset.RegisterNode(subtreeRoot);

            Dictionary<BehaviourNode, BehaviourNode> cloneMap = new Dictionary<BehaviourNode, BehaviourNode>();
            Vector2 origin = Vector2.zero;

            if (selectedNodes.Count > 0) origin = selectedNodes.First().graphPosition;

            if (rootCandidates.Count > 0)
            {
                origin = rootCandidates[0].graphPosition;
            }
            Vector2 layoutOffset = new Vector2(0f, 200f);

            foreach (BehaviourNode node in selectedNodes)
            {
                BehaviourNode clone = CloneNodeIntoAsset(node, subtreeAsset);
                clone.graphPosition = node.graphPosition - origin + layoutOffset;
                cloneMap[node] = clone;
            }

            foreach (var kvp in cloneMap)
            {
                BehaviourNode src = kvp.Key;
                BehaviourNode dst = kvp.Value;
                for (int c = 0; c < src.children.Count; c++)
                {
                    BehaviourNode child = src.children[c];
                    if (child == null) continue;
                    if (!selectedNodes.Contains(child)) continue;
                    subtreeAsset.AddChild(dst, cloneMap[child]);
                }
            }

            if (rootCandidates.Count == 1)
            {
                subtreeAsset.AddChild(subtreeRoot, cloneMap[rootCandidates[0]]);
            }
            EditorUtility.SetDirty(subtreeAsset);
            AssetDatabase.SaveAssets();

            BehaviourNode replaceNode = rootCandidates.Count == 1 ? rootCandidates[0] : null;
            BehaviourNode externalParent = replaceNode != null ? FindFirstParentOutsideSelection(replaceNode, selectedNodes, tree) : null;

            if (replaceNode != null && externalParent != null)
            {
                SubtreeNode subtreeRefNode = (SubtreeNode)tree.CreateNode(typeof(SubtreeNode));
                subtreeRefNode.name = "Subtree";
                subtreeRefNode.graphPosition = replaceNode.graphPosition;
                subtreeRefNode.subTreeAsset = subtreeAsset;
                tree.RegisterNode(subtreeRefNode);

                int childIdx = externalParent.children.IndexOf(replaceNode);
                if (childIdx >= 0)
                {
                    Undo.RecordObject(externalParent, "(BTree) Replace Child");
                    externalParent.children[childIdx] = subtreeRefNode;
                    EditorUtility.SetDirty(externalParent);
                }
            }
            else
            {
                SubtreeNode subtreeRefNode = (SubtreeNode)tree.CreateNode(typeof(SubtreeNode));

                if(rootCandidates.Count > 0)
                {
                    subtreeRefNode.graphPosition = rootCandidates[0].graphPosition;
                }
                else
                {
                    subtreeRefNode.graphPosition = Vector2.zero;
                }

                subtreeRefNode.name = "Subtree";
                subtreeRefNode.subTreeAsset = subtreeAsset;
                tree.RegisterNode(subtreeRefNode);
            }

            {
                List<BehaviourNode> toDelete = selectedNodes.ToList();
                for (int i = 0; i < toDelete.Count; i++)
                {
                    tree.DeleteNode(toDelete[i]);
                }
            }

            Undo.CollapseUndoOperations(undoGroup);

            populateAction(subtreeAsset);
        }

        private List<BehaviourNode> FindSelectionRootCandidates(HashSet<BehaviourNode> selectedNodes, BehaviourTreeAsset tree)
        {
            List<BehaviourNode> candidates = new List<BehaviourNode>();
            foreach (BehaviourNode node in selectedNodes)
            {
                bool hasSelectedParent = false;
                for (int i = 0; i < tree.nodesList.Count; i++)
                {
                    BehaviourNode candidate = tree.nodesList[i];
                    if (candidate == null || !selectedNodes.Contains(candidate)) continue;
                    if (candidate.children.Contains(node))
                    {
                        hasSelectedParent = true;
                        break;
                    }
                }
                if (!hasSelectedParent)
                    candidates.Add(node);
            }
            return candidates;
        }

        private BehaviourNode FindFirstParentOutsideSelection(BehaviourNode node, HashSet<BehaviourNode> selectedNodes, BehaviourTreeAsset tree)
        {
            for (int i = 0; i < tree.nodesList.Count; i++)
            {
                BehaviourNode p = tree.nodesList[i];
                if (p == null) continue;
                if (selectedNodes.Contains(p)) continue;
                if (p.children.Contains(node))
                    return p;
            }
            return null;
        }

        private BehaviourNode CloneNodeIntoAsset(BehaviourNode src, BehaviourTreeAsset destination)
        {
            BehaviourNode dst;
            if (src is LeafNode leaf)
            {
                LeafNode created = (LeafNode)destination.CreateNode(typeof(LeafNode));
                created.SetLeafType(leaf.NodeType);
                created.name = leaf.name;
                created.methodID = leaf.methodID;
                created.fieldEntries = leaf.fieldEntries;
                created.BlackBoardTypeID = leaf.BlackBoardTypeID;
                dst = created;
            }
            else if (src is DecoratorNode decorator)
            {
                DecoratorNode created = (DecoratorNode)destination.CreateNode(typeof(DecoratorNode));
                created.name = decorator.name;
                created.methodID = decorator.methodID;
                created.fieldEntries = decorator.fieldEntries;
                created.BlackBoardTypeID = decorator.BlackBoardTypeID;
                dst = created;
            }
            else if (src is CompositeNode composite)
            {
                CompositeNode created = (CompositeNode)destination.CreateNode(typeof(CompositeNode));
                created.SetCompositeType(composite.NodeType);
                created.name = composite.name;
                dst = created;
            }
            else if (src is SubtreeNode subtree)
            {
                SubtreeNode created = (SubtreeNode)destination.CreateNode(typeof(SubtreeNode));
                created.name = subtree.name;
                created.subTreeAsset = subtree.subTreeAsset;
                created.bindings = subtree.bindings != null ? new List<SubtreeBinding>(subtree.bindings) : new List<SubtreeBinding>();
                dst = created;
            }
            else
            {
                dst = destination.CreateNode(src.GetType());
                dst.name = src.name;
            }

            destination.RegisterNode(dst);
            return dst;
        }
    }
}
