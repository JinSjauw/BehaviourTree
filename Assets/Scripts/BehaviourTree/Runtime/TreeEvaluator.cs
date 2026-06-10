using System;
using System.Collections.Generic;
using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Runtime 
{
    /// <summary>
    /// Tick-based behaviour tree evaluator.
    /// Replaces the stack-based approach with fixed-size arrays (activeChildIndex)
    /// and recursive TickNode dispatch. Each call to Evaluate() performs a single
    /// tick from the root, resuming any RUNNING branches via activeChildIndex.
    /// </summary>
    public class TreeEvaluator
    {
        private NodeData[] nodeDatas;
        private FieldData[] fieldDatas;
        private NodeMethod[] methodInstances;
        private int[] activeChildIndex;
        private Dictionary<int, ParallelChildState[]> parallelStates;
        private TickContext tickContext;
        private bool isInitialized = false;

        public int currentNodeIndex { get; private set; } = -1;
        public NodeState[] nodeStates;

        public TreeEvaluator(NodeData[] nodeDatas, FieldData[] fieldDatas, int maxTreeDepth)
        {
            Debug.Log("Created Tree Evaluator");
            this.nodeDatas = nodeDatas;
            this.fieldDatas = fieldDatas;
            nodeStates = new NodeState[nodeDatas.Length];
            activeChildIndex = new int[nodeDatas.Length];
            parallelStates = new Dictionary<int, ParallelChildState[]>();

            // Create class-based method instances for nodes that have methodName set
            methodInstances = new NodeMethod[nodeDatas.Length];
            for (int i = 0; i < nodeDatas.Length; i++)
            {
                string name = nodeDatas[i].methodName;
                if (string.IsNullOrEmpty(name)) continue;

                NodeMethod instance = MethodRegistry.CreateInstance(name);
                if (instance == null) continue;

                FieldBinding[] bindings = MethodRegistry.GetBindings(name);
                ReadOnlySpan<FieldData> fields = GetNodeFieldSlice(nodeDatas[i]);
                instance.DeserializeFields(fields, bindings ?? Array.Empty<FieldBinding>());
                methodInstances[i] = instance;
            }

            if (nodeDatas == null || fieldDatas == null || nodeDatas.Length == 0)
            {
                Debug.LogError("TreeEvaluator: nodeDatas or fieldDatas is NULL");
                return;
            }

            isInitialized = true;
        }

        private ReadOnlySpan<FieldData> GetNodeFieldSlice(NodeData node)
        {
            if (node.fieldDataCount > 0 && fieldDatas != null && node.fieldDataStartIndex >= 0)
                return new ReadOnlySpan<FieldData>(fieldDatas, node.fieldDataStartIndex, node.fieldDataCount);
            return default;
        }

        /// <summary>
        /// Performs a single tick of the behaviour tree from the root.
        /// Call once per frame. Tree state (activeChildIndex, nodeStates)
        /// persists across calls so RUNNING branches resume automatically.
        /// </summary>
        public void Evaluate(BlackBoard blackBoard)
        {
            if (!isInitialized)
            {
                Debug.LogError("TreeEvaluator: not initialized");
                return;
            }

            tickContext.nodeDatas = nodeDatas;
            tickContext.methodInstances = methodInstances;
            tickContext.nodeStates = nodeStates;
            tickContext.activeChildIndex = activeChildIndex;
            tickContext.parallelStates = parallelStates;
            tickContext.blackBoard = blackBoard;

            // Effective root has no children — nothing to evaluate
            if (nodeDatas[0].firstChildIndex < 0)
                return;

            NodeState result = TickDispatcher.TickNode(0, ref tickContext);
            nodeStates[0] = result;
            currentNodeIndex = 0;
        }
    }
}
