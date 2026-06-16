namespace BehaviourTree.Core
{
    [System.Serializable]
    public struct NodeData
    {
        public BehaviourNodeType nodeType;
        public int firstChildIndex;
        public int lastChildIndex;

        /// <summary>Class-based method name.</summary>
        public string methodName;
        public BlackBoardType blackBoardTypeID;

        /// <summary>Index into the parallel FieldData[] array where this node's fields begin.</summary>
        public int fieldDataStartIndex;
        /// <summary>Number of FieldData entries belonging to this node.</summary>
        public int fieldDataCount;

        /// <summary>Conditional abort type. Only meaningful for COMPOSITE nodes.</summary>
        public AbortType abortType;
    }
}

