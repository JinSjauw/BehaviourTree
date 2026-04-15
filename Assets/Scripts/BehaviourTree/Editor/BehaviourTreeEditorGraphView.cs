using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using UnityEngine;

namespace BehaviourTree.Editor
{
    [UxmlElement("BTGraphView")]
    public partial class BehaviourTreeEditorGraphView : GraphView
    {

        // callback for when graph changes (hook up export logic here)
        public Action<BehaviourTreeEditorGraphView> onGraphDataChanged;
        
        private BehaviourTreeAsset tree;

        public BehaviourTreeEditorGraphView()
        {
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Scripts/BehaviourTree/Editor/BehaviourTreeEditor.uss");
            styleSheets.Add(styleSheet);

            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);

            // Standard manipulators for pan, select, box-select
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            // Optional: Frame selection with 'F' key
            //this.AddManipulator(new FrameSelection());

            // Grid background (visual only)
            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            // Optional: Listen for graph changes to trigger auto-save/compile
            graphViewChanged += OnGraphViewChanged;
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
                    // Rule 1: Root node can't be a child
                    if (endNode.NodeSO.NodeType == BehaviourNodeType.ROOT && port.direction == Direction.Input)
                        continue;

                    // Rule 2: Leaf nodes can't have children
                    if (startNode.NodeSO.NodeType == BehaviourNodeType.ACTION && port.direction == Direction.Output)
                        continue;
                    if (startNode.NodeSO.NodeType == BehaviourNodeType.CONDITION && port.direction == Direction.Output)
                        continue;

                    //// Rule 3: Decorators can only have ONE child
                    //if (startNode.NodeType == BTNodeType.Decorator &&
                    //    port.direction == Direction.Output &&
                    //    startNode.GetChildCount() >= 1)
                    //    continue;

                    // Rule 4: Prevent cycles (optional but recommended)
                    if (WouldCreateCycle(startNode, endNode))
                        continue;
                }

                compatible.Add(port);
            }

            return compatible;
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
            if (change.edgesToCreate != null || change.elementsToRemove != null)
            {

                if(change.elementsToRemove.Count > 0) 
                { 
                    for(int i = 0; i < change.elementsToRemove.Count; i++) 
                    {
                        BehaviourNodeView nodeView = change.elementsToRemove[i] as BehaviourNodeView;
                        if(nodeView != null) 
                        {
                            tree.DeleteNode(nodeView.NodeSO);
                        }
                    }
                }

                onGraphDataChanged?.Invoke(this);
            }

            return change;
        }

        private void CreateNode(Type type) 
        {
            BehaviourNode node = tree.CreateNode(type);
            CreateNodeView(node);
        }

        //Build dropdown menu
        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            base.BuildContextualMenu(evt);

            var types = TypeCache.GetTypesDerivedFrom<BehaviourNode>();
            foreach (var type in types) 
            {
                evt.menu.AppendAction($"{type.Name}", (a) => CreateNode(type));
            }
        }

        public void PopulateView(BehaviourTreeAsset tree)
        {
            if (!tree) 
            {
                Debug.LogWarning($"No treeAsset selected");
                return; 
            }

            this.tree = tree;

            graphViewChanged -= OnGraphViewChanged;

            DeleteElements(graphElements);

            graphViewChanged += OnGraphViewChanged;

            for (int i = 0; i < tree.nodesList.Count; i++)
            {
                CreateNodeView(tree.nodesList[i]);
            }
        }

        private void CreateNodeView(BehaviourNode node)
        {
            BehaviourNodeView nodeView = new BehaviourNodeView(node);
            AddElement(nodeView);
        }
    }
}