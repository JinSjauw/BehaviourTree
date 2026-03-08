[System.Serializable]
public struct NodeData
{
    public BehaviourNodeType nodeType;
    public int firstChildIndex;
    public int lastChildIndex;

    public MethodID methodID;
    public ParamSetID paramSetTypeID;
    public BlackBoardType BlackBoardTypeID;
}
