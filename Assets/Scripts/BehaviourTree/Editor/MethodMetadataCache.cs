
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
        public int index;
    }

    #if UNITY_EDITOR

    public static class MethodMetadataCache
    {
        private static Dictionary<MethodID, List<ParamInfo>> _cache;

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

        private static void BuildIfNeeded()
        {
            if (_cache != null) return;
            _cache = new Dictionary<MethodID, List<ParamInfo>>();

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var asm in assemblies)
            {
                foreach (var type in asm.GetTypes())
                {
                    // Look for partial struct types named *_Params
                    if (!type.IsValueType || !type.Name.EndsWith("_Params")) continue;

                    // Infer MethodID from the name (e.g. HELLOWORLD_Params -> HELLOWORLD)
                    string methodName = type.Name.Replace("_Params", "");
                    if (!Enum.TryParse<MethodID>(methodName, out var methodId)) continue;

                    var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
                    var paramList = new List<ParamInfo>();
                    int idx = 0;
                    foreach (var field in fields)
                    {
                        bool isVar = field.GetCustomAttribute<BTreeVarAttribute>() != null;
                        paramList.Add(new ParamInfo
                        {
                            fieldName = field.Name,
                            fieldType = field.FieldType,
                            isVariable = isVar,
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