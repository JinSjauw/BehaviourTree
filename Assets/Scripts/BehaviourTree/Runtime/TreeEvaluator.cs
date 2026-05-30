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
        private const int maxIterations = 10000;

        private NodeData[] nodeDatas;
        private FieldData[] fieldDatas;
        private List<EvaluatorFrame> nodeStack;
        public int currentNodeIndex { get; private set; } = -1;
        public NodeState[] nodeStates;

        public TreeEvaluator(NodeData[] nodeDatas, FieldData[] fieldDatas)
        {
            Debug.Log("Created Tree Evaluator");
            this.nodeDatas = nodeDatas;
            this.fieldDatas = fieldDatas;
            nodeStack = new List<EvaluatorFrame>();
            nodeStates = new NodeState[nodeDatas.Length];
        }

        public void Evaluate(BlackBoard blackBoard)
        {
            if (nodeStack.Count == 0)
            {
                if (nodeDatas[0].firstChildIndex < 0)
                    return;
                nodeStack.Add(new EvaluatorFrame { nodeIndex = 0, childIndex = 0, lastChildStatus = NodeState.NONE });
            }

            Array.Clear(nodeStates, 0, nodeStates.Length);
            currentNodeIndex = -1;

            UnwindSpecialComposites();

            EvaluatorContext context = new EvaluatorContext(nodeStack, nodeDatas, fieldDatas, nodeStates, blackBoard);

            int iterationGuard = 0;

            while (nodeStack.Count > 0)
            {
                if (iterationGuard > maxIterations)
                {
                    Debug.LogError($"TreeEvaluator exceeded {maxIterations} iterations. Possible infinite loop in behaviour tree. Aborting evaluation.");
                    nodeStack.Clear();
                    break;
                }

                iterationGuard++;

                INodeHandler handler = NodeHandlerRegistry.GetHandler(nodeDatas[nodeStack[nodeStack.Count - 1].nodeIndex].nodeType);
                if (handler == null)
                {
                    Debug.LogError($"No handler registered for node type {nodeDatas[nodeStack[nodeStack.Count - 1].nodeIndex].nodeType}");
                    nodeStack.RemoveAt(nodeStack.Count - 1);
                    continue;
                }

                handler.Process(context);

                if (context.ShouldBreak)
                    break;
            }
        }

        private void UnwindSpecialComposites()
        {
            for (int i = nodeStack.Count - 1; i >= 0; i--)
            {
                BehaviourNodeType nodeType = nodeDatas[nodeStack[i].nodeIndex].nodeType;

                if (nodeType == BehaviourNodeType.PRIORITY)
                {
                    if (nodeStack.Count > i + 1)
                        nodeStack.RemoveRange(i + 1, nodeStack.Count - i - 1);

                    var frame = nodeStack[i];
                    frame.childIndex = 0;
                    frame.lastChildStatus = NodeState.NONE;
                }
            }
        }
    }
}
