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
        private readonly NodeMethod[] methodInstances;
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
            NodeMethod[] methodInstances,
            NodeState[] nodeStates,
            Dictionary<int, ParallelChildState[]> parallelStates)
        {
            this.stack = stack;
            this.nodeDatas = nodeDatas;
            this.fieldDatas = fieldDatas;
            this.methodInstances = methodInstances;
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

        public NodeState EvaluateLeaf(int nodeIndex, ref NodeData nodeData)
        {
            NodeMethod method = methodInstances[nodeIndex];
            if (method == null)
            {
                UnityEngine.Debug.LogError($"Method instance not found for node index {nodeIndex}. Returning FAILURE.");
                return NodeState.FAILURE;
            }

            method.ResolveInputs(BlackBoard);

            NodeState result;
            if (method is ActionMethod action)
                result = action.Execute();
            else if (method is ConditionMethod condition)
                result = condition.Execute();
            else
                return NodeState.FAILURE;

            method.WriteOutputs(BlackBoard);
            return result;
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

        /// <summary>Returns the class-based method instance for a given node index, or null.</summary>
        public NodeMethod GetMethodInstance(int nodeIndex)
        {
            if (methodInstances == null || nodeIndex < 0 || nodeIndex >= methodInstances.Length)
                return null;
            return methodInstances[nodeIndex];
        }
    }
}