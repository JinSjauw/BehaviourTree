using System;
using System.Collections.Generic;
using System.Linq;
using BehaviourTree.Core;
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
        private ClipBoardData clipBoard;
        private BehaviourTreeEditorGraphView graphView;
        private string lastPastedDataHash;

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

            clipBoard.nodeDatas.Clear();
            clipBoard.edgeDatas.Clear();

            foreach(BehaviourNodeView nodeView in selectedNodes)
            {
                BehaviourNode node = nodeView.NodeSO;

                if(node == null) continue;
                if(node.NodeType == BehaviourNodeType.ROOT || node is RootNode) continue;

                SerializedNodeData serializedNode = SerializeNode(node);

                if(serializedNode != null)
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

            // Track last pasted hash to prevent duplicate pastes from system clipboard
            string currentHash = HashClipboardData();
            if (currentHash == lastPastedDataHash)
            {
                clipBoard.nodeDatas.Clear();
                clipBoard.edgeDatas.Clear();
                return;
            }
            lastPastedDataHash = currentHash;

            // Map old GUIDs to new GUIDs
            Dictionary<string, string> guidMap = new Dictionary<string, string>();
            List<BehaviourNodeView> pastedViews = new List<BehaviourNodeView>();

            foreach (SerializedNodeData serializedNode in clipBoard.nodeDatas)
            {
                string newGuid = GUID.Generate().ToString();
                guidMap[serializedNode.guid] = newGuid;

                BehaviourNode newNode = CreateNodeDataFromSerialized(serializedNode, treeAsset);
                if(newNode == null) 
                {
                    Debug.LogWarning($"Failed to create node from serialized data for GUID {serializedNode.guid}");
                    continue; 
                }

                newNode.guid = newGuid;
                newNode.graphPosition = serializedNode.graphPosition + new Vector2(30, 30); // offset

                BehaviourNodeView nodeView = graphView.CreateNodeView(newNode);

                if(!pastedViews.Contains(nodeView))
                {
                    pastedViews.Add(nodeView);
                }
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

            graphView.ClearSelection();
            foreach (BehaviourNodeView view in pastedViews)
            {
                graphView.AddToSelection(view);
            }
        }


        #region Helpers

        private string HashClipboardData()
        {
            if (clipBoard.nodeDatas.Count == 0) return string.Empty;
            // Use JSON as a simple content hash — sufficient for detecting identical pastes
            return JsonUtility.ToJson(clipBoard, prettyPrint: false);
        }

        private BehaviourNode CreateNodeDataFromSerialized(SerializedNodeData data, BehaviourTreeAsset treeAsset)
        {
            //Instantiate correct node type based on data.nodeType
            BehaviourNode node = null;
            switch (data.nodeType)
            {
                case BehaviourNodeType.ACTION:
                case BehaviourNodeType.CONDITION:
                    LeafNode actionNode = ScriptableObject.CreateInstance<LeafNode>();
                    actionNode.SetLeafType(data.nodeType);
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
                case BehaviourNodeType.SEQUENCE:
                case BehaviourNodeType.PARALLEL:
                case BehaviourNodeType.PRIORITY:
                    CompositeNode compositeNode = ScriptableObject.CreateInstance<CompositeNode>();
                    compositeNode.SetCompositeType(data.nodeType);
                    compositeNode.name = data.nodeType.ToString();
                    node = compositeNode;
                    break;
                case BehaviourNodeType.SUBTREE:
                    SubtreeNode subtreeNode = ScriptableObject.CreateInstance<SubtreeNode>();
                    subtreeNode.name = "SUBTREE";
                    subtreeNode.bindings = data.bindings ?? new List<SubtreeBinding>();
                    if (!string.IsNullOrEmpty(data.subtreeAssetGUID))
                    {
                        string path = AssetDatabase.GUIDToAssetPath(data.subtreeAssetGUID);
                        subtreeNode.subTreeAsset = AssetDatabase.LoadAssetAtPath<BehaviourTreeAssetBase>(path);
                    }
                    node = subtreeNode;
                    break;
                default:
                    return null;
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

            if(node.NodeType == BehaviourNodeType.ACTION || node.NodeType == BehaviourNodeType.CONDITION)
            {
                LeafNode actionNode = (LeafNode)node;
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
            else if (node.NodeType == BehaviourNodeType.SUBTREE)
            {
                SubtreeNode subtreeNode = (SubtreeNode)node;
                serializedNode.bindings = subtreeNode.bindings != null ? new List<SubtreeBinding>(subtreeNode.bindings) : new List<SubtreeBinding>();
                if (subtreeNode.subTreeAsset != null)
                {
                    string path = AssetDatabase.GetAssetPath(subtreeNode.subTreeAsset);
                    serializedNode.subtreeAssetGUID = AssetDatabase.AssetPathToGUID(path);
                }
            }
            
            return serializedNode;
        }

        #endregion
    }
}