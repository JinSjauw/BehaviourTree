using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using UnityEngine;
using BehaviourTree.Core;
using BehaviourTree.Runtime;

namespace BehaviourTree.Editor
{
    [UxmlElement("BTGraphView")]
    public partial class BehaviourTreeEditorGraphView : GraphView
    {
        // Callback for when graph changes (hook up export logic here)
        public Action<BehaviourTreeEditorGraphView> onGraphDataChanged;
        public Action<BehaviourNodeView> OnNodeSelected;
        
        private BehaviourTreeAsset tree;
        private Dictionary<string, BehaviourNodeView> nodeViewDict;
        private NodeSearchProvider searchWindow;
        private TextField graphTitleLabel;
        private CopyPasteHandler copyPasteHandler;
        private BtEdgeConnectorListener edgeConnectorListener;

        public bool HasTree => tree != null;

        public BehaviourTreeEditorGraphView()
        {
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(BehaviourTreeEditorPaths.EditorUss);
            styleSheets.Add(styleSheet);

            nodeViewDict = new Dictionary<string, BehaviourNodeView>();   

            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);

            // Standard manipulators for pan, select, box-select
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            AddGrid();
            AddGraphTitle();
            EnsureSearchWindow();
            edgeConnectorListener = new BtEdgeConnectorListener(this);
            
            //Setup copy/paste callbacks
            copyPasteHandler = new CopyPasteHandler(this);

            serializeGraphElements = SerializeGraphSelection;
            unserializeAndPaste = UnSerializeAndPaste;

            //Listen for graph changes to trigger auto-save/compile
            graphViewChanged += OnGraphViewChanged;

            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void UnSerializeAndPaste(string operationName, string data)
        {
            if (string.IsNullOrEmpty(data)) return;

            // Restore the static clipboard from the string
            copyPasteHandler.DeserializeClipboard(data);

            // Paste the nodes using the handler’s existing logic
            copyPasteHandler.PasteNodes(tree);
        }

        private string SerializeGraphSelection(IEnumerable<GraphElement> elements)
        {
            copyPasteHandler.CopySelectedNodes();

            return copyPasteHandler.SerializeClipboard();
        }

        private void AddGrid()
        {
            // Grid background
            GridBackground grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();
        }

        private void AddGraphTitle()
        {
            graphTitleLabel = new TextField
            {
                value = "Behaviour Tree",
                isDelayed = true
            };
            graphTitleLabel.name = "GraphTitle";
            graphTitleLabel.ClearClassList();

            VisualElement input = graphTitleLabel.Q<VisualElement>("unity-text-input");
            input.name = "GraphTitleInput";
            input.ClearClassList();

            graphTitleLabel.RegisterValueChangedCallback(evt =>
            {
                if (tree == null)
                {
                    graphTitleLabel.SetValueWithoutNotify("Behaviour Tree");
                    return;
                }

                string newName = evt.newValue?.Trim();
                if (string.IsNullOrEmpty(newName) || newName == tree.name)
                {
                    RefreshTitle();
                    return;
                }

                if (EditorApplication.isPlaying)
                {
                    RefreshTitle();
                    return;
                }

                string path = AssetDatabase.GetAssetPath(tree);
                if (string.IsNullOrEmpty(path))
                {
                    RefreshTitle();
                    return;
                }

                string err = AssetDatabase.RenameAsset(path, newName);
                if (!string.IsNullOrEmpty(err))
                {
                    Debug.LogError(err);
                    RefreshTitle();
                    return;
                }

                AssetDatabase.SaveAssets();
                RefreshTitle();
            });
            Add(graphTitleLabel);
        }

        public void EnsureSearchWindow()
        {
            if(searchWindow == null)
            {
                searchWindow = ScriptableObject.CreateInstance<NodeSearchProvider>();

                searchWindow.Initialize(this);
            }

            nodeCreationRequest = context =>
            {
                EnsureSearchWindow();
                if (tree == null) return;

                Rect windowRect = EditorWindow.focusedWindow.position;

                Vector2 localPos = this.ChangeCoordinatesTo(contentViewContainer, this.WorldToLocal(context.screenMousePosition - new Vector2(windowRect.x, windowRect.y)));

                searchWindow.SetCreationPosition(localPos);
                searchWindow.ClearPendingConnection();
                OpenSearchWindow(context.screenMousePosition);
            };
        }

