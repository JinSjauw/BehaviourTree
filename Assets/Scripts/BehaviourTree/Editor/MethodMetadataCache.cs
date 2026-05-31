
using System;
using System.Collections.Generic;
using System.Reflection;
using BehaviourTree.Core;

namespace BehaviourTree.Editor
{
    /// <summary>
    /// Editor-only cache that scans all *_Params struct types and stores their field metadata.
    /// </summary>
    public class ParamInfo
    {
        public string fieldName;
        public Type fieldType;
        public bool isVariable; // true if [BTreeVar] is present
        public bool isToggleVariable;
        public int index;
    }

#if UNITY_EDITOR

    public static class MethodMetadataCache
    {
        private static Dictionary<MethodID, List<ParamInfo>> _cache;
        private static HashSet<MethodID> _generateDeserializer;

        public static IReadOnlyDictionary<MethodID, List<ParamInfo>> Cache
        {
            get
            {
                BuildIfNeeded();
                return _cache;
            }
        }

        public static List<ParamInfo> GetParamsForMethod(MethodID id)
        {
            BuildIfNeeded();
            _cache.TryGetValue(id, out var list);
            return list;
        }

        public static bool ShouldGenerateBindings(MethodID id)
        {
            BuildIfNeeded();
            return _generateDeserializer != null && _generateDeserializer.Contains(id);
        }

        private static void BuildIfNeeded()
        {
            if (_cache != null) return;
            _cache = new Dictionary<MethodID, List<ParamInfo>>();
            _generateDeserializer = new HashSet<MethodID>();

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var asm in assemblies)
            {
                Type[] types;
                
                try
                {
                    types = asm.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    types = e.Types;
                }

                foreach (var type in types)
                {
                    if (type == null) continue;

                    // Look for partial struct types named *_NodeFields
                    if (!type.IsValueType || !type.Name.EndsWith("_NodeFields")) continue;

                    // Infer MethodID from the name (e.g. HELLOWORLD_NodeFields -> HELLOWORLD)
                    string methodName = type.Name.Replace("_NodeFields", "");
                    if (!Enum.TryParse<MethodID>(methodName, out var methodId)) continue;

                    if (type.GetCustomAttribute<GenerateNodeFieldBindingsAttribute>() != null)
                    {
                        _generateDeserializer.Add(methodId);
                    }

                    var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
                    var paramList = new List<ParamInfo>();
                    int idx = 0;
                    foreach (var field in fields)
                    {
                        SharedVarAttribute varAttribute = field.GetCustomAttribute<SharedVarAttribute>();
                        bool isVar = varAttribute != null;
                        bool isToggle = varAttribute?.IsToggleVariable ?? false;

                        paramList.Add(new ParamInfo
                        {
                            fieldName = field.Name,
                            fieldType = field.FieldType,
                            isVariable = isVar,
                            isToggleVariable = isToggle,
                            index = idx++
                        });
                    }
                    _cache[methodId] = paramList;
                }
            }
        }
    }
#endif
}
