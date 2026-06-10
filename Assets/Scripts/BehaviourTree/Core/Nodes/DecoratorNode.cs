using System.Collections.Generic;
using UnityEngine;

namespace BehaviourTree.Core
{
    //[CreateAssetMenu(fileName = "Action Node", menuName = "Scriptable Objects/BT Nodes/Action Node")]
    public class DecoratorNode : BehaviourNode
    {
        public override BehaviourNodeType NodeType => BehaviourNodeType.DECORATOR;

        /// <summary>Class-based method name. Populated by editor when user selects a method.</summary>
        public string methodName;
        
        /// <summary>Dynamic list of field entries – generated from *_Params metadata.</summary>
        public List<NodeFieldEntry> fieldEntries = new List<NodeFieldEntry>();

        [HideInInspector] public BlackBoardType BlackBoardTypeID;
    }
}