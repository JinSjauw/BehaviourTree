using UnityEngine;

namespace BTreeEditor 
{
    [CreateAssetMenu(fileName = "Condition Node", menuName = "Scriptable Objects/BT Nodes/Condition Node")]
    public class ConditionNode : BehaviourNode
    {
        public override BehaviourNodeType NodeType => BehaviourNodeType.CONDITION;

        public MethodID methodID;

        //Maybe also a string/enum to find the corresponding param collection.
        //paramSetType -> Find correct registry. 

        //Dynamic blackBoard DATA
        //public ParamSetID paramSetID;
        public BlackBoardType BlackBoardTypeID;

        //Static DATA
        public StaticConfigSetID configParamSetID;
        public NodeConfigID treeConfigID;
    }
}

