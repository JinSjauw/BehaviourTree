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
    /// Use <see cref="FieldReader"/> to unpack the span.
    /// </summary>
    public delegate NodeState BehaviorMethod(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields);

    public static class MethodRegistry
    {
        private static Dictionary<MethodID, BehaviorMethod> methodRegistry = new Dictionary<MethodID, BehaviorMethod>();
        private static Dictionary<MethodID, DecoratorMethod> decoratorRegistry = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize() 
        {
            Debug.Log("Registering BT methods...");
            methodRegistry.Clear();
            decoratorRegistry.Clear();

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                foreach (var type in assembly.GetTypes())
                {
                    foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                    {
                        BTreeMethodAttribute attr = method.GetCustomAttribute<BTreeMethodAttribute>();
                        if (attr != null)
                        {
                            BehaviorMethod del = (BehaviorMethod)Delegate.CreateDelegate(typeof(BehaviorMethod), method);
                            methodRegistry.Add(attr.methodID, del);
                            Debug.Log("Registered: " + attr.methodID);
                        }

                        BTreeDecoratorMethodAttribute decoratorAttr = method.GetCustomAttribute<BTreeDecoratorMethodAttribute>();
                        if (decoratorAttr != null)
                        {
                            DecoratorMethod del = (DecoratorMethod)Delegate.CreateDelegate(typeof(DecoratorMethod), method);
                            decoratorRegistry.Add(decoratorAttr.methodID, del);
                            Debug.Log("Registered decorator: " + decoratorAttr.methodID);
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

         public static DecoratorMethod GetDecoratorMethod(MethodID methodID)
        {
            if (methodID == MethodID.NONE) return null;

            if (!decoratorRegistry.TryGetValue(methodID, out var method))
            {
#if UNITY_EDITOR
                throw new KeyNotFoundException($"Missing decorator entry ID: {methodID}");
#else
                Debug.LogError($"Missing decorator entry ID: {methodID}");
                return null;
#endif
            }
            return method;
        }
    }

    
}

