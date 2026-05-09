using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BehaviourTree.Core
{
    
/// <summary>
/// Static helpers for FieldType => System.Type resolution
/// </summary>
public static class FieldTypeHelper
    {
        /// <summary>Every supported FieldType.</summary>
        public static readonly IReadOnlyList<FieldType> AllFieldTypes =
            Enum.GetValues(typeof(FieldType)).Cast<FieldType>().ToList();

        public static Type GetSystemType(FieldType ft) => ft switch
        {
            FieldType.Int => typeof(int),
            FieldType.Float => typeof(float),
            FieldType.Bool => typeof(bool),
            FieldType.Vector2 => typeof(Vector2),
            FieldType.Vector3 => typeof(Vector3),
            FieldType.GameObject => typeof(GameObject),
            FieldType.Transform => typeof(Transform),
            _ => typeof(int),
        };


        public static string GetTypeName(FieldType ft) => GetSystemType(ft).AssemblyQualifiedName;

        /// <summary>Human-readable name shown in dropdowns.</summary>
        public static string GetDisplayName(FieldType ft) => ft switch
        {
            FieldType.Int => "Integer",
            FieldType.Float => "Float",
            FieldType.Bool => "Bool",
            FieldType.Vector2 => "Vector2",
            FieldType.Vector3 => "Vector3",
            FieldType.GameObject => "GameObject",
            FieldType.Transform => "Transform",
            _ => "Unknown",
        };

        /// <summary>Reverse-lookup: System.Type → FieldType</summary>
        public static FieldType GetFieldType(Type type)
        {
            if (type == typeof(int) || type == typeof(uint)) return FieldType.Int;
            if (type == typeof(float)) return FieldType.Float;
            if (type == typeof(bool)) return FieldType.Bool;
            if (type == typeof(Vector2)) return FieldType.Vector2;
            if (type == typeof(Vector3)) return FieldType.Vector3;
            if (type == typeof(GameObject)) return FieldType.GameObject;
            if (type == typeof(Transform)) return FieldType.Transform;
            return FieldType.Int;
        }

        /// <summary>
        /// Convert a serialised type name (from a BlackboardVariable) back to a System.Type.
        /// </summary>
        public static Type GetSystemTypeFromName(string typeName)
        {
            // Try assembly-qualified first, fall back to known types
            Type t = Type.GetType(typeName);
            if (t != null) return t;

            foreach (FieldType ft in AllFieldTypes)
            {
                Type candidate = GetSystemType(ft);
                if (candidate.FullName == typeName || candidate.AssemblyQualifiedName == typeName)
                    return candidate;
            }
            return typeof(int);
        }
    }
}