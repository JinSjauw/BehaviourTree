using System.Collections.Generic;
using UnityEngine;

public class EvaluatorFrame 
{
    public int nodeIndex;
    public int childIndex;
    public NodeState lastChildStatus;
}

//This class is meant to evaluate the tree and also handle the per node type execution logic.
public class TreeEvaluator
{
    //Root is needs to turn into a list/array the flattened behaviourNodes in the tree.
    private NodeData[] nodeDatas;
    private Stack<EvaluatorFrame> nodeStack;

    public TreeEvaluator(NodeData[] nodeDatas) 
    {
        this.nodeDatas = nodeDatas;
        nodeStack = new Stack<EvaluatorFrame>();
    }

    public void Evaluate(BlackBoard blackBoard)
    {
        if (nodeStack.Count == 0)
            nodeStack.Push(new EvaluatorFrame { nodeIndex = 0, childIndex = 0, lastChildStatus = NodeState.NONE });

        while (nodeStack.Count > 0)
        {
            EvaluatorFrame frame = nodeStack.Peek();
            ref NodeData node = ref nodeDatas[frame.nodeIndex];

            if (node.nodeType == BehaviourNodeType.ACTION || node.nodeType == BehaviourNodeType.CONDITION)
            {
                NodeState status = EvaluateLeaf(blackBoard, ref node);
                if (status == NodeState.RUNNING)
                {
                    break;
                }
                else
                {
                    nodeStack.Pop(); // leaf done
                    if (nodeStack.Count > 0)
                    {
                        // Parent is now top; store the result in its frame
                        EvaluatorFrame parentFrame = nodeStack.Peek();
                        parentFrame.lastChildStatus = status;
                    }
                    // Continue loop so parent can act on the result
                }
            }
            else if (node.nodeType == BehaviourNodeType.SEQUENCE)
            {
                int childCount = node.lastChildIndex - node.firstChildIndex + 1;

                // If we have a lastChildStatus, it means a child just finished
                if (frame.lastChildStatus != NodeState.NONE)
                {
                    if (frame.lastChildStatus == NodeState.FAILURE)
                    {
                        // Sequence fails immediately
                        nodeStack.Pop();
                        if (nodeStack.Count > 0)
                            nodeStack.Peek().lastChildStatus = NodeState.FAILURE;
                        continue;
                    }
                    else if (frame.lastChildStatus == NodeState.SUCCESS)
                    {
                        // Child succeeded, move to next child
                        frame.childIndex++;
                        frame.lastChildStatus = NodeState.NONE; // reset
                                                                   // If we've processed all children, sequence succeeds
                        if (frame.childIndex >= childCount)
                        {
                            nodeStack.Pop();
                            if (nodeStack.Count > 0)
                                nodeStack.Peek().lastChildStatus = NodeState.SUCCESS;
                            continue;
                        }
                    }
                    // else if Running – shouldn't happen because we break earlier
                }

                // If we haven't finished, push the next child
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
                        // Selector succeeds immediately
                        nodeStack.Pop();
                        if (nodeStack.Count > 0)
                            nodeStack.Peek().lastChildStatus = NodeState.SUCCESS;
                        continue;
                    }
                    else if (frame.lastChildStatus == NodeState.FAILURE)
                    {
                        // Child failed, try next
                        frame.childIndex++;
                        frame.lastChildStatus = NodeState.NONE;
                        if (frame.childIndex >= childCount)
                        {
                            // All children failed, selector fails
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

        return method.Invoke(blackBoard, ref nodeData);
    }
}
