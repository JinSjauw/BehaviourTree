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
        private Label graphTitleLabel;
        private Vector2 nextGraphPosition;
        private CopyPasteHandler copyPasteHandler;

        public BehaviourTreeEditorGraphView()
        {
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Scripts/BehaviourTree/Editor/UIDocuments/BehaviourTreeEditor.uss");
            styleSheets.Add(styleSheet);

            nodeViewDict = new Dictionary<string, BehaviourNodeView>();   

            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);

            // Standard manipulators for pan, select, box-select
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            AddGrid();
            AddGraphTitle();
            AddSearchWindow();
            
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
            graphTitleLabel = new Label("Behaviour Tree")
            {
                style =
                {
                    flexShrink = 1,
                    fontSize = 18,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    unityTextAlign = TextAnchor.MiddleLeft,
                    paddingTop = 8,
                    paddingLeft = 12,
                    paddingBottom = 4,
                    color = new Color(0.8f, 0.8f, 0.8f, 1f)
                }
            };
            Add(graphTitleLabel);
        }

        private void AddSearchWindow()
        {
            if(searchWindow == null)
            {
                searchWindow = ScriptableObject.CreateInstance<NodeSearchProvider>();

                searchWindow.Initialize(this);
            }

            nodeCreationRequest = context =>
            {
                Rect windowRect = EditorWindow.focusedWindow.position;

                Vector2 localPos = this.ChangeCoordinatesTo(contentViewContainer, this.WorldToLocal(context.screenMousePosition - new Vector2(windowRect.x, windowRect.y)));

                nextGraphPosition = localPos;
                OpenSearchWindow(context.screenMousePosition);
            };
        }

        private void OpenSearchWindow(Vector2 mousePosition)
        {
            SearchWindow.Open(new SearchWindowContext(mousePosition), searchWindow);
        }

        private void OnUndoRedo()
        {
            if(tree == null) return;
            
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
            // If nodes/edges were added, removed, or moved, notify exporter
            if (change.elementsToRemove != null)
            {
                for (int i = 0; i < change.elementsToRemove.Count; i++)
                {
                    BehaviourNodeView nodeView = change.elementsToRemove[i] as BehaviourNodeView;
                    if (nodeView != null)
                    {
                        tree.DeleteNode(nodeView.NodeSO);
                        //Remove from dict
                        nodeViewDict.Remove(nodeView.Guid);
                    }

                    Edge edge = change.elementsToRemove[i] as Edge;
                    if(edge != null) 
                    {
                        BehaviourNodeView parentView = edge.output.node as BehaviourNodeView;
                        BehaviourNodeView childView = edge.input.node as BehaviourNodeView;
                        tree.RemoveChild(parentView.NodeSO, childView.NodeSO);
                    }
                }

                onGraphDataChanged?.Invoke(this);
            }

            if(change.edgesToCreate != null) 
            {
                for(int i = 0; i < change.edgesToCreate.Count; i++) 
                {
                    Edge edge = change.edgesToCreate[i];
                    BehaviourNodeView parentView = edge.output.node as BehaviourNodeView;
                    BehaviourNodeView childView = edge.input.node as BehaviourNodeView;

                    tree.AddChild(parentView.NodeSO, childView.NodeSO);
                }
                onGraphDataChanged?.Invoke(this);
            }

            if(change.movedElements != null)
            {
                foreach(BehaviourNodeView nodeView in nodeViewDict.Values)
                {
                    nodeView.SortChildren();
                }
            }

            return change;
        }

        public BehaviourNodeView CreateNodeView(BehaviourNode node)
        {
            BehaviourNodeView nodeView = new BehaviourNodeView(node);
            nodeView.OnNodeSelected = OnNodeSelected;
            nodeView.layer = 0;

            AddElement(nodeView);
            nodeViewDict[nodeView.Guid] = nodeView;

            return nodeView;
        }

        private BehaviourNodeView FindNodeView(BehaviourNode node) 
        {
            if(nodeViewDict.TryGetValue(node.guid, out BehaviourNodeView nodeView)) 
            {
                return nodeView;
            }
            
            return null;
        }
        
        public BehaviourNode CreateCompositeNode(BehaviourNodeType compositeType) 
        {
            CompositeNode node = (CompositeNode)tree.CreateNode(typeof(CompositeNode));
            node.SetCompositeType(compositeType);
            node.name = compositeType.ToString();
            node.graphPosition = nextGraphPosition;
            
            CreateNodeView(node);
            return node;
        }
        
        public void CreateLeafNode(MethodID methodID)
        {
            ActionNode node = (ActionNode)tree.CreateNode(typeof(ActionNode));
            node.methodID = methodID;
            node.name = methodID.ToString();
            node.graphPosition = nextGraphPosition;

            CreateNodeView(node);
        }

        public void CreateDecoratorNode(MethodID methodID)
        {
            DecoratorNode node = (DecoratorNode)tree.CreateNode(typeof(DecoratorNode));
            node.methodID = methodID;
            node.name = methodID.ToString();
            node.graphPosition = nextGraphPosition;

            CreateNodeView(node);
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var compatible = new List<Port>();

            foreach (var port in ports)
            {
                // Block obvious invalid connections
                if (port == startPort) continue;                           // No self-connect
                if (port.node == startPort.node) continue;                 // No same-node
                if (port.direction == startPort.direction) continue;       // Input↔Input or Output↔Output

                // Cast to your custom node type for BT-specific rules
                if (startPort.node is BehaviourNodeView startNode &&
                    port.node is BehaviourNodeView endNode)
                {
                    //Root node can't be a child
                    if (endNode.NodeSO.NodeType == BehaviourNodeType.ROOT && port.direction == Direction.Input)
                        continue;

                    //Leaf nodes can't have children
                    if (startNode.NodeSO.NodeType == BehaviourNodeType.ACTION && port.direction == Direction.Input)
                        continue;
                    if (startNode.NodeSO.NodeType == BehaviourNodeType.CONDITION && port.direction == Direction.Input)
                        continue;

                    if(endNode.NodeSO.children.Contains(startNode.NodeSO)) continue;
                    //Decorators can only have ONE child
                    //if (startNode.NodeType == BTNodeType.Decorator &&
                    //    port.direction == Direction.Output &&
                    //    startNode.GetChildCount() >= 1)
                    //    continue;

                    //Prevent cycles
                    if (WouldCreateCycle(startNode, endNode))
                        continue;
                }

                compatible.Add(port);
            }

            return compatible;
        }

        //Build dropdown menu
        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            base.BuildContextualMenu(evt);

            if(tree == null) return;
            evt.menu.AppendAction($"Create Node", (a) => OpenSearchWindow(GUIUtility.GUIToScreenPoint(Event.current.mousePosition)));
        }

        public void PopulateView(BehaviourTreeAsset tree)
        {
            if (!tree) 
            {
                Debug.LogWarning($"No treeAsset selected");
                return; 
            }

            this.tree = tree;

            graphTitleLabel.text = tree.name;

            graphViewChanged -= OnGraphViewChanged;

            DeleteElements(graphElements);

            graphViewChanged += OnGraphViewChanged;
            if(tree.nodesList == null)
            {
                tree.nodesList = new List<BehaviourNode>();
            }

            if(tree.rootCopy == null)
            {
                tree.rootCopy = tree.CreateNode(typeof(RootNode));   
                tree.rootCopy.name = "ROOT";
                EditorUtility.SetDirty(tree);
                AssetDatabase.SaveAssets();
            }

            for (int i = 0; i < tree.nodesList.Count; i++)
            {
                CreateNodeView(tree.nodesList[i]);
            }

            for (int i = 0; i < tree.nodesList.Count; i++)
            {
                BehaviourNode node = tree.nodesList[i];

                for(int j = 0; j < node.children.Count; j++) 
                {
                    BehaviourNodeView parentView = FindNodeView(node);
                    BehaviourNodeView childView = FindNodeView(node.children[j]);

                    if (parentView == null || childView == null)
                    {
                        Debug.LogWarning($"Failed to connect edge: {node.guid} → {node.children[j]?.guid}");
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
    
        //TEMP
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
    }
}