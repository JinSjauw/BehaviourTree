using System;
using System.Collections.Generic;
using System.Linq;
using BehaviourTree.Core;
using Codice.CM.Common;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace BehaviourTree.Editor
{
    [Serializable]
    public class ClipBoardData
    {
        public List<SerializedNodeData> nodeDatas = new List<SerializedNodeData>();
        public List<SerializedEdgeData> edgeDatas = new List<SerializedEdgeData>();
    }

    // <summary> Handles the copy paste actions. Contains the functions supposed to be bound to the graphView copy paste callbacks </summary>
    public class CopyPasteHandler
    {
        private static ClipBoardData clipBoard;
        private BehaviourTreeEditorGraphView graphView;

        public CopyPasteHandler(BehaviourTreeEditorGraphView graphView)
        {
            this.graphView = graphView;

            if(clipBoard == null)
            {
                clipBoard = new ClipBoardData();
            }
        }

        //Copy
        public void CopySelectedNodes()
        {
            List<BehaviourNodeView> selectedNodes = graphView.selection.OfType<BehaviourNodeView>().ToList();
            if(selectedNodes.Count == 0) return;

            foreach(BehaviourNodeView nodeView in selectedNodes)
            {
                BehaviourNode node = nodeView.NodeSO;

                if(node== null) return;

                SerializedNodeData serializedNode = SerializeNode(node);

                if(serializedNode != null && !clipBoard.nodeDatas.Contains(serializedNode))
                {
                    clipBoard.nodeDatas.Add(serializedNode);
                }
            }

            // Now capture edges between selected nodes
            foreach (var edge in graphView.edges)
            {
                var sourceNodeView = edge.output.node as BehaviourNodeView;
                var targetNodeView = edge.input.node as BehaviourNodeView;
                if (sourceNodeView != null && targetNodeView != null &&
                    selectedNodes.Contains(sourceNodeView) && selectedNodes.Contains(targetNodeView))
                {
                    clipBoard.edgeDatas.Add(new SerializedEdgeData
                    {
                        sourceNodeGUID = sourceNodeView.NodeSO.guid,
                        targetNodeGUID = targetNodeView.NodeSO.guid
                    });
                }
            }
        }

        //Paste
        public void PasteNodes(BehaviourTreeAsset treeAsset)
        {
            if (clipBoard.nodeDatas.Count == 0) return;

            // Map old GUIDs to new GUIDs
            Dictionary<string, string> guidMap = new Dictionary<string, string>();
            List<BehaviourNodeView> pastedViews = new List<BehaviourNodeView>();

            foreach (var serializedNode in clipBoard.nodeDatas)
            {
                string newGuid = GUID.Generate().ToString();
                guidMap[serializedNode.guid] = newGuid;

                BehaviourNode newNode = CreateNodeDataFromSerialized(serializedNode, treeAsset);
                newNode.guid = newGuid;
                newNode.graphPosition = serializedNode.graphPosition + new Vector2(30, 30); // offset

                BehaviourNodeView nodeView = graphView.CreateNodeView(newNode); // adjust to your API

                pastedViews.Add(nodeView);
            }

            //Use GUID to recreate edges
            foreach (var serializedEdge in clipBoard.edgeDatas)
            {
                if (guidMap.TryGetValue(serializedEdge.sourceNodeGUID, out string newSourceGuid) &&
                    guidMap.TryGetValue(serializedEdge.targetNodeGUID, out string newTargetGuid))
                {
                    BehaviourNodeView sourceView = pastedViews.FirstOrDefault(nodeView => nodeView.NodeSO.guid == newSourceGuid);
                    BehaviourNodeView targetView = pastedViews.FirstOrDefault(nodeView => nodeView.NodeSO.guid == newTargetGuid);

                    if (sourceView != null && targetView != null)
                    {
                        // Assuming each node has one output port and one input port
                        Port outputPort = sourceView.output;   // you need to expose this
                        Port inputPort = targetView.input;     // and this

                        treeAsset.AddChild(sourceView.NodeSO, targetView.NodeSO);

                        Edge edge = outputPort.ConnectTo(inputPort);
                        graphView.AddElement(edge);
                    }
                }
            }

            clipBoard.nodeDatas.Clear();
            clipBoard.edgeDatas.Clear();

            // Step 3: Clear selection and select pasted nodes
            graphView.ClearSelection();
            foreach (var view in pastedViews)
                graphView.AddToSelection(view);
        }


        #region Helpers

        private BehaviourNode CreateNodeDataFromSerialized(SerializedNodeData data, BehaviourTreeAsset treeAsset)
        {
            // Factory method: instantiate correct node type based on data.nodeType
            BehaviourNode node = null;
            switch (data.nodeType)
            {
                case BehaviourNodeType.ACTION:
                    ActionNode actionNode = ScriptableObject.CreateInstance<ActionNode>();
                    actionNode.name = data.methodID.ToString();
                    actionNode.methodID = data.methodID;
                    actionNode.fieldEntries = data.fieldEntries;
                    actionNode.BlackBoardTypeID = data.BlackBoardTypeID;
                    node = actionNode;
                    break;
                case BehaviourNodeType.DECORATOR:
                    DecoratorNode decoratorNode = ScriptableObject.CreateInstance<DecoratorNode>();
                    decoratorNode.name = data.methodID.ToString();
                    decoratorNode.methodID = data.methodID;
                    decoratorNode.fieldEntries = data.fieldEntries;
                    decoratorNode.BlackBoardTypeID = data.BlackBoardTypeID;
                    node = decoratorNode;
                    break;
                case BehaviourNodeType.SELECTOR:
                    SelectorNode selectorNode = ScriptableObject.CreateInstance<SelectorNode>();
                    selectorNode.name = selectorNode.NodeType.ToString();
                    node = selectorNode;
                    break;
                case BehaviourNodeType.SEQUENCE:
                    SequenceNode sequenceNode = ScriptableObject.CreateInstance<SequenceNode>();
                    sequenceNode.name = sequenceNode.NodeType.ToString();
                    node = sequenceNode;
                    break;
                default:
                    node = ScriptableObject.CreateInstance<BehaviourNode>();
                    break;
            }
            
            treeAsset.RegisterNode(node);

            return node;
        }

        // Convert the current clipboard to a JSON string
        public string SerializeClipboard()
        {
            if (clipBoard.nodeDatas.Count == 0) return null;
            return JsonUtility.ToJson(clipBoard, prettyPrint: false);
        }

        // Overwrite the static clipboard from a JSON string
        public void DeserializeClipboard(string json)
        {
            clipBoard = JsonUtility.FromJson<ClipBoardData>(json);
        }

        private SerializedNodeData SerializeNode(BehaviourNode node)
        {
            SerializedNodeData serializedNode = new SerializedNodeData
            {
                nodeType = node.NodeType,
                guid = node.guid,
                graphPosition = node.graphPosition
            };

            if(node.NodeType == BehaviourNodeType.ACTION)
            {
                ActionNode actionNode = (ActionNode)node;
                serializedNode.methodID = actionNode.methodID;
                serializedNode.fieldEntries = actionNode.fieldEntries;
                serializedNode.BlackBoardTypeID = actionNode.BlackBoardTypeID;
            }
            else if(node.NodeType == BehaviourNodeType.DECORATOR)
            {
                DecoratorNode decoratorNode = (DecoratorNode)node;
                serializedNode.methodID = decoratorNode.methodID;
                serializedNode.fieldEntries = decoratorNode.fieldEntries;
                serializedNode.BlackBoardTypeID = decoratorNode.BlackBoardTypeID;
            }
            
            return serializedNode;
        }

        #endregion
    }
}