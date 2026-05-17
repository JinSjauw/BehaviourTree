using System;
using BehaviourTree.Core;
using BehaviourTree.Runtime;
using Unity.VisualScripting;
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

            var p = NodeFieldBindings.DeserializeHELLOWORLD(fields, blackBoard);
            Debug.Log($"Speed: {p.Velocity} , Health: {p.Health}, TestTime: {p.TestTime} Position: {blackBoard.Get<Vector2>((int)BehaviourTreeAsset_BB_Keys.POSITION)}");

            p.Health += 3;
            //p.TestTime += 1;

            NodeFieldBindings.SerializeHELLOWORLD(p, fields, blackBoard);

            return NodeState.SUCCESS;
        }

        [BTreeMethod(MethodID.BYEWORLD)]
        public static NodeState ByeWorld(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            var p = NodeFieldBindings.DeserializeBYEWORLD(fields, blackBoard);

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

            NodeFieldBindings.SerializeBYEWORLD(p, fields, blackBoard);

            return state;
        }

        [BTreeMethod(MethodID.WAITWORLD)]
        public static NodeState WaitWorld(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            var p = NodeFieldBindings.DeserializeWAITWORLD(fields, blackBoard);
            
            Debug.Log($"WAITWORLD TIMEDCOUNTER: {p.testTimedThreshhold}");

            if(p.testTimedThreshhold > 4)
            {
                return NodeState.SUCCESS;
            }

            return NodeState.FAILURE;
        }
    }
}