        private void OpenSearchWindow(Vector2 mousePosition)
        {
            EnsureSearchWindow();
            if (tree == null) return;
            SearchWindow.Open(new SearchWindowContext(mousePosition), searchWindow);
        }

        private void SetupEdgeConnectors(BehaviourNodeView nodeView)
        {
            if (nodeView?.input != null)
                nodeView.input.AddManipulator(new EdgeConnector<Edge>(edgeConnectorListener));

            if (nodeView?.output != null)
                nodeView.output.AddManipulator(new EdgeConnector<Edge>(edgeConnectorListener));
        }

        private void OpenSearchWindowForEdgeDrop(Port startPort, Vector2 graphMousePosition)
        {
            if (startPort == null) return;
            EnsureSearchWindow();
            if (searchWindow == null) return;
            if (tree == null) return;

            Vector2 screenPos = GUIUtility.GUIToScreenPoint(graphMousePosition);
            Rect windowRect = EditorWindow.focusedWindow.position;
            Vector2 windowMouse = screenPos - new Vector2(windowRect.x, windowRect.y);
            Vector2 localPos = this.ChangeCoordinatesTo(contentViewContainer, this.WorldToLocal(windowMouse));

            searchWindow.SetCreationPosition(localPos);
            searchWindow.SetPendingConnection(startPort);
            OpenSearchWindow(screenPos);
        }

        public void ClearView()
        {
            tree = null;
            graphTitleLabel.SetValueWithoutNotify("Behaviour Tree");

            graphViewChanged -= OnGraphViewChanged;
            try
            {
                DeleteElements(graphElements);
                nodeViewDict.Clear();
            }
            finally
            {
                graphViewChanged += OnGraphViewChanged;
            }
        }

        public bool TryConnectPorts(Port from, Port to)
        {
            if (from == null || to == null) return false;
            if (from.direction == to.direction) return false;

            Port output = from.direction == Direction.Output ? from : to;
            Port input = from.direction == Direction.Input ? from : to;

            if (!GetCompatiblePorts(output, null).Contains(input)) return false;

            List<GraphElement> edgesToRemove = new List<GraphElement>();

            if (input.capacity == Port.Capacity.Single)
            {
                foreach (Edge e in input.connections.ToList())
                    edgesToRemove.Add(e);
            }

            if (output.capacity == Port.Capacity.Single)
            {
                foreach (Edge e in output.connections.ToList())
                    edgesToRemove.Add(e);
            }

            if (edgesToRemove.Count > 0)
                DeleteElements(edgesToRemove);

            BehaviourNodeView parentView = output.node as BehaviourNodeView;
            BehaviourNodeView childView = input.node as BehaviourNodeView;

            if (parentView == null || childView == null) return false;
            if (tree == null) return false;

            tree.AddChild(parentView.NodeSO, childView.NodeSO);

            Edge edge = output.ConnectTo(input);
            AddElement(edge);

            onGraphDataChanged?.Invoke(this);
            return true;
        }

        private void OnUndoRedo()
        {
            if(tree == null) return;
            
            if (tree.NeedsNodesListSync())
                tree.SyncNodesListFromAssets();

            PopulateView(tree);
        }

        // Simple cycle detection: can't connect if 'target' is an ancestor of 'source'
        private bool WouldCreateCycle(BehaviourNodeView source, BehaviourNodeView target)
        {
            var current = target;
            while (current != null)
            {
                if (current == source) return true;
                // Walk up via input port (parent)
                var parentPort = current.inputContainer.Children().OfType<Port>().FirstOrDefault();
                if (parentPort?.connections.FirstOrDefault()?.output?.node is not BehaviourNodeView parent)
                    break;
                current = parent;
            }
            return false;
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (change.elementsToRemove != null)
                HandleElementRemoval(change.elementsToRemove);

            if (change.edgesToCreate != null)
                HandleEdgeCreation(change.edgesToCreate);

            if (change.movedElements != null)
                HandleElementsMoved();

            return change;
        }

