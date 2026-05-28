using System;
using System.Collections.Generic;
using BehaviourTree.Core;

namespace BehaviourTree.Runtime
{
    public class EvaluatorContext
    {
        private readonly List<EvaluatorFrame> stack;
        private readonly NodeData[] nodeDatas;
        private readonly FieldData[] fieldDatas;
        private readonly NodeState[] nodeStates;

        public BlackBoard BlackBoard { get; }
        public int CurrentNodeIndex { get; set; }
        public bool ShouldBreak { get; set; }

        public EvaluatorContext(List<EvaluatorFrame> stack, NodeData[] nodeDatas, FieldData[] fieldDatas, NodeState[] nodeStates, BlackBoard blackBoard)
        {
            this.stack = stack;
            this.nodeDatas = nodeDatas;
            this.fieldDatas = fieldDatas;
            this.nodeStates = nodeStates;
            BlackBoard = blackBoard;
            CurrentNodeIndex = -1;
            ShouldBreak = false;
        }

        public EvaluatorFrame CurrentFrame => stack[stack.Count - 1];
        public ref NodeData CurrentNode => ref nodeDatas[stack[stack.Count - 1].nodeIndex];
        public NodeState[] NodeStates => nodeStates;

        public void PushChild(int childNodeIndex)
        {
            stack.Add(new EvaluatorFrame
            {
                nodeIndex = childNodeIndex,
                childIndex = 0,
                lastChildStatus = NodeState.NONE
            });
        }

        public void PopAndNotifyParent(NodeState result)
        {
            nodeStates[stack[stack.Count - 1].nodeIndex] = result;
            stack.RemoveAt(stack.Count - 1);

            if (stack.Count > 0)
            {
                var parentFrame = stack[stack.Count - 1];
                parentFrame.lastChildStatus = result;
                stack[stack.Count - 1] = parentFrame;
            }
        }

        public void UpdateNodeStatus(NodeState status, int nodeIndex)
        {
            nodeStates[nodeIndex] = status;
        }

        public void MarkCurrentNodeRunning()
        {
            nodeStates[stack[stack.Count - 1].nodeIndex] = NodeState.RUNNING;
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
            for (int i = 0; i < stack.Count; i++)
            {
                if (nodeStates[stack[i].nodeIndex] == NodeState.NONE)
                {
                    nodeStates[stack[i].nodeIndex] = NodeState.RUNNING;
                }
            }
        }
    }
}
