using BehaviourTree;
using UnityEngine;

[CreateAssetMenu(fileName = "RuntimeBTreeAsset", menuName = "BehaviourTree/RuntimeBTreeAsset")]
public class RuntimeBTreeAsset : ScriptableObject
{
    public NodeData[] runtimeTreeData;
}
