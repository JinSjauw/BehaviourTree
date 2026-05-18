using System;
using System.Collections.Generic;
using BehaviourTree.Core;
using UnityEditor;
using UnityEngine;

namespace BehaviourTree.Editor 
{
    [CreateAssetMenu(fileName = "BehaviourTreeAsset", menuName = "BehaviourTree/BTAsset")]
    public class BehaviourTreeAsset : ScriptableObject, IBehaviourTreeAuthoringAsset
    {
        [HideInInspector] public List<BehaviourNode> nodesList;
        [HideInInspector] public BehaviourNode rootCopy;
        [HideInInspector] public BlackboardDefinition blackboardDefinition;

        public BehaviourNode Root => rootCopy;
        public BlackboardDefinition BlackboardDefinition => blackboardDefinition;
        public string DisplayName => name;

        //Create unique runtime instances of the SO's
        public void Initialize() 
        {
            nodesList = new List<BehaviourNode>{ rootCopy };

            for(int i = 0; i < rootCopy.children.Count; i++) 
            {
                BehaviourNode nodeCopy = Instantiate(rootCopy.children[i]);
            
                rootCopy.children[i] = nodeCopy;
                nodesList.Add(nodeCopy);

                for(int j = 0; j < nodeCopy.children.Count; j++)
                {
                    BehaviourNode childCopy = Instantiate(nodeCopy.children[j]);
                    nodeCopy.children[j] = childCopy;
                    nodesList.Add(childCopy);
                }
            }
        }

        public void CreateBlackBoard()
        {
            BlackboardDefinition createdBlackboard = CreateInstance<BlackboardDefinition>();
            createdBlackboard.name = this.name + "_BB_Definition";

            blackboardDefinition = createdBlackboard;
            AssetDatabase.AddObjectToAsset(createdBlackboard, this);
            AssetDatabase.SaveAssets();
        }

        public BehaviourNode CreateNode(Type type)
        {
            BehaviourNode node = (BehaviourNode)CreateInstance(type);
            node.name = type.Name;
            node.guid = GUID.Generate().ToString();
        
            return node;
        }

        public void RegisterNode(BehaviourNode node)
        {
            Undo.RecordObject(this, "(BTree) Register Node");
            nodesList.Add(node);

            AssetDatabase.AddObjectToAsset(node, this);
            Undo.RegisterCreatedObjectUndo(node, "(BTree) Register Node");
            
            EditorUtility.SetDirty(this);
        }

        public bool NeedsNodesListSync()
        {
            if (nodesList == null) return true;

            HashSet<BehaviourNode> nodeSet = new HashSet<BehaviourNode>();
            HashSet<string> guidSet = new HashSet<string>();

            for (int i = 0; i < nodesList.Count; i++)
            {
                BehaviourNode node = nodesList[i];
                if (node == null) return true;

                if (!nodeSet.Add(node)) return true;

                string key = !string.IsNullOrEmpty(node.guid) ? node.guid : node.GetInstanceID().ToString();
                if (!guidSet.Add(key)) return true;
            }

            if (rootCopy != null && !nodeSet.Contains(rootCopy)) return true;

            for (int i = 0; i < nodesList.Count; i++)
            {
                BehaviourNode node = nodesList[i];
                if (node == null) continue;

                List<BehaviourNode> children = node.children;
                if (children == null) continue;

                for (int j = 0; j < children.Count; j++)
                {
                    BehaviourNode child = children[j];
                    if (child == null) continue;
                    if (!nodeSet.Contains(child)) return true;
                }
            }

            return false;
        }

        public void SyncNodesListFromAssets()
        {
            string path = AssetDatabase.GetAssetPath(this);
            if (string.IsNullOrEmpty(path)) return;

            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            if (assets == null || assets.Length == 0) return;

            List<BehaviourNode> found = new List<BehaviourNode>();
            HashSet<string> seen = new HashSet<string>();

            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is not BehaviourNode node) continue;

                string key = !string.IsNullOrEmpty(node.guid) ? node.guid : node.GetInstanceID().ToString();
                if (!seen.Add(key)) continue;

                found.Add(node);
            }

            if (rootCopy != null)
            {
                for (int i = found.Count - 1; i >= 0; i--)
                {
                    if (found[i] == rootCopy)
                        found.RemoveAt(i);
                }
                found.Insert(0, rootCopy);
            }

            if (found.Count > 1)
            {
                int startIdx = rootCopy != null ? 1 : 0;
                List<BehaviourNode> rest = new List<BehaviourNode>();
                for (int i = startIdx; i < found.Count; i++)
                    rest.Add(found[i]);

                rest.Sort((a, b) =>
                {
                    int cmp = a.graphPosition.y.CompareTo(b.graphPosition.y);
                    if (cmp != 0) return cmp;
                    cmp = a.graphPosition.x.CompareTo(b.graphPosition.x);
                    if (cmp != 0) return cmp;
                    cmp = string.CompareOrdinal(a.name, b.name);
                    if (cmp != 0) return cmp;
                    return string.CompareOrdinal(a.guid, b.guid);
                });

                int write = startIdx;
                for (int i = 0; i < rest.Count; i++)
                    found[write++] = rest[i];
            }

            bool changed = nodesList == null || nodesList.Count != found.Count;
            if (!changed && nodesList != null)
            {
                for (int i = 0; i < found.Count; i++)
                {
                    if (nodesList[i] != found[i])
                    {
                        changed = true;
                        break;
                    }
                }
            }

            if (!changed) return;

            Undo.RecordObject(this, "(BTree) Sync Nodes");
            nodesList = found;
            EditorUtility.SetDirty(this);
        }

        public void DeleteNode(BehaviourNode node) 
        {
            Undo.RecordObject(this, "(BTree) Delete Node");
            nodesList.Remove(node);
            Undo.DestroyObjectImmediate(node);
        }

        public void AddChild(BehaviourNode parent, BehaviourNode child) 
        {
            if(parent.NodeType != BehaviourNodeType.ACTION && parent.NodeType != BehaviourNodeType.CONDITION) 
            {
                if (child.NodeType == BehaviourNodeType.ROOT) 
                {
                    Debug.LogError("ROOT NODE CANNOT BE CHILD!");
                    return;
                }

                if (!parent.children.Contains(child)) 
                {
                    if (!nodesList.Contains(child))
                    {
                        RegisterNode(child);
                    }

                    Undo.RecordObject(parent, "(BTree) Add Child");
                    parent.children.Add(child);
                    EditorUtility.SetDirty(parent);
                }
                else 
                {
                    Debug.LogWarning("Parent already contains child!");
                }
            }
        }

        public void RemoveChild(BehaviourNode parent, BehaviourNode child)
        {
            if(parent.children.Contains(child)) 
            {
                Undo.RecordObject(parent, "(BTree) Remove Child");
                parent.children.Remove(child);
                EditorUtility.SetDirty(parent);
            }
            else 
            {
                Debug.LogWarning("Parent does not contain child");
            }
        }

        public void ClearNodes() 
        {
            for(int i = 0; i < nodesList.Count; i++) 
            {
                Destroy(nodesList[i]);
            }

            Destroy(rootCopy);

            rootCopy = null;
            nodesList.Clear();
        }
    }
}
