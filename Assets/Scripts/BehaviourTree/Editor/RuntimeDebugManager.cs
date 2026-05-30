using System;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using UnityEngine;
using BehaviourTree.Core;
using BehaviourTree.Runtime;
using System.Linq;

namespace BehaviourTree.Editor
{
    public class RuntimeDebugManager
    {
        private readonly BehaviourTreeEditorGraphView graphView;

        private readonly Dictionary<string, BehaviourNodeView> proxyNodeViews = new Dictionary<string, BehaviourNodeView>();
        private readonly Dictionary<string, Edge> proxyInternalEdges = new Dictionary<string, Edge>();
        private readonly Dictionary<string, Edge> proxyReplacementEdges = new Dictionary<string, Edge>();
        private readonly List<Edge> hiddenEdges = new List<Edge>();
        private readonly HashSet<int> hiddenEdgeHashes = new HashSet<int>();
        private readonly HashSet<string> replacedSubtreeGuids = new HashSet<string>();
        private readonly List<BehaviourNodeView> hiddenSubtreeViews = new List<BehaviourNodeView>();

        private readonly Dictionary<UnityEngine.Object, Dictionary<string, BehaviourNode>> authoringNodeLookupCache
            = new Dictionary<UnityEngine.Object, Dictionary<string, BehaviourNode>>();

        public RuntimeDebugManager(BehaviourTreeEditorGraphView graphView)
        {
            this.graphView = graphView;
        }

        public void ClearCaches()
        {
            hiddenEdges.Clear();
            hiddenEdgeHashes.Clear();
            replacedSubtreeGuids.Clear();
            hiddenSubtreeViews.Clear();
            proxyInternalEdges.Clear();
            proxyReplacementEdges.Clear();
            proxyNodeViews.Clear();
        }

        public void SetupDebugProxies(TreeRunner runner, Dictionary<string, BehaviourNodeView> nodeViewDict)
        {
            if (runner == null) return;

            RuntimeDebugProvider provider = runner.GetComponent<RuntimeDebugProvider>();
            if (provider == null || provider.currentNodeGuids == null || provider.currentNodeStates == null) return;
            if (provider.currentNodeGuids.Length != provider.currentNodeStates.Length) return;

            EnsureProxyNodesExist(provider.currentNodeGuids, nodeViewDict);
            EnsureProxyEdges();
            ReplaceSubtreeNodesWithRootProxies(nodeViewDict);
        }

        public void RefreshDebugVisuals(TreeRunner runner, Dictionary<string, BehaviourNodeView> nodeViewDict)
        {
            if (runner == null) return;

            RuntimeDebugProvider provider = runner.GetComponent<RuntimeDebugProvider>();
            if (provider == null || provider.currentNodeStates == null) return;

            NodeState[] states = provider.currentNodeStates;
            int activeIndex = provider.activeNodeIndex;

            if (provider.currentNodeGuids == null || provider.currentNodeGuids.Length != states.Length)
            {
                foreach (BehaviourNodeView nodeView in nodeViewDict.Values)
                {
                    if (nodeView?.NodeSO == null) continue;

                    int runtimeIdx = nodeView.NodeSO.runtimeIndex;
                    if (runtimeIdx < 0 || runtimeIdx >= states.Length) continue;

                    NodeState state = states[runtimeIdx];
                    bool isActive = runtimeIdx == activeIndex;
                    nodeView.SetDebugState(state, isActive);
                }
                return;
            }

            Dictionary<string, int> guidToIndex = new Dictionary<string, int>(provider.currentNodeGuids.Length);
            for (int i = 0; i < provider.currentNodeGuids.Length; i++)
            {
                string guid = provider.currentNodeGuids[i];
                if (string.IsNullOrEmpty(guid)) continue;
                guidToIndex[guid] = i;
            }

            foreach (BehaviourNodeView nodeView in nodeViewDict.Values)
            {
                if (nodeView?.NodeSO == null) continue;

                if (guidToIndex.TryGetValue(nodeView.Guid, out int runtimeIdx))
                {
                    NodeState state = states[runtimeIdx];
                    bool isActive = runtimeIdx == activeIndex;
                    nodeView.SetDebugState(state, isActive);
                }
                else
                {
                    nodeView.SetDebugState(NodeState.NONE, false);
                }
            }

            foreach (var kvp in proxyNodeViews)
            {
                BehaviourNodeView proxy = kvp.Value;
                if (proxy == null) continue;

                if (guidToIndex.TryGetValue(kvp.Key, out int runtimeIdx))
                {
                    NodeState state = states[runtimeIdx];
                    bool isActive = runtimeIdx == activeIndex;
                    proxy.SetDebugState(state, isActive);
                }
                else
                {
                    proxy.SetDebugState(NodeState.NONE, false);
                }
            }
        }

