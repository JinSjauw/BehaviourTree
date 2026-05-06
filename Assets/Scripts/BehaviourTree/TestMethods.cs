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
            Debug.Log($"Speed: {p.Speed}, Health: {p.Health}, TestTime: {p.TestTime}");

            p.Health -= 3;
            p.TestTime += Time.deltaTime;

            ParamsDeserializer.SerializeHELLOWORLD(p, fields, blackBoard);

            return NodeState.SUCCESS;
        }

        [BTreeMethod(MethodID.BYEWORLD)]
        public static NodeState ByeWorld(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            
            return NodeState.FAILURE;
        }

        [BTreeMethod(MethodID.WAITWORLD)]
        public static NodeState WaitWorld(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            return NodeState.SUCCESS;
        }
    }
}