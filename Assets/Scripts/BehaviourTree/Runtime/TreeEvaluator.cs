using System;
using System.Collections.Generic;
using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Runtime 
{
    public class EvaluatorFrame 
    {
        public int nodeIndex;
        public int childIndex;
        public NodeState lastChildStatus;
    }

    public class TreeEvaluator
    {
        private NodeData[] nodeDatas;
        private FieldData[] fieldDatas;
        private Stack<EvaluatorFrame> nodeStack;

        public TreeEvaluator(NodeData[] nodeDatas, FieldData[] fieldDatas)
        {
            this.nodeDatas = nodeDatas;
            this.fieldDatas = fieldDatas;
            nodeStack = new Stack<EvaluatorFrame>();
        }

        public void Evaluate(BlackBoard blackBoard)
        {
            if (nodeStack.Count == 0)
                nodeStack.Push(new EvaluatorFrame { nodeIndex = 0, childIndex = 0, lastChildStatus = NodeState.NONE });

            while (nodeStack.Count > 0)
            {
                EvaluatorFrame frame = nodeStack.Peek();
                NodeData node = nodeDatas[frame.nodeIndex];

                if (node.nodeType == BehaviourNodeType.ACTION || node.nodeType == BehaviourNodeType.CONDITION)
                {
                    NodeState status = EvaluateLeaf(blackBoard, ref node);
                    if (status == NodeState.RUNNING)
                    {
                        break;
                    }
                    else
                    {
                        nodeStack.Pop();
                        if (nodeStack.Count > 0)
                        {
                            EvaluatorFrame parentFrame = nodeStack.Peek();
                            parentFrame.lastChildStatus = status;
                        }
                    }
                }
                else if (node.nodeType == BehaviourNodeType.SEQUENCE)
                {
                    int childCount = node.lastChildIndex - node.firstChildIndex + 1;

                    if (frame.lastChildStatus != NodeState.NONE)
                    {
                        if (frame.lastChildStatus == NodeState.FAILURE)
                        {
                            nodeStack.Pop();
                            if (nodeStack.Count > 0)
                                nodeStack.Peek().lastChildStatus = NodeState.FAILURE;
                            continue;
                        }
                        else if (frame.lastChildStatus == NodeState.SUCCESS)
                        {
                            frame.childIndex++;
                            frame.lastChildStatus = NodeState.NONE;
                            if (frame.childIndex >= childCount)
                            {
                                nodeStack.Pop();
                                if (nodeStack.Count > 0)
                                    nodeStack.Peek().lastChildStatus = NodeState.SUCCESS;
                                continue;
                            }
                        }
                    }
                    int childNodeIndex = node.firstChildIndex + frame.childIndex;
                    nodeStack.Push(new EvaluatorFrame { nodeIndex = childNodeIndex, childIndex = 0, lastChildStatus = NodeState.NONE });
                }
                else if (node.nodeType == BehaviourNodeType.SELECTOR)
                {
                    int childCount = node.lastChildIndex - node.firstChildIndex + 1;

                    if (frame.lastChildStatus != NodeState.NONE)
                    {
                        if (frame.lastChildStatus == NodeState.SUCCESS)
                        {
                            nodeStack.Pop();
                            if (nodeStack.Count > 0)
                                nodeStack.Peek().lastChildStatus = NodeState.SUCCESS;
                            continue;
                        }
                        else if (frame.lastChildStatus == NodeState.FAILURE)
                        {
                            frame.childIndex++;
                            frame.lastChildStatus = NodeState.NONE;
                            if (frame.childIndex >= childCount)
                            {
                                nodeStack.Pop();
                                if (nodeStack.Count > 0)
                                    nodeStack.Peek().lastChildStatus = NodeState.FAILURE;
                                continue;
                            }
                        }
                    }

                    int childNodeIndex = node.firstChildIndex + frame.childIndex;
                    nodeStack.Push(new EvaluatorFrame { nodeIndex = childNodeIndex, childIndex = 0, lastChildStatus = NodeState.NONE });
                }
            }
        }

        private NodeState EvaluateLeaf(BlackBoard blackBoard, ref NodeData nodeData) 
        {
            BehaviorMethod method = MethodRegistry.GetMethod(nodeData.methodID);

            if (method == null) 
            {
                Debug.LogError($"Method not found! returning FAILURE state {nodeData.methodID}");
                return NodeState.FAILURE;
            }

            ReadOnlySpan<FieldData> fieldsSlice = default;
            if (nodeData.fieldDataCount > 0 && fieldDatas != null && nodeData.fieldDataStartIndex >= 0)
            {
                fieldsSlice = new ReadOnlySpan<FieldData>(fieldDatas, nodeData.fieldDataStartIndex, nodeData.fieldDataCount);
            }

            return method.Invoke(blackBoard, fieldsSlice);
        }
    }
}

