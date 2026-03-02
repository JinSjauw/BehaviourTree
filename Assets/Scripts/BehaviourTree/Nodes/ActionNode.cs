using UnityEngine;

[CreateAssetMenu(fileName = "Action Node", menuName = "Scriptable Objects/BT Nodes/Action Node")]
public class ActionNode : BehaviourNode
{
    public override BehaviourNodeType NodeType => BehaviourNodeType.ACTION;

    public MethodID methodID;

    //Maybe also a string/enum to find the corresponding param collection.
    //paramSetType -> Find correct registry. 

    public ParamSetID paramSetType;
    public BlackBoardType BlackBoardTypeID;
}
