using System;
using System.Collections.Generic;
using BehaviourTree.Core;
using Codice.Client.Common.TreeGrouper;

namespace BehaviourTree.Runtime
{
    /// <summary>
    /// Provides handlers with safe access to the evaluator's stack, data, and blackboard.
    /// </summary>
    public class EvaluatorContext
    {
        private readonly Stack<EvaluatorFrame> stack;
        private readonly NodeData[] nodeDatas;
        private readonly FieldData[] fieldDatas;
        private readonly NodeState[] nodeStates;

        public BlackBoard BlackBoard { get; }
        public int CurrentNodeIndex { get; set; }
        public bool ShouldBreak { get; set; }

        public EvaluatorContext( Stack<EvaluatorFrame> stack, NodeData[] nodeDatas, FieldData[] fieldDatas, NodeState[] nodeStates, BlackBoard blackBoard)
        {
            this.stack = stack;
            this.nodeDatas = nodeDatas;
            this.fieldDatas = fieldDatas;
            this.nodeStates = nodeStates;
            BlackBoard = blackBoard;
            CurrentNodeIndex = -1;
            ShouldBreak = false;
        }

        public EvaluatorFrame CurrentFrame => stack.Peek();
        public ref NodeData CurrentNode => ref nodeDatas[stack.Peek().nodeIndex];
        public NodeState[] NodeStates => nodeStates;
        //public ReadOnlySpan<FieldData> FieldDatas => fieldDatas;

        /// <summary>
        /// Push a child node with a fresh frame.
        /// </summary>
        public void PushChild(int childNodeIndex)
        {
            stack.Push(new EvaluatorFrame
            {
                nodeIndex = childNodeIndex,
                childIndex = 0,
                lastChildStatus = NodeState.NONE
            });
        }

        /// <summary>
        /// Pop the current frame and notify the parent of the result.
        /// </summary>
        public void PopAndNotifyParent(NodeState result)
        {
            nodeStates[stack.Peek().nodeIndex] = result;
            stack.Pop();

            if (stack.Count > 0)
            {
                stack.Peek().lastChildStatus = result;
            }
        }

        /// <summary>
        /// Mark the current node as RUNNING in the state array.
        /// </summary>
        public void MarkCurrentNodeRunning()
        {
            nodeStates[stack.Peek().nodeIndex] = NodeState.RUNNING;
        }

        /// <summary>
        /// Invokes the leaf node's method from the MethodRegistry.
        /// </summary>
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

        public void SetStackRunning()
        {
            foreach (var frame in stack)
            {
                if (nodeStates[frame.nodeIndex] == NodeState.NONE)
                {
                    nodeStates[frame.nodeIndex] = NodeState.RUNNING;
                }
            }
        }
    }
}
