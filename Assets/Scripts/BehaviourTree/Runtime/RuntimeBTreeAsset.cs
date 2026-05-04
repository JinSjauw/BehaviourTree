using BehaviourTree;
using BehaviourTree.Core;
using UnityEngine;

[CreateAssetMenu(fileName = "RuntimeBTreeAsset", menuName = "BehaviourTree/RuntimeBTreeAsset")]
public class RuntimeBTreeAsset : ScriptableObject
{
    public NodeData[] runtimeTreeData;

    /// <summary>Packed field data for all leaf nodes.</summary>
    public FieldData[] runtimeFieldData;
}

