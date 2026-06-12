using System;
using UnityEngine;

namespace BehaviourTree.Core
{
    
/// <summary>
/// Static helpers for type resolution, display names, and common type queries.
/// </summary>
public static class FieldTypeHelper
    {
        /// <summary>Common types available by default in the blackboard type picker.</summary>
        public static readonly Type[] CommonTypes = new Type[]
        {
            typeof(int),
            typeof(float),
            typeof(bool),
            typeof(string),
            typeof(Vector2),
            typeof(Vector3),
            typeof(Vector4),
            typeof(Color),
            typeof(Quaternion),
            typeof(GameObject),
            typeof(Transform),
            typeof(Material),
        };

        /// <summary>Human-readable display name for a type.</summary>
        public static string GetDisplayName(Type type, int stride = 1)
        {
            if (type == null) return "Unknown";

            string baseName = type.Name;
            // Use friendly names for common Unity types
            if (type == typeof(int)) baseName = "Integer";
            else if (type == typeof(float)) baseName = "Float";
            else if (type == typeof(bool)) baseName = "Bool";
            else if (type == typeof(string)) baseName = "String";
            else if (type == typeof(GameObject)) baseName = "GameObject";
            else if (type == typeof(Transform)) baseName = "Transform";

            return stride > 1 ? $"{baseName}[{stride}]" : baseName;
        }

        /// <summary>Returns true if the type is one of the common built-in types.</summary>
        public static bool IsCommonType(Type type)
        {
            if (type == null) return false;
            for (int i = 0; i < CommonTypes.Length; i++)
            {
                if (CommonTypes[i] == type)
                    return true;
            }
            return false;
        }

        /// <summary>Returns true if the type can be stored as an inline constant in FieldData (int, float, bool, enum).</summary>
        public static bool CanPackInline(Type type)
        {
            if (type == null) return false;
            return type == typeof(int) || type == typeof(uint)
                || type == typeof(float)
                || type == typeof(bool)
                || type.IsEnum;
        }

        /// <summary>Returns true if the type is a UnityEngine.Object subclass (needs serializedReferences list).</summary>
        public static bool IsUnityObjectType(Type type)
        {
            return type != null && typeof(UnityEngine.Object).IsAssignableFrom(type);
        }

        /// <summary>
        /// Convert a serialised type name (from a BlackboardVariable) back to a System.Type.
        /// </summary>
        public static bool TryGetSystemTypeFromName(string typeName, out Type type)
        {
            type = null;
            if (string.IsNullOrEmpty(typeName)) return false;

            Type resolvedType = Type.GetType(typeName);
            if (resolvedType != null)
            {
                type = resolvedType;
                return true;
            }
            return false;
        }

        public static Type GetSystemTypeFromName(string typeName)
        {
            return TryGetSystemTypeFromName(typeName, out Type type) ? type : null;
        }
    }
}
