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
        public int currentNodeIndex { get; private set; } = -1;
        public NodeState[] nodeStates;

        public TreeEvaluator(NodeData[] nodeDatas, FieldData[] fieldDatas)
        {
            Debug.Log("Created Tree Evaluator");
            this.nodeDatas = nodeDatas;
            this.fieldDatas = fieldDatas;
            nodeStack = new Stack<EvaluatorFrame>();
            nodeStates = new NodeState[nodeDatas.Length];
        }

        public void Evaluate(BlackBoard blackBoard)
        {
            if (nodeStack.Count == 0)
                nodeStack.Push(new EvaluatorFrame { nodeIndex = 0, childIndex = 0, lastChildStatus = NodeState.NONE });

            Array.Clear(nodeStates, 0, nodeStates.Length);
            currentNodeIndex = -1;

            EvaluatorContext context = new EvaluatorContext(nodeStack, nodeDatas, fieldDatas, nodeStates, blackBoard);

            while (nodeStack.Count > 0)
            {
                INodeHandler handler = NodeHandlerRegistry.GetHandler(nodeDatas[nodeStack.Peek().nodeIndex].nodeType);
                if (handler == null)
                {
                    Debug.LogError($"No handler registered for node type {nodeDatas[nodeStack.Peek().nodeIndex].nodeType}");
                    nodeStack.Pop();
                    continue;
                }

                handler.Process(context);

                if (context.ShouldBreak)
                    break;
            }
        }
    }
}

