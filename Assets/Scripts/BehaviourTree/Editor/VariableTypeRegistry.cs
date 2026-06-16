using System;
using System.Collections.Generic;
using BehaviourTree.Core;

namespace BehaviourTree.Editor
{
    /// <summary>
    /// Central registry of types available for blackboard variable creation.
    /// Auto-seeds from FieldTypeHelper.CommonTypes on first access.
    /// External code calls RegisterType to add custom/enum types.
    /// </summary>
    public static class VariableTypeRegistry
    {
        private static readonly List<Type> registeredTypes = new();

        static VariableTypeRegistry()
        {
            foreach (Type t in FieldTypeHelper.CommonTypes)
                registeredTypes.Add(t);
        }

        /// <summary>All registered types (built-in + custom).</summary>
        public static IReadOnlyList<Type> Types => registeredTypes;

        /// <summary>
        /// Register a type for variable creation. Safe to call before or after
        /// registry initialization. Duplicates are silently ignored.
        /// </summary>
        public static void RegisterType(Type type)
        {
            if (type == null || registeredTypes.Contains(type)) return;
            registeredTypes.Add(type);
        }
    }
}
