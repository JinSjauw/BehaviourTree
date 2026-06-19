using System.Collections.Generic;
using UnityEngine;

namespace BehaviourTree.Core
{
    //[CreateAssetMenu(fileName = "Action Node", menuName = "Scriptable Objects/BT Nodes/Action Node")]
    public class LeafNode : BehaviourNode
    {
        [HideInInspector] [SerializeField] private BehaviourNodeType leafType = BehaviourNodeType.ACTION;

        public override BehaviourNodeType NodeType => leafType;

        /// <summary>Class-based method name. Populated by editor when user selects a method.</summary>
        public string methodName;
        
        /// <summary>Dynamic list of field entries – generated from *_Params metadata.</summary>
        public List<NodeFieldEntry> fieldEntries = new List<NodeFieldEntry>();

        //[SerializeField] public string comment;

        [HideInInspector] public BlackBoardType BlackBoardTypeID;

        public void SetLeafType(BehaviourNodeType type)
        {
            if (type != BehaviourNodeType.ACTION && type != BehaviourNodeType.CONDITION)
            {
                Debug.LogError($"Invalid leaf type '{type}' assigned to LeafNode. Ignoring.");
                return;
            }
            leafType = type;
        }
    }
}
