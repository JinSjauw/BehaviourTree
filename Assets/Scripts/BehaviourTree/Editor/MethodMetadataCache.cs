
using System;
using System.Collections.Generic;
using System.Reflection;
using BehaviourTree.Core;

namespace BehaviourTree.Editor
{
    /// <summary>
    /// Editor-only cache that scans all NodeMethod subclasses and stores their field metadata.
    /// </summary>
    public class ParamInfo
    {
        public string fieldName;
        public Type fieldType;
        public bool isVariable;
        public bool isArray;
        public bool isToggleVariable;
        public int index;
    }

#if UNITY_EDITOR

    public static class MethodMetadataCache
    {
        private static Dictionary<string, List<ParamInfo>> cache;

        /// <summary>Lookup by method name string.</summary>
        public static List<ParamInfo> GetParamsForMethod(string methodName)
        {
            BuildIfNeeded();
            if (string.IsNullOrEmpty(methodName)) return null;
            cache.TryGetValue(methodName, out var list);
            return list;
        }

        private static void BuildIfNeeded()
        {
            if (cache != null) return;
            cache = new Dictionary<string, List<ParamInfo>>();

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var asm in assemblies)
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = e.Types; }

                foreach (var type in types)
                {
                    if (type == null || type.IsAbstract) continue;
                    if (!typeof(NodeMethod).IsAssignableFrom(type)) continue;

                    NodeMethod temp = null;
                    string name = null;
                    try
                    {
                        temp = (NodeMethod)Activator.CreateInstance(type);
                        name = temp.MethodName;
                    }
                    catch { continue; }

                    if (string.IsNullOrEmpty(name) || cache.ContainsKey(name))
                        continue;

                    cache[name] = BuildParamListFromFields(type);
                }
            }
        }

        private static List<ParamInfo> BuildParamListFromFields(Type type)
        {
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            var paramList = new List<ParamInfo>();
            int fieldIndex = 0;
            foreach (var field in fields)
            {
                SharedVarAttribute varAttribute = field.GetCustomAttribute<SharedVarAttribute>();
                SharedArrayAttribute arrayAttribute = field.GetCustomAttribute<SharedArrayAttribute>();
                bool isVar = varAttribute != null;
                bool isArray = arrayAttribute != null;
                bool isToggle = varAttribute?.IsToggleVariable ?? false;

                paramList.Add(new ParamInfo
                {
                    fieldName = field.Name,
                    fieldType = field.FieldType,
                    isVariable = isVar,
                    isArray = isArray,
                    isToggleVariable = isToggle,
                    index = fieldIndex++
                });
            }
            return paramList;
        }
    }
#endif
}
