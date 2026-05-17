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
        public List<BehaviourNode> nodesList;
        public BehaviourNode rootCopy;
        public BlackboardDefinition blackboardDefinition;

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

            Undo.RecordObject(this, "(BTree) Create Node");
            nodesList.Add(node);

            AssetDatabase.AddObjectToAsset(node, this);
            Undo.RegisterCreatedObjectUndo(node, "(BTree) Create Node");
            AssetDatabase.SaveAssets();

            return node;
        }

        public void RegisterNode(BehaviourNode node)
        {
            Undo.RecordObject(this, "(BTree) Register Node");
            nodesList.Add(node);

            AssetDatabase.AddObjectToAsset(node, this);
            Undo.RegisterCreatedObjectUndo(node, "(BTree) Register Node");
            AssetDatabase.SaveAssets();
        }

        public void DeleteNode(BehaviourNode node) 
        {
            Undo.RecordObject(this, "(BTree) Create Node");
            nodesList.Remove(node);
            Undo.DestroyObjectImmediate(node);
        }

        public void AddChild(BehaviourNode parent, BehaviourNode child) 
        {
            if(parent.NodeType != BehaviourNodeType.ACTION || parent.NodeType != BehaviourNodeType.CONDITION) 
            {
                if (child.NodeType == BehaviourNodeType.ROOT) 
                {
                    Debug.LogError("ROOT NODE CANNOT BE CHILD!");
                    return;
                }

                if (!parent.children.Contains(child)) 
                {
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