        private void ClearAll()
        {
            for (int i = 0; i < hiddenEdges.Count; i++)
            {
                Edge edge = hiddenEdges[i];
                if (edge != null)
                    edge.style.display = DisplayStyle.Flex;
            }
            hiddenEdges.Clear();
            hiddenEdgeHashes.Clear();

            foreach (var edge in proxyInternalEdges.Values)
            {
                edge?.RemoveFromHierarchy();
            }
            proxyInternalEdges.Clear();

            foreach (var edge in proxyReplacementEdges.Values)
            {
                edge?.RemoveFromHierarchy();
            }
            proxyReplacementEdges.Clear();

            foreach (var node in proxyNodeViews.Values)
            {
                node?.RemoveFromHierarchy();
            }
            proxyNodeViews.Clear();

            for (int i = 0; i < hiddenSubtreeViews.Count; i++)
            {
                BehaviourNodeView view = hiddenSubtreeViews[i];
                if (view != null)
                    view.style.display = DisplayStyle.Flex;
            }
            hiddenSubtreeViews.Clear();
            replacedSubtreeGuids.Clear();
        }

        public void RemoveAllProxies()
        {
            foreach (var edge in proxyInternalEdges.Values)
                edge?.RemoveFromHierarchy();
            proxyInternalEdges.Clear();

            foreach (var edge in proxyReplacementEdges.Values)
                edge?.RemoveFromHierarchy();
            proxyReplacementEdges.Clear();

            foreach (var node in proxyNodeViews.Values)
                node?.RemoveFromHierarchy();
            proxyNodeViews.Clear();

            for (int i = 0; i < hiddenEdges.Count; i++)
            {
                Edge edge = hiddenEdges[i];
                if (edge != null)
                    edge.style.display = DisplayStyle.Flex;
            }
            hiddenEdges.Clear();
            hiddenEdgeHashes.Clear();

            for (int i = 0; i < hiddenSubtreeViews.Count; i++)
            {
                BehaviourNodeView view = hiddenSubtreeViews[i];
                if (view != null)
                    view.style.display = DisplayStyle.Flex;
            }
            hiddenSubtreeViews.Clear();
            replacedSubtreeGuids.Clear();
        }

        private void EnsureProxyNodesExist(string[] runtimeGuids, Dictionary<string, BehaviourNodeView> nodeViewDict)
        {
            HashSet<string> needed = new HashSet<string>();

            for (int i = 0; i < runtimeGuids.Length; i++)
            {
                string runtimeGuid = runtimeGuids[i];
                if (string.IsNullOrEmpty(runtimeGuid) || !runtimeGuid.Contains("/")) continue;
                needed.Add(runtimeGuid);

                if (!proxyNodeViews.TryGetValue(runtimeGuid, out BehaviourNodeView proxy))
                {
                    proxy = CreateRuntimeProxyNode(runtimeGuid, nodeViewDict);
                    if (proxy != null)
                        proxyNodeViews[runtimeGuid] = proxy;
                }
            }

            List<string> toRemove = null;
            foreach (var kvp in proxyNodeViews)
            {
                if (needed.Contains(kvp.Key)) continue;
                toRemove ??= new List<string>();
                toRemove.Add(kvp.Key);
            }
            if (toRemove != null)
            {
                for (int i = 0; i < toRemove.Count; i++)
                {
                    string guid = toRemove[i];
                    if (proxyNodeViews.TryGetValue(guid, out var view))
                        view.RemoveFromHierarchy();
                    proxyNodeViews.Remove(guid);
                }
            }
        }

