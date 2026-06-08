using System;
using BehaviourTree.Core;
using BehaviourTree.Runtime;
using UnityEngine;

namespace BehaviourTree
{
    public partial class StandardMethods
    {
        [BTreeDecoratorMethod(MethodID.INVERTER)]
        public static NodeState Inverter(NodeState childResult, BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            INVERTER_NodeFields p = NodeFieldBindings.DeserializeINVERTER(fields, blackBoard);

            if (p.alwaysFailure) return NodeState.FAILURE;
            if (p.alwaysSuccess) return NodeState.SUCCESS;

            return childResult switch
            {
                NodeState.SUCCESS => NodeState.FAILURE,
                NodeState.FAILURE  => NodeState.SUCCESS,
                _                  => childResult   // RUNNING passes through
            };
        }

        [BTreeDecoratorMethod(MethodID.REPEATER)]
        public static NodeState Repeater(NodeState childResult, BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            if (childResult == NodeState.RUNNING)
                return NodeState.RUNNING;

            REPEATER_NodeFields repeaterParams = NodeFieldBindings.DeserializeREPEATER(fields, blackBoard);

            if (childResult == NodeState.SUCCESS && repeaterParams.currentCount < repeaterParams.targetCount - 1)
            {
                repeaterParams.currentCount++;
                NodeFieldBindings.SerializeREPEATER(repeaterParams, fields, blackBoard);
                return NodeState.RUNNING;   // signals handler to re-push child
            }

            repeaterParams.currentCount = 0;
            NodeFieldBindings.SerializeREPEATER(repeaterParams, fields, blackBoard);
            return childResult;
        }
    }
        
}
