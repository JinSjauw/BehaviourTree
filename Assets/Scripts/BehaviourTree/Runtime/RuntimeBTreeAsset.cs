using BehaviourTree;
using BehaviourTree.Core;
using UnityEngine;

[CreateAssetMenu(fileName = "RuntimeBTreeAsset", menuName = "BehaviourTree/RuntimeBTreeAsset")]
public class RuntimeBTreeAsset : ScriptableObject
{
    /// <summary>flattened behaviour tree</summary>
    public NodeData[] runtimeNodeData;

    /// <summary>Packed field data for all leaf nodes.</summary>
    public FieldData[] runtimeFieldData;

    public BlackboardDefinition blackboardDefinition;

#if UNITY_EDITOR
    [HideInInspector] public UnityEngine.Object sourceTree;
#endif

}

