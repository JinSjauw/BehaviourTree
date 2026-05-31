using BehaviourTree;
using BehaviourTree.Core;
using UnityEngine;

public class RuntimeBehaviourTreeAsset : ScriptableObject
{
    /// <summary>flattened behaviour tree</summary>
    public NodeData[] runtimeNodeData;

    public string[] runtimeNodeGuids;

    /// <summary>Packed field data for all leaf nodes.</summary>
    public FieldData[] runtimeFieldData;

    public BlackboardDefinition blackboardDefinition;

    public int maxTreeDepth;

#if UNITY_EDITOR
    [HideInInspector] public UnityEngine.Object sourceTree;
#endif

}