        private BehaviourNodeView CreateRuntimeProxyNode(string runtimeGuid, Dictionary<string, BehaviourNodeView> nodeViewDict)
        {
            string[] segments = runtimeGuid.Split('/');
            if (segments.Length < 2) return null;

            string anchorGuid = segments[0];
            if (!nodeViewDict.TryGetValue(anchorGuid, out BehaviourNodeView anchorView)) return null;

            if (anchorView.NodeSO is not SubtreeNode anchorSubtreeNode) return null;
            if (anchorSubtreeNode.SubTreeAsset == null) return null;

            if (!TryResolveRuntimePath(anchorSubtreeNode, segments, out BehaviourNode targetNode, out Vector2 relativePos))
                return null;

            BehaviourNodeView proxy = new BehaviourNodeView(targetNode);
            proxy.capabilities &= ~(Capabilities.Movable | Capabilities.Selectable | Capabilities.Deletable | Capabilities.Copiable);
            proxy.layer = 1;

            Vector2 basePos = anchorSubtreeNode.graphPosition;
            Vector2 finalPos = basePos + relativePos;
            proxy.style.left = finalPos.x;
            proxy.style.top = finalPos.y;

            graphView.AddElement(proxy);
            return proxy;
        }

        private bool TryResolveRuntimePath(SubtreeNode rootSubtreeNode, string[] segments, out BehaviourNode targetNode, out Vector2 relativePos)
        {
            targetNode = null;
            relativePos = Vector2.zero;

            BehaviourTreeAssetBase currentAuthoring = rootSubtreeNode.SubTreeAsset;
            Vector2 origin = GetAuthoringRootOrigin(currentAuthoring);
            Vector2 acc = -origin;

            for (int i = 1; i < segments.Length; i++)
            {
                if (!TryGetAuthoringNodeByGuid(currentAuthoring, segments[i], out BehaviourNode node))
                    return false;

                if (i == segments.Length - 1)
                {
                    targetNode = node;
                    acc += node.graphPosition;
                    relativePos = acc;
                    return true;
                }

                acc += node.graphPosition;
                if (node is not SubtreeNode nestedSubtree || nestedSubtree.SubTreeAsset == null)
                    return false;

                currentAuthoring = nestedSubtree.SubTreeAsset;
                origin = GetAuthoringRootOrigin(currentAuthoring);
                acc -= origin;
            }

            return false;
        }

        private Vector2 GetAuthoringRootOrigin(BehaviourTreeAssetBase authoring)
        {
            if (authoring == null || authoring.Root == null) return Vector2.zero;
            BehaviourNode root = authoring.Root;
            if (root.NodeType == BehaviourNodeType.ROOT && root.children.Count > 0)
                root = root.children[0];
            return root != null ? root.graphPosition : Vector2.zero;
        }

        private BehaviourNode GetAuthoringEffectiveRoot(BehaviourTreeAssetBase authoring)
        {
            if (authoring == null || authoring.Root == null) return null;
            BehaviourNode root = authoring.Root;
            if (root.NodeType == BehaviourNodeType.ROOT && root.children.Count > 0)
                root = root.children[0];
            return root;
        }

        private bool TryGetAuthoringNodeByGuid(BehaviourTreeAssetBase authoring, string guid, out BehaviourNode node)
        {
            node = null;
            if (authoring is not BehaviourTreeAsset treeAsset) return false;
            if (treeAsset.nodesList == null) return false;

            if (!authoringNodeLookupCache.TryGetValue(treeAsset, out var map) || map == null)
            {
                map = new Dictionary<string, BehaviourNode>();
                for (int i = 0; i < treeAsset.nodesList.Count; i++)
                {
                    BehaviourNode n = treeAsset.nodesList[i];
                    if (n == null || string.IsNullOrEmpty(n.guid)) continue;
                    if (!map.ContainsKey(n.guid))
                        map[n.guid] = n;
                }
                authoringNodeLookupCache[treeAsset] = map;
            }

            return map.TryGetValue(guid, out node);
        }

