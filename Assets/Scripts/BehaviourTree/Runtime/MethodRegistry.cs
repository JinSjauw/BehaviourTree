using System;
using System.Collections.Generic;
using System.Reflection;
using BehaviourTree.Core;
using UnityEngine;
using UnityEngine.Scripting;

namespace BehaviourTree.Runtime 
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class BTreeMethodAttribute : Attribute 
    {
        public MethodID methodID;
        public BTreeMethodAttribute(MethodID methodID) => this.methodID = methodID;
    }

    /// <summary>
    /// Delegate for behaviour tree methods.
    /// Receives the blackboard (for writing) and a raw span of FieldData (for reading).
    /// Use <see cref="FieldReader"/> to unpack the span comfortably.
    /// </summary>
    public delegate NodeState BehaviorMethod(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields);

    public static class MethodRegistry
    {
        private static Dictionary<MethodID, BehaviorMethod> methodRegistry = new Dictionary<MethodID, BehaviorMethod>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize() 
        {
            Debug.Log("Registering BT methods...");
            methodRegistry.Clear();

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                foreach (var type in assembly.GetTypes())
                {
                    foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                    {
                        var attr = method.GetCustomAttribute<BTreeMethodAttribute>();
                        if (attr != null)
                        {
                            var del = (BehaviorMethod)Delegate.CreateDelegate(typeof(BehaviorMethod), method);
                            methodRegistry.Add(attr.methodID, del);
                            Debug.Log("Registered: " + attr.methodID);
                        }
                    }
                }
            }
        }

        public static BehaviorMethod GetMethod(MethodID methodID) 
        {
            if (methodID == MethodID.NONE) return null;

            if (!methodRegistry.TryGetValue(methodID, out BehaviorMethod method))
            {
            #if UNITY_EDITOR
                throw new KeyNotFoundException($"Missing entry ID: {methodID}");
            #else
                Debug.LogError($"Missing entry ID: {methodID}");
                return null;
            #endif
            }
            return method;
        } 
    }

    // ─── Example Methods ─────────────────────────────────────────────
    // Field indices for HELLOWORLD_Params: 0=Speed(const), 1=Health(var), 2=TestTime(var)
    public class TestMethods 
    {
        [Preserve]
        [BTreeMethod(MethodID.HELLOWORLD)]
        public static NodeState HelloWorld(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            Debug.Log("HELLO WORLD!");

            // fields[0] = Speed (constant float)
            FieldReader reader = new FieldReader(fields, blackBoard);
            float speed = reader.GetFloat(0);
            Debug.Log("Speed: " + speed);

            // fields[1] = Health (blackboard variable)
            int health = reader.GetInt(1);
            Debug.Log("Health: " + health);
            return NodeState.SUCCESS;
        }

        [BTreeMethod(MethodID.BYEWORLD)]
        public static NodeState ByeWorld(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            Debug.Log("Bye world... :(");

            FieldReader reader = new FieldReader(fields, blackBoard);

            // Read health (index 1 in HELLOWORLD_Params)
            int health = reader.GetInt(1);
            Debug.Log("Taking Damage! Health was: " + health);

            health -= 3;
            blackBoard.Set(1, health);
            Debug.Log("Health now: " + health);
            return NodeState.FAILURE;
        }

        [BTreeMethod(MethodID.WAITWORLD)]
        public static NodeState WaitWorld(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            Debug.Log("Wait world...");

            FieldReader reader = new FieldReader(fields, blackBoard);

            // Read TestTime (index 2 in HELLOWORLD_Params)
            float time = reader.GetFloat(2);
            if (time < 3f)
            {
                    time += Time.deltaTime;
                    Debug.Log("Waiting! " + time);
                blackBoard.Set(2, time);
                    return NodeState.RUNNING;
                }
                else 
                {
                blackBoard.Set(2, 0f);
                Debug.Log("Done waiting!");
                return NodeState.SUCCESS;
                }
            }
        }
    }

