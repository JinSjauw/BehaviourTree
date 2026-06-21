
using System;
using System.Collections.Generic;
using System.Reflection;
using BehaviourTree.Core;
using BehaviourTree.Runtime;

namespace BehaviourTree.Editor
{
    /// <summary>
    /// Describes a method's parameter as exposed in the node inspector.
    /// </summary>
    public class ParamInfo
    {
        public string fieldName;
        public Type fieldType;
        public bool isVariable;
        public bool isArray;
        public bool isToggleVariable;
        public bool isRoleDropdown;
        public int index;

        /// <summary>When true, this field is hidden from the inspector.
        /// The variableName is auto-filled from autoVariableName.
        /// The shared var slot offset is still baked normally.</summary>
        public bool isHidden;

        /// <summary>BB variable name to auto-bind when isHidden is true.</summary>
        public string autoVariableName;

        /// <summary>When true, constant-mode shows an order search dropdown
        /// instead of a raw int field. Name stored in stringValue, baker resolves to index.</summary>
        public bool isOrderDropdown;
    }

#if UNITY_EDITOR

    public static class MethodMetadataCache
    {
        private static Dictionary<string, List<ParamInfo>> cache;

        static MethodMetadataCache()
        {
            MethodRegistry.OnRegistryRebuilt += InvalidateCache;
        }

        /// <summary>Clears the cached metadata so the next lookup rebuilds from the current assembly state.</summary>
        public static void InvalidateCache()
        {
            cache = null;
        }

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

                    // Resolve method name from attribute or type name — no need to instantiate
                    NodeMethodAttribute attr = type.GetCustomAttribute<NodeMethodAttribute>();
                    string name = attr != null ? attr.methodName : type.Name;
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
                bool isRoleDropdown = varAttribute?.IsRoleDropdown ?? false;
                bool isHidden = varAttribute?.IsHidden ?? false;
                string autoVarName = varAttribute?.AutoVariableName;
                bool isOrderDropdown = varAttribute?.IsOrderDropdown ?? false;

                paramList.Add(new ParamInfo
                {
                    fieldName = field.Name,
                    fieldType = field.FieldType,
                    isVariable = isVar,
                    isArray = isArray,
                    isToggleVariable = isToggle,
                    isRoleDropdown = isRoleDropdown,
                    isHidden = isHidden,
                    autoVariableName = autoVarName,
                    isOrderDropdown = isOrderDropdown,
                    index = fieldIndex++
                });
            }
            return paramList;
        }
    }
#endif
}
