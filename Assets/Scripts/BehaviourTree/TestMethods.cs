using System;
using BehaviourTree.Core;
using BehaviourTree.Runtime;
using UnityEngine;
using UnityEngine.Scripting;

namespace BehaviourTree
{
    public class TestMethods 
    {
        [Preserve]
        [BTreeMethod(MethodID.HELLOWORLD)]
        public static NodeState HelloWorld(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            Debug.Log("HELLO WORLD!");

            var p = ParamsDeserializer.DeserializeHELLOWORLD(fields, blackBoard);
            Debug.Log($"Speed: {p.Velocity} , Health: {p.Health}, TestTime: {p.TestTime}");

            p.Health += 3;
            p.TestTime += 1;

            ParamsDeserializer.SerializeHELLOWORLD(p, fields, blackBoard);

            return NodeState.SUCCESS;
        }

        [BTreeMethod(MethodID.BYEWORLD)]
        public static NodeState ByeWorld(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            var p = ParamsDeserializer.DeserializeBYEWORLD(fields, blackBoard);

            p.timer += Time.deltaTime;

            Debug.Log($"TIMER: {p.timer} TIMEDCOUNTER: {p.testTimedThreshhold} WAITINGTIME{p.waitingTime}");

            NodeState state = NodeState.SUCCESS;

            if(p.timer >= p.waitingTime)
            {
                p.timer = 0;
                p.testTimedThreshhold++;

                Debug.Log($"Added a timer counter!");
            }
            else
            {
                state = NodeState.RUNNING;
            }

            ParamsDeserializer.SerializeBYEWORLD(p, fields, blackBoard);

            return state;
        }

        [BTreeMethod(MethodID.WAITWORLD)]
        public static NodeState WaitWorld(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            var p = ParamsDeserializer.DeserializeWAITWORLD(fields, blackBoard);
            
            Debug.Log($"WAITWORLD TIMEDCOUNTER: {p.testTimedThreshhold}");

            if(p.testTimedThreshhold > 4)
            {
                return NodeState.SUCCESS;
            }

            return NodeState.FAILURE;
        }
    }
}
