using System;
using UnityEngine;

namespace BehaviourTree.Core
{
    /// <summary>
    /// Per-component override for a single value-type slot element.
    /// Stores the boxed value using typed serialized fields so Unity can persist it.
    /// Keyed by variableName + elementIndex — name-based, not slot-index-based,
    /// so overrides naturally follow their variable across definition reorders.
    /// </summary>
    [Serializable]
    public class BlackboardValueOverride
    {
        public string variableName;
        public int elementIndex;

        /// <summary>Assembly-qualified type name for deserialization.</summary>
        public string typeName;

        // One of these fields is populated based on the actual type.
        public int intValue;
        public float floatValue;
        public bool boolValue;
        public Vector2 vector2Value;
        public Vector3 vector3Value;
        public Vector4 vector4Value;
        public Color colorValue;
        public int enumUnderlyingValue;

        /// <summary>Extracts the boxed value from the appropriate typed field.</summary>
        public object GetBoxedValue()
        {
            Type type = string.IsNullOrEmpty(typeName) ? null : Type.GetType(typeName);
            if (type == null) return null;
            if (type == typeof(int)) return intValue;
            if (type == typeof(float)) return floatValue;
            if (type == typeof(bool)) return boolValue;
            if (type == typeof(Vector2)) return vector2Value;
            if (type == typeof(Vector3)) return vector3Value;
            if (type == typeof(Vector4)) return vector4Value;
            if (type == typeof(Color)) return colorValue;
            if (type.IsEnum) return Enum.ToObject(type, enumUnderlyingValue);
            return null;
        }

        /// <summary>Writes a boxed value into the appropriate typed field.</summary>
        public void SetBoxedValue(object value)
        {
            if (value == null) return;
            Type type = value.GetType();
            typeName = type.AssemblyQualifiedName;
            if (value is int intVal) intValue = intVal;
            else if (value is float floatVal) floatValue = floatVal;
            else if (value is bool boolVal) boolValue = boolVal;
            else if (value is Vector2 v2) vector2Value = v2;
            else if (value is Vector3 v3) vector3Value = v3;
            else if (value is Vector4 v4) vector4Value = v4;
            else if (value is Color colorVal) colorValue = colorVal;
            else if (value is Enum enumVal) enumUnderlyingValue = Convert.ToInt32(enumVal);
        }

        /// <summary>Stable key for HashSet lookups.</summary>
        public string GetKey() => $"{variableName}|{elementIndex}";
    }
}