        private void EnsureProxyEdges()
        {
            foreach (var edge in proxyInternalEdges.Values)
            {
                edge?.RemoveFromHierarchy();
            }
            proxyInternalEdges.Clear();

            foreach (var kvp in proxyNodeViews)
            {
                string runtimeGuid = kvp.Key;
                BehaviourNodeView parentView = kvp.Value;
                if (parentView == null || parentView.NodeSO == null || parentView.output == null) continue;

                string scopePrefix = runtimeGuid.Substring(0, runtimeGuid.LastIndexOf("/", StringComparison.Ordinal));
                BehaviourNode node = parentView.NodeSO;

                if (node is SubtreeNode subtreeNode)
                {
                    BehaviourNode subRoot = subtreeNode.SubTreeAsset != null ? subtreeNode.SubTreeAsset.Root : null;
                    if (subRoot != null && subRoot.NodeType == BehaviourNodeType.ROOT && subRoot.children.Count > 0)
                        subRoot = subRoot.children[0];
                    if (subRoot != null)
                    {
                        string childGuid = runtimeGuid + "/" + subRoot.guid;
                        TryAddProxyEdge(runtimeGuid, childGuid);
                    }
                    continue;
                }

                for (int c = 0; c < node.children.Count; c++)
                {
                    BehaviourNode child = node.children[c];
                    if (child == null) continue;
                    string childGuid = scopePrefix + "/" + child.guid;
                    TryAddProxyEdge(runtimeGuid, childGuid);
                }
            }
        }

        private void TryAddProxyEdge(string parentGuid, string childGuid)
        {
            if (!proxyNodeViews.TryGetValue(parentGuid, out BehaviourNodeView parentView)) return;
            if (!proxyNodeViews.TryGetValue(childGuid, out BehaviourNodeView childView)) return;
            if (parentView.output == null || childView.input == null) return;

            string key = parentGuid + "->" + childGuid;
            if (proxyInternalEdges.ContainsKey(key)) return;

            Edge edge = parentView.output.ConnectTo(childView.input);
            proxyInternalEdges[key] = edge;
            graphView.AddElement(edge);
        }

        private void ReplaceSubtreeNodesWithRootProxies(Dictionary<string, BehaviourNodeView> nodeViewDict)
        {
            foreach (BehaviourNodeView subtreeView in nodeViewDict.Values)
            {
                if (subtreeView?.NodeSO is not SubtreeNode subtreeNode) continue;
                if (subtreeNode.SubTreeAsset == null) continue;
                if (replacedSubtreeGuids.Contains(subtreeNode.guid)) continue;

                BehaviourNode subRoot = GetAuthoringEffectiveRoot(subtreeNode.SubTreeAsset);
                if (subRoot == null || string.IsNullOrEmpty(subRoot.guid)) continue;

                string rootRuntimeGuid = subtreeNode.guid + "/" + subRoot.guid;
                if (!proxyNodeViews.TryGetValue(rootRuntimeGuid, out BehaviourNodeView rootProxy)) continue;
                if (rootProxy == null) continue;

                subtreeView.style.display = DisplayStyle.None;
                hiddenSubtreeViews.Add(subtreeView);

                if (subtreeView.input == null || rootProxy.input == null) continue;

                foreach (Edge edge in subtreeView.input.connections.ToList())
                {
                    if (edge == null || edge.output == null) continue;

                    edge.style.display = DisplayStyle.None;
                    int edgeKey = edge.GetHashCode();
                    if (hiddenEdgeHashes.Add(edgeKey))
                        hiddenEdges.Add(edge);

                    string key = "replace:" + edgeKey;
                    if (proxyReplacementEdges.ContainsKey(key)) continue;

                    Edge replacement = edge.output.ConnectTo(rootProxy.input);
                    proxyReplacementEdges[key] = replacement;
                    graphView.AddElement(replacement);
                }

                replacedSubtreeGuids.Add(subtreeNode.guid);
            }
        }
    }
}
