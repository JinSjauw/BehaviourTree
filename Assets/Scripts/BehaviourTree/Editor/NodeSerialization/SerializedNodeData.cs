
using System;
using System.Collections.Generic;
using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Editor
{
    [Serializable]
    public class SerializedNodeData
    {
        public BehaviourNodeType nodeType;
        public List<BehaviourNode> children;
        public MethodID methodID;
        public List<NodeFieldEntry> fieldEntries;
        public BlackBoardType BlackBoardTypeID;

        public string guid;
        public Vector2 graphPosition;
    }

    [Serializable]
    public class SerializedEdgeData
    {
        public string sourceNodeGUID;
        public string targetNodeGUID;
        public int sourceOutputIndex;
        public int targetInputIndex;
    }
}

