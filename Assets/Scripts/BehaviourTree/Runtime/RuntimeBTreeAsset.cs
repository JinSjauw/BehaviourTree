using BehaviourTree;
using BehaviourTree.Core;
using BehaviourTree.Editor;
using UnityEngine;

[CreateAssetMenu(fileName = "RuntimeBTreeAsset", menuName = "BehaviourTree/RuntimeBTreeAsset")]
public class RuntimeBTreeAsset : ScriptableObject
{
    public NodeData[] runtimeNodeData;

    /// <summary>Packed field data for all leaf nodes.</summary>
    public FieldData[] runtimeFieldData;

    public BlackboardDefinition blackboardDefinition;

    [HideInInspector] public BehaviourTreeAsset sourceTree;

}

