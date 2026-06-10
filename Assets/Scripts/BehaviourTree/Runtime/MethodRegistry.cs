using System;
using System.Collections.Generic;
using System.Reflection;
using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Runtime
{
    public static class MethodRegistry
    {
        /// <summary>Fires after the registry finishes scanning assemblies. Subscribe to invalidate caches.</summary>
        public static event Action OnRegistryRebuilt;

        private static readonly Dictionary<string, Type> methodTypeMap = new();
        private static readonly Dictionary<Type, FieldBinding[]> bindingCache = new();

        // Static constructor — runs once on first type access, in editor and runtime
        static MethodRegistry()
        {
            Build();
        }

        /// <summary>Rebuilds the registry. Call when new types may be available (e.g. after script compilation in editor).</summary>
        public static void Rebuild()
        {
            methodTypeMap.Clear();
            bindingCache.Clear();
            Build();
        }

        private static void Build()
        {
            Debug.Log("[MethodRegistry] Registering class-based methods...");

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (Assembly assembly in assemblies)
            {
                Type[] types;
                try { types = assembly.GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = e.Types; }

                foreach (Type type in types)
                {
                    if (type == null || type.IsAbstract) continue;
                    if (!typeof(NodeMethod).IsAssignableFrom(type)) continue;

                    try
                    {
                        NodeMethod temp = (NodeMethod)Activator.CreateInstance(type);
                        string methodName = temp.MethodName;

                        if (methodTypeMap.ContainsKey(methodName))
                        {
                            Debug.LogWarning($"[MethodRegistry] Duplicate method name '{methodName}': {type.Name} conflicts with {methodTypeMap[methodName].Name}. Skipping.");
                            continue;
                        }

                        methodTypeMap[methodName] = type;
                        bindingCache[type] = CreateBindings(type);
                        Debug.Log($"[MethodRegistry] Registered: {methodName} ({type.Name})");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[MethodRegistry] Failed to register '{type.Name}': {ex.Message}");
                    }
                }
            }

            OnRegistryRebuilt?.Invoke();
        }

        // ── Public API ─────────────────────────────────────────────

        public static IReadOnlyCollection<string> GetMethodNames() => methodTypeMap.Keys;

        public static Type GetMethodType(string methodName)
        {
            methodTypeMap.TryGetValue(methodName, out Type type);
            return type;
        }

        public static FieldBinding[] GetBindings(Type methodType)
        {
            bindingCache.TryGetValue(methodType, out FieldBinding[] bindings);
            return bindings;
        }

        public static FieldBinding[] GetBindings(string methodName)
        {
            Type type = GetMethodType(methodName);
            return type != null ? GetBindings(type) : null;
        }

        public static NodeMethod CreateInstance(string methodName)
        {
            if (string.IsNullOrEmpty(methodName)) return null;
            Type type = GetMethodType(methodName);
            if (type == null)
            {
                Debug.LogError($"[MethodRegistry] Unknown method: '{methodName}'");
                return null;
            }
            return (NodeMethod)Activator.CreateInstance(type);
        }

        public static bool IsClassMethod(string methodName) => methodTypeMap.ContainsKey(methodName);

        public static BehaviourNodeType GetCategory(Type methodType)
        {
            if (typeof(ActionMethod).IsAssignableFrom(methodType))    return BehaviourNodeType.ACTION;
            if (typeof(ConditionMethod).IsAssignableFrom(methodType)) return BehaviourNodeType.CONDITION;
            if (typeof(BehaviourTree.Core.DecoratorMethod).IsAssignableFrom(methodType)) return BehaviourNodeType.DECORATOR;
            return BehaviourNodeType.ACTION;
        }

        public static BehaviourNodeType GetCategory(string methodName)
        {
            Type type = GetMethodType(methodName);
            return type != null ? GetCategory(type) : BehaviourNodeType.ACTION;
        }

        // ── Internal ───────────────────────────────────────────────

        internal static FieldBinding[] CreateBindings(Type methodType)
        {
            FieldInfo[] fields = methodType.GetFields(BindingFlags.Public | BindingFlags.Instance);
            List<FieldBinding> list = new List<FieldBinding>(fields.Length);

            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                SharedVarAttribute sharedVar = field.GetCustomAttribute<SharedVarAttribute>();
                bool isSharedVar = sharedVar != null;
                bool isOutput = isSharedVar;
                if (isSharedVar && sharedVar.IsToggleVariable)
                    isOutput = false;

                FieldType ft = FieldTypeHelper.GetFieldType(field.FieldType);

                list.Add(new FieldBinding
                {
                    fieldInfo = field,
                    fieldType = ft,
                    isOutput = isOutput,
                    bbSlotIndex = -1
                });
            }

            return list.ToArray();
        }
    }
}
