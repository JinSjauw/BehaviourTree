using System;
using System.Collections.Generic;
using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Runtime 
{
    public struct EvaluatorFrame 
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
        private EvaluatorFrame[] frameStack;
        private int frameCount;
        private Dictionary<int, ParallelChildState[]> parallelStates;
        private EvaluatorContext context;
        private bool isInitialized = false;

        public int currentNodeIndex { get; private set; } = -1;
        public NodeState[] nodeStates;

        public TreeEvaluator(NodeData[] nodeDatas, FieldData[] fieldDatas, int maxTreeDepth)
        {
            Debug.Log("Created Tree Evaluator");
            this.nodeDatas = nodeDatas;
            this.fieldDatas = fieldDatas;
            frameStack = new EvaluatorFrame[maxTreeDepth + 1];
            nodeStates = new NodeState[nodeDatas.Length];
            parallelStates = new Dictionary<int, ParallelChildState[]>();
            context = new EvaluatorContext(frameStack, nodeDatas, fieldDatas, nodeStates, parallelStates);

            if (nodeDatas == null || fieldDatas == null || nodeDatas.Length == 0)
            {
                Debug.LogError("TreeEvaluator: nodeDatas or fieldDatas is NULL");
                return;
            }

            isInitialized = true;
        }

        public void Evaluate(BlackBoard blackBoard)
        {
            if (!isInitialized)
            {
                Debug.LogError("TreeEvaluator: not initialized");
                return;
            }

            if (frameCount == 0)
            {
                if (nodeDatas[0].firstChildIndex < 0) return;
                frameStack[frameCount++] = new EvaluatorFrame { nodeIndex = 0, childIndex = 0, lastChildStatus = NodeState.NONE };
                Array.Clear(nodeStates, 0, nodeStates.Length);
            }

            currentNodeIndex = -1;

            UnwindSpecialComposites();

            context.Reset(frameCount, blackBoard);

            int iterationGuard = 0;

            while (context.FrameCount > 0)
            {
                if (iterationGuard > maxIterations)
                {
                    Debug.LogError($"TreeEvaluator exceeded {maxIterations} iterations. Possible infinite loop in behaviour tree. Aborting evaluation.");
                    context.FrameCount = 0;
                    break;
                }

                iterationGuard++;

                INodeHandler handler = NodeHandlerRegistry.GetHandler(nodeDatas[frameStack[context.FrameCount - 1].nodeIndex].nodeType);
                if (handler == null)
                {
                    Debug.LogError($"No handler registered for node type {nodeDatas[frameStack[context.FrameCount - 1].nodeIndex].nodeType}");
                    context.FrameCount--;
                    continue;
                }

                handler.Process(context);

                if (context.ShouldBreak)
                    break;
            }

            frameCount = context.FrameCount;
        }

        private void UnwindSpecialComposites()
        {
            for (int i = frameCount - 1; i >= 0; i--)
            {
                BehaviourNodeType nodeType = nodeDatas[frameStack[i].nodeIndex].nodeType;

                if (nodeType == BehaviourNodeType.PRIORITY)
                {
                    if (frameCount > i + 1)
                        frameCount = i + 1;

                    frameStack[i].childIndex = 0;
                    frameStack[i].lastChildStatus = NodeState.NONE;
                }
            }
        }
    }
}