        private void HandleElementRemoval(List<GraphElement> elementsToRemove)
        {
            for (int i = 0; i < elementsToRemove.Count; i++)
            {
                if (elementsToRemove[i] is BehaviourNodeView nodeView)
                {
                    tree.DeleteNode(nodeView.NodeSO);
                    nodeViewDict.Remove(nodeView.Guid);
                }
                else if (elementsToRemove[i] is Edge edge)
                {
                    BehaviourNodeView parentView = edge.output.node as BehaviourNodeView;
                    BehaviourNodeView childView = edge.input.node as BehaviourNodeView;
                    tree.RemoveChild(parentView.NodeSO, childView.NodeSO);
                }
            }

            onGraphDataChanged?.Invoke(this);
        }

        private void HandleEdgeCreation(List<Edge> edgesToCreate)
        {
            for (int i = 0; i < edgesToCreate.Count; i++)
            {
                Edge edge = edgesToCreate[i];
                BehaviourNodeView parentView = edge.output.node as BehaviourNodeView;
                BehaviourNodeView childView = edge.input.node as BehaviourNodeView;

                tree.AddChild(parentView.NodeSO, childView.NodeSO);
            }

            onGraphDataChanged?.Invoke(this);
        }

        private void HandleElementsMoved()
        {
            foreach (BehaviourNodeView nodeView in nodeViewDict.Values)
                nodeView.SortChildren();
        }

        public BehaviourNodeView CreateNodeView(BehaviourNode node)
        {
            BehaviourNodeView nodeView = BuildNodeView(node);
            RegisterNodeView(nodeView);
            return nodeView;
        }

        private BehaviourNodeView BuildNodeView(BehaviourNode node)
        {
            BehaviourNodeView nodeView = new BehaviourNodeView(node);
            nodeView.OnNodeSelected = OnNodeSelected;
            nodeView.layer = 0;
            return nodeView;
        }

        private void RegisterNodeView(BehaviourNodeView nodeView)
        {
            AddElement(nodeView);
            SetupEdgeConnectors(nodeView);

            if (!nodeViewDict.TryAdd(nodeView.Guid, nodeView))
                Debug.LogWarning($"Duplicate GUID in graph view: {nodeView.Guid}");
        }

        private BehaviourNodeView FindNodeView(BehaviourNode node) 
        {
            if(nodeViewDict.TryGetValue(node.guid, out BehaviourNodeView nodeView)) 
            {
                return nodeView;
            }
            
            return null;
        }
        
        public BehaviourNodeView CreateCompositeNode(BehaviourNodeType compositeType, Vector2 position)
        {
            CompositeNode node = (CompositeNode)tree.CreateNode(typeof(CompositeNode));
            //Undo.RecordObject(node, "(BTree) Configure Node");
            node.SetCompositeType(compositeType);
            node.name = compositeType.ToString();
            node.graphPosition = position;
            tree.RegisterNode(node);
            
            return CreateNodeView(node);
        }
        
        public BehaviourNodeView CreateLeafNode(MethodID methodID, Vector2 position, BehaviourNodeType leafType = BehaviourNodeType.ACTION)
        {
            LeafNode node = (LeafNode)tree.CreateNode(typeof(LeafNode));
            //Undo.RecordObject(node, "(BTree) Configure Node");
            node.SetLeafType(leafType);
            node.methodID = methodID;
            node.name = methodID.ToString();
            node.graphPosition = position;
            tree.RegisterNode(node);

            return CreateNodeView(node);
        }

