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

    /// <summary>
    /// Boxed constants for types larger than 4 bytes (Vector2, Vector3, Color, custom types).
    /// Indexed by FieldData.value when FieldData.IsBoxedConstant is true.
    /// </summary>
    public object[] boxedConstants;

    public BlackboardDefinition blackboardDefinition;

    public int maxTreeDepth;

    /// <summary>GUID of the source tree asset. Populated during bake. Used for matching tracked bindings in builds.</summary>
    public string sourceTreeGuid;

#if UNITY_EDITOR
    [HideInInspector] public UnityEngine.Object sourceTree;
#endif

}

