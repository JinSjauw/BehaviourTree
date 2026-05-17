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
            INVERTER_Params p = ParamsDeserializer.DeserializeINVERTER(fields, blackBoard);

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

            REPEATER_Params p = ParamsDeserializer.DeserializeREPEATER(fields, blackBoard);

            if (childResult == NodeState.SUCCESS && p.currentCount < p.targetCount)
            {
                p.currentCount++;
                ParamsDeserializer.SerializeREPEATER(p, fields, blackBoard);
                Debug.Log($"Repeating! {p.currentCount}");
                return NodeState.RUNNING;   // signals handler to re-push child
            }

            p.currentCount = 0;
            ParamsDeserializer.SerializeREPEATER(p, fields, blackBoard);
            return childResult;
        }
    }
        
}