        public BehaviourNodeView CreateDecoratorNode(MethodID methodID, Vector2 position)
        {
            DecoratorNode node = (DecoratorNode)tree.CreateNode(typeof(DecoratorNode));
            //Undo.RecordObject(node, "(BTree) Configure Node");
            node.methodID = methodID;
            node.name = methodID.ToString();
            node.graphPosition = position;
            tree.RegisterNode(node);
            
            return CreateNodeView(node);
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var compatible = new List<Port>();

            foreach (var port in ports)
            {
                if (IsSelfOrSameNode(port, startPort)) continue;
                if (IsWrongDirection(port, startPort)) continue;

                if (startPort.node is BehaviourNodeView startNode &&
                    port.node is BehaviourNodeView endNode)
                {
                    if (IsRootAsChild(endNode, port)) continue;
                    if (IsLeafNodeParenting(startNode, port)) continue;
                    if (IsDuplicateChild(endNode, startNode)) continue;
                    if (WouldCreateCycle(startNode, endNode)) continue;
                }

                compatible.Add(port);
            }

            return compatible;
        }

        private static bool IsSelfOrSameNode(Port port, Port startPort)
        {
            return port == startPort || port.node == startPort.node;
        }

        private static bool IsWrongDirection(Port port, Port startPort)
        {
            return port.direction == startPort.direction;
        }

        private static bool IsRootAsChild(BehaviourNodeView endNode, Port port)
        {
            return endNode.NodeSO.NodeType == BehaviourNodeType.ROOT
                && port.direction == Direction.Input;
        }

        private static bool IsLeafNodeParenting(BehaviourNodeView startNode, Port port)
        {
            if (port.direction != Direction.Input) return false;

            var nodeType = startNode.NodeSO.NodeType;
            return nodeType == BehaviourNodeType.ACTION
                || nodeType == BehaviourNodeType.CONDITION;
        }

        private static bool IsDuplicateChild(BehaviourNodeView endNode, BehaviourNodeView startNode)
        {
            return endNode.NodeSO.children.Contains(startNode.NodeSO);
        }

        //Build dropdown menu
        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            base.BuildContextualMenu(evt);

            evt.menu.AppendAction($"Create Node", (a) =>
            {
                searchWindow.ClearPendingConnection();
                OpenSearchWindow(GUIUtility.GUIToScreenPoint(Event.current.mousePosition));
            }, _ => tree == null ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);
        }

        public void PopulateView(BehaviourTreeAsset tree)
        {
            if (!tree)
            {
                Debug.LogWarning("No treeAsset selected");
                return;
            }

            InitTree(tree);
            ClearAndRebuildViews();
            EnsureRootNodeExists();
            CleanupAndCreateViews();
            CleanupAndWireEdges();
        }

        private void InitTree(BehaviourTreeAsset tree)
        {
            this.tree = tree;
            RefreshTitle();

            if (tree.nodesList == null)
                tree.nodesList = new List<BehaviourNode>();
        }

        public void RefreshTitle()
        {
            if (graphTitleLabel == null) return;
            graphTitleLabel.SetValueWithoutNotify(tree != null ? tree.name : "Behaviour Tree");
        }

        private void ClearAndRebuildViews()
        {
            graphViewChanged -= OnGraphViewChanged;
            try
            {
                DeleteElements(graphElements);
                nodeViewDict.Clear();
            }
            finally { graphViewChanged += OnGraphViewChanged; }
        }

        private void EnsureRootNodeExists()
        {
            if (tree.rootCopy != null) return;

            tree.rootCopy = tree.CreateNode(typeof(RootNode));
            //Undo.RecordObject(tree.rootCopy, "(BTree) Configure Node");
            tree.rootCopy.name = "ROOT";
            tree.rootCopy.graphPosition = ViewportCenter();
            tree.RegisterNode(tree.rootCopy);
        }

        private void CleanupAndCreateViews()
        {
            for (int i = tree.nodesList.Count - 1; i >= 0; i--)
            {
                if (tree.nodesList[i] == null)
                {
                    tree.nodesList.RemoveAt(i);
                    continue;
                }
                CreateNodeView(tree.nodesList[i]);
            }
        }

