using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using UnityEngine;
using BehaviourTree.Core;

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


        public BehaviourTreeEditorGraphView()
        {
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Scripts/BehaviourTree/Editor/BehaviourTreeEditor.uss");
            styleSheets.Add(styleSheet);

            nodeViewDict = new Dictionary<string, BehaviourNodeView>();   

            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);

            // Standard manipulators for pan, select, box-select
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            // Grid background
            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            //Listen for graph changes to trigger auto-save/compile
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
                    //Root node can't be a child
                    if (endNode.NodeSO.NodeType == BehaviourNodeType.ROOT && port.direction == Direction.Input)
                        continue;

                    //Leaf nodes can't have children
                    if (startNode.NodeSO.NodeType == BehaviourNodeType.ACTION && port.direction == Direction.Output)
                        continue;
                    if (startNode.NodeSO.NodeType == BehaviourNodeType.CONDITION && port.direction == Direction.Output)
                        continue;

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
            if(tree.nodesList == null)
            {
                tree.nodesList = new List<BehaviourNode>();
            }

            if(tree.rootCopy == null)
            {
                tree.rootCopy = tree.CreateNode(typeof(RootNode));   
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

        private void CreateNodeView(BehaviourNode node)
        {
            BehaviourNodeView nodeView = new BehaviourNodeView(node);
            nodeView.OnNodeSelected = OnNodeSelected;

            AddElement(nodeView);
            nodeViewDict[nodeView.Guid] = nodeView;
        }

        private BehaviourNodeView FindNodeView(BehaviourNode node) 
        {
            if(nodeViewDict.TryGetValue(node.guid, out BehaviourNodeView nodeView)) 
            {
                return nodeView;
            }
            
            return null;
            //return GetNodeByGuid(node.guid) as BehaviourNodeView;
        }

       
    }
}