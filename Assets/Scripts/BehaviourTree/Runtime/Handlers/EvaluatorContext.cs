using System;
using System.Collections.Generic;
using BehaviourTree.Core;

namespace BehaviourTree.Runtime
{
    public class EvaluatorContext
    {
        private readonly EvaluatorFrame[] stack;
        private readonly NodeData[] nodeDatas;
        private readonly FieldData[] fieldDatas;
        private readonly NodeState[] nodeStates;
        private readonly Dictionary<int, ParallelChildState[]> parallelStates;

        public BlackBoard BlackBoard { get; private set; }
        public int CurrentNodeIndex { get; set; }
        public bool ShouldBreak { get; set; }
        public int FrameCount { get; set; }

        public EvaluatorContext(
            EvaluatorFrame[] stack,
            NodeData[] nodeDatas,
            FieldData[] fieldDatas,
            NodeState[] nodeStates,
            Dictionary<int, ParallelChildState[]> parallelStates)
        {
            this.stack = stack;
            this.nodeDatas = nodeDatas;
            this.fieldDatas = fieldDatas;
            this.nodeStates = nodeStates;
            this.parallelStates = parallelStates;
        }

        public void Reset(int frameCount, BlackBoard blackBoard)
        {
            FrameCount = frameCount;
            BlackBoard = blackBoard;
            CurrentNodeIndex = -1;
            ShouldBreak = false;
        }

        public ref EvaluatorFrame CurrentFrame => ref stack[FrameCount - 1];
        public ref NodeData CurrentNode => ref nodeDatas[stack[FrameCount - 1].nodeIndex];
        public NodeState[] NodeStates => nodeStates;
        public Dictionary<int, ParallelChildState[]> ParallelStates => parallelStates;

        public void PushChild(int childNodeIndex)
        {
            stack[FrameCount++] = new EvaluatorFrame
            {
                nodeIndex = childNodeIndex,
                childIndex = 0,
                lastChildStatus = NodeState.NONE
            };
        }

        public void PopAndNotifyParent(NodeState result)
        {
            nodeStates[stack[FrameCount - 1].nodeIndex] = result;
            FrameCount--;

            if (FrameCount > 0)
            {
                stack[FrameCount - 1].lastChildStatus = result;
            }
        }

        public void UpdateNodeStatus(NodeState status, int nodeIndex)
        {
            nodeStates[nodeIndex] = status;
        }

        public void MarkCurrentNodeRunning()
        {
            nodeStates[stack[FrameCount - 1].nodeIndex] = NodeState.RUNNING;
        }

        public NodeState EvaluateLeaf(ref NodeData nodeData)
        {
            BehaviorMethod method = MethodRegistry.GetMethod(nodeData.methodID);
            if (method == null)
            {
                UnityEngine.Debug.LogError($"Method not found! Returning FAILURE state {nodeData.methodID}");
                return NodeState.FAILURE;
            }

            ReadOnlySpan<FieldData> fieldsSlice = default;
            if (nodeData.fieldDataCount > 0 && fieldDatas != null && nodeData.fieldDataStartIndex >= 0)
            {
                fieldsSlice = new ReadOnlySpan<FieldData>(
                    fieldDatas,
                    nodeData.fieldDataStartIndex,
                    nodeData.fieldDataCount
                );
            }

            return method.Invoke(BlackBoard, fieldsSlice);
        }

        public ReadOnlySpan<FieldData> GetNodeFields(NodeData node)
        {
            if (node.fieldDataCount > 0 && fieldDatas != null && node.fieldDataStartIndex >= 0)
            {
                return new ReadOnlySpan<FieldData>(fieldDatas, node.fieldDataStartIndex, node.fieldDataCount);
            }
            return default;
        }

        public ref NodeData GetNodeData(int nodeIndex)
        {
            return ref nodeDatas[nodeIndex];
        }

        public void SetStackRunning()
        {
            for (int i = 0; i < FrameCount; i++)
            {
                if (nodeStates[stack[i].nodeIndex] == NodeState.NONE)
                {
                    nodeStates[stack[i].nodeIndex] = NodeState.RUNNING;
                }
            }
        }
    }
}