using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BehaviourTreeAsset", menuName = "Scriptable Objects/BT")]
public class BehaviourTreeAsset : ScriptableObject
{
    public List<BehaviourNode> nodesList;
    public BehaviourNode root;
}