        private void CleanupAndWireEdges()
        {
            for (int i = 0; i < tree.nodesList.Count; i++)
            {
                BehaviourNode node = tree.nodesList[i];

                for (int j = node.children.Count - 1; j >= 0; j--)
                {
                    if (node.children[j] == null)
                        node.children.RemoveAt(j);
                }

                for (int j = 0; j < node.children.Count; j++)
                {
                    BehaviourNode child = node.children[j];

                    BehaviourNodeView parentView = FindNodeView(node);
                    if (parentView == null && node != null)
                        parentView = CreateNodeView(node);

                    BehaviourNodeView childView = FindNodeView(child);
                    if (childView == null && child != null)
                        childView = CreateNodeView(child);

                    if (parentView == null || childView == null)
                    {
                        Debug.LogWarning($"Failed to connect edge: {node.guid} → {child?.guid}");
                        continue;
                    }

                    if (parentView.output == null || childView.input == null)
                    {
                        Debug.LogWarning($"Port missing: {node.NodeType} → {node.children[j].NodeType}");
                        continue;
                    }

                    Edge edge = parentView.output.ConnectTo(childView.input);
                    AddElement(edge);
                }
            }
        }
    
        public void RefreshDebugVisuals(TreeRunner runner)
        {
            // Only meaningful in Play Mode
            if (!EditorApplication.isPlaying) return;

            if (runner == null) return;

            RuntimeDebugProvider provider = runner.GetComponent<RuntimeDebugProvider>();
            if (provider == null || provider.currentNodeStates == null) return;

            NodeState[] states = provider.currentNodeStates;
            int activeIndex = provider.activeNodeIndex;

            foreach (BehaviourNodeView nodeViewEntry in nodeViewDict.Values)
            {
                BehaviourNodeView nodeView = nodeViewEntry;
                if (nodeView?.NodeSO == null) continue;

                int runtimeIdx = nodeView.NodeSO.runtimeIndex;
                if (runtimeIdx < 0 || runtimeIdx >= states.Length) continue;

                NodeState state = states[runtimeIdx];
                bool isActive = runtimeIdx == activeIndex;

                nodeView.SetDebugState(state, isActive);
            }
        }

        private Vector2 ViewportCenter()
        {
            Rect layout = contentViewContainer.layout;
            if (layout.width > 0 && layout.height > 0)
            {
                return new Vector2(layout.width * 0.5f, layout.height * 0.5f);
            }
            return new Vector2(400, 300);
        }

        private sealed class BtEdgeConnectorListener : IEdgeConnectorListener
        {
            private readonly BehaviourTreeEditorGraphView graphView;

            public BtEdgeConnectorListener(BehaviourTreeEditorGraphView graphView)
            {
                this.graphView = graphView;
            }

            public void OnDropOutsidePort(Edge edge, Vector2 position)
            {
                Port startPort = edge?.output ?? edge?.input;

                edge?.output?.Disconnect(edge);
                edge?.input?.Disconnect(edge);
                edge?.RemoveFromHierarchy();

                if (startPort != null && startPort.direction == Direction.Output)
                    graphView.OpenSearchWindowForEdgeDrop(startPort, position);
            }

            public void OnDrop(GraphView graphView, Edge edge)
            {
                if (graphView == null || edge == null) return;
                if (edge.input == null || edge.output == null) return;

                List<GraphElement> elementsToRemove = new List<GraphElement>();

                if (edge.input.capacity == Port.Capacity.Single)
                {
                    foreach (Edge connection in edge.input.connections.ToList())
                    {
                        if (connection != edge)
                            elementsToRemove.Add(connection);
                    }
                }

                if (edge.output.capacity == Port.Capacity.Single)
                {
                    foreach (Edge connection in edge.output.connections.ToList())
                    {
                        if (connection != edge)
                            elementsToRemove.Add(connection);
                    }
                }

                if (elementsToRemove.Count > 0)
                    graphView.DeleteElements(elementsToRemove);

                edge.input.Connect(edge);
                edge.output.Connect(edge);

                var edgesToCreate = new List<Edge> { edge };
                var graphViewChange = new GraphViewChange { edgesToCreate = edgesToCreate };
                graphViewChange = graphView.graphViewChanged?.Invoke(graphViewChange) ?? graphViewChange;

                if (graphViewChange.edgesToCreate != null)
                {
                    for (int i = 0; i < graphViewChange.edgesToCreate.Count; i++)
                        graphView.AddElement(graphViewChange.edgesToCreate[i]);
                }
            }
        }
    }
}
