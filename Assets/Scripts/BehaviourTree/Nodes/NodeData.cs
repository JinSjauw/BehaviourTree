[System.Serializable]
public unsafe struct NodeData
{
    public BehaviourNodeType nodeType;
    public int firstChildIndex;
    public int lastChildIndex;

    public MethodID methodID;
    public ParamSetID paramSetID;
    public BlackBoardType blackBoardTypeID;

    public StaticConfigSetID staticConfigSetID;
    public TreeConfigID treeConfigID;

    public fixed byte configByteBlob[32];
    //public byte[] configByteBlob;
}
