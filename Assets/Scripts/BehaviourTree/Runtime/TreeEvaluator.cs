using System;
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
        private string[] fieldTypeNames;
        private object[] boxedConstants;
        private NodeMethod[] methodInstances;
        private int[] activeChildIndex;
        private int[] runningAgentIndex;
        private bool[] lastConditionResult;
        private TickContext tickContext;
        private bool isInitialized = false;

        public int currentNodeIndex { get; private set; } = -1;
        public NodeState[] nodeStates;

        /// <summary>
        /// Current agent count for commander trees. Set by CommanderTreeRunner
        /// before Evaluate() so composites read ctx.agentCount without BB variables.
        /// Default 0 for non-commander trees.
        /// </summary>
        public int agentCount;

        public TreeEvaluator(NodeData[] nodeDatas, FieldData[] fieldDatas, string[] fieldTypeNames, object[] boxedConstants, int maxTreeDepth)
        {
            if (nodeDatas == null || fieldDatas == null || nodeDatas.Length == 0)
            {
                Debug.LogError("TreeEvaluator: nodeDatas or fieldDatas is NULL/empty");
                return;
            }

            this.nodeDatas = nodeDatas;
            this.fieldDatas = fieldDatas;
            this.fieldTypeNames = fieldTypeNames;
            this.boxedConstants = boxedConstants;
            nodeStates = new NodeState[nodeDatas.Length];
            activeChildIndex = new int[nodeDatas.Length];
            runningAgentIndex = new int[nodeDatas.Length];
            lastConditionResult = new bool[nodeDatas.Length];

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
                if (bindings != null && bindings.Length > 0)
                {
                    instance.DeserializeFields(fields, bindings, boxedConstants);
                }
                else if (instance.ParameterCount > 0)
                {
                    // Slice fieldTypeNames the same way as fields
                    string[] nodeTypeNames = null;
                    int start = nodeDatas[i].fieldDataStartIndex;
                    int count = nodeDatas[i].fieldDataCount;
                    if (fieldTypeNames != null && start >= 0 && start + count <= fieldTypeNames.Length)
                    {
                        nodeTypeNames = new string[count];
                        Array.Copy(fieldTypeNames, start, nodeTypeNames, 0, count);
                    }
                    instance.DeserializeParameters(fields, nodeTypeNames, boxedConstants);
                }
                methodInstances[i] = instance;
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
            tickContext.runningAgentIndex = runningAgentIndex;
            tickContext.agentCount = agentCount;
            tickContext.lastConditionResult = lastConditionResult;
            tickContext.blackBoard = blackBoard;

            // Effective root has no children — nothing to evaluate
            if (nodeDatas[0].firstChildIndex < 0) return;

            TickDispatcher.TickNode(0, ref tickContext);
            currentNodeIndex = 0;
        }
    }
}
