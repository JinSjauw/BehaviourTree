using System;
using System.Collections.Generic;
using System.Reflection;
using BehaviourTree.Core;
using UnityEngine;

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

    
}

