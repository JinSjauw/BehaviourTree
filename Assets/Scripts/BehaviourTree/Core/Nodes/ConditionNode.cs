using System.Collections.Generic;
using UnityEngine;

namespace BehaviourTree.Core 
{
    [CreateAssetMenu(fileName = "Condition Node", menuName = "Scriptable Objects/BT Nodes/Condition Node")]
    public class ConditionNode : BehaviourNode
    {
        public override BehaviourNodeType NodeType => BehaviourNodeType.CONDITION;

        public MethodID methodID;

        /// <summary>Dynamic list of field entries – generated from *_Params metadata.</summary>
        public List<NodeFieldEntry> fieldEntries = new List<NodeFieldEntry>();
        public BlackBoardType BlackBoardTypeID;
    }
}

