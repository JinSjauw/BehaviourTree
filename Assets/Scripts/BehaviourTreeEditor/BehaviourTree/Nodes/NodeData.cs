namespace BTreeEditor
{
    [System.Serializable]
    public unsafe struct NodeData
    {
        public BehaviourNodeType nodeType;
        public int firstChildIndex;
        public int lastChildIndex;

        public MethodID methodID;
        public BlackBoardType blackBoardTypeID;
        public fixed byte configByteBlob[32];
    }
}
