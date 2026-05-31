using System.Collections.Generic;
using BehaviourTree.Core;

namespace BehaviourTree.Runtime
{
    public struct ParallelChildState
    {
        public int nodeIndex;
        public NodeState result;
    }

    public class ParallelHandler : INodeHandler
    {
        public bool Process(EvaluatorContext context)
        {
            EvaluatorFrame frame = context.CurrentFrame;
            ref NodeData node = ref context.CurrentNode;
            int childCount = node.lastChildIndex - node.firstChildIndex + 1;

            Dictionary<int, ParallelChildState[]> states = context.ParallelStates;

            if (!states.TryGetValue(frame.nodeIndex, out var children))
            {
                children = new ParallelChildState[childCount];
                for (int i = 0; i < childCount; i++)
                    children[i] = new ParallelChildState { nodeIndex = node.firstChildIndex + i, result = NodeState.NONE };
                states[frame.nodeIndex] = children;
            }

            context.MarkCurrentNodeRunning();
            context.CurrentNodeIndex = frame.nodeIndex;

            bool anyRunning = false;
            bool anyFailure = false;

            for (int i = 0; i < childCount; i++)
            {
                ref ParallelChildState child = ref children[i];
                if (child.result == NodeState.SUCCESS || child.result == NodeState.FAILURE)
                    continue;

                ref NodeData childData = ref context.GetNodeData(child.nodeIndex);

                if (childData.nodeType == BehaviourNodeType.ACTION || childData.nodeType == BehaviourNodeType.CONDITION)
                {
                    child.result = context.EvaluateLeaf(ref childData);
                }
            }

            for (int i = 0; i < childCount; i++)
            {
                ParallelChildState child = children[i];
                context.UpdateNodeStatus(child.result, child.nodeIndex);

                if (child.result == NodeState.RUNNING)
                    anyRunning = true;
                if (child.result == NodeState.FAILURE)
                    anyFailure = true;
            }

            if (anyFailure)
            {
                states.Remove(frame.nodeIndex);
                context.PopAndNotifyParent(NodeState.FAILURE);
                return false;
            }

            if (anyRunning)
            {
                context.ShouldBreak = true;
                context.SetStackRunning();
                return false;
            }

            states.Remove(frame.nodeIndex);
            context.PopAndNotifyParent(NodeState.SUCCESS);
            return false;
        }
    }
}
