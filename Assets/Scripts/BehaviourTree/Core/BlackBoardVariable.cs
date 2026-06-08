using System;
using UnityEngine;

namespace BehaviourTree.Core
{
    [Serializable]
    public struct BlackboardVariable
    {
        public string name;
        public string typeName;

        /// <summary>
        /// Number of consecutive slots this variable occupies in the flat storage array.
        /// stride = 1 for a single value; stride > 1 for per-element arrays.
        /// User-assignable for any blackboard variable.
        /// </summary>
        public int stride;

        /// <summary>True when this variable represents an array (stride > 1).</summary>
        public bool IsArray => stride > 1;

        // ── Initial values for value types (single) ──
        public bool showInitialValue;
        public int intValue;
        public float floatValue;
        public bool boolValue;
        public Vector2 vector2Value;
        public Vector3 vector3Value;

        // ── Per-element initial values for value-type arrays (stride > 1) ──
        public int[] initialIntValues;
        public float[] initialFloatValues;
        public bool[] initialBoolValues;
        public Vector2[] initialVector2Values;
        public Vector3[] initialVector3Values;

        /// <summary>
        /// Returns true if this variable's type supports an initial value
        /// (i.e. it is a value type, not GameObject / Transform).
        /// </summary>
        public bool SupportsInitialValue()
        {
            Type type = FieldTypeHelper.GetSystemTypeFromName(typeName);
            return type != null && type.IsValueType && !type.IsEnum;
        }

        /// <summary>
        /// Returns the boxed initial value for a specific element index.
        /// For strided variables, uses the per-element array if present.
        /// Falls back to the single default value.
        /// </summary>
        public object GetInitialValue(int elementIndex = 0)
        {
            Type type = FieldTypeHelper.GetSystemTypeFromName(typeName);
            if (type == null)
            {
                if (!string.IsNullOrEmpty(typeName))
                    Debug.LogWarning($"[BlackboardVariable] Unresolved typeName '{typeName}' for variable '{name}'. Returning null.");
                return null;
            }

            if (type == typeof(int))
                return TryGetArrayElement(initialIntValues, elementIndex, intValue);
            if (type == typeof(float))
                return TryGetArrayElement(initialFloatValues, elementIndex, floatValue);
            if (type == typeof(bool))
                return TryGetArrayElement(initialBoolValues, elementIndex, boolValue);
            if (type == typeof(Vector2))
                return TryGetArrayElement(initialVector2Values, elementIndex, vector2Value);
            if (type == typeof(Vector3))
                return TryGetArrayElement(initialVector3Values, elementIndex, vector3Value);

            // GameObject / Transform — leave null
            return null;
        }

        /// <summary>
        /// Resize the appropriate per-element array to match a new stride value.
        /// Preserves existing values and pads with defaults.
        /// </summary>
        public void ResizeInitialArray(int newStride)
        {
            Type type = FieldTypeHelper.GetSystemTypeFromName(typeName);
            if (type == null || type.IsValueType == false) return;

            if (type == typeof(int))
                initialIntValues = ResizeArray(initialIntValues, newStride, intValue);
            else if (type == typeof(float))
                initialFloatValues = ResizeArray(initialFloatValues, newStride, floatValue);
            else if (type == typeof(bool))
                initialBoolValues = ResizeArray(initialBoolValues, newStride, boolValue);
            else if (type == typeof(Vector2))
                initialVector2Values = ResizeArray(initialVector2Values, newStride, vector2Value);
            else if (type == typeof(Vector3))
                initialVector3Values = ResizeArray(initialVector3Values, newStride, vector3Value);
        }

        public T TryGetArrayElement<T>(T[] array, int index, T fallback)
        {
            if (array != null && index >= 0 && index < array.Length)
                return array[index];
            return fallback;
        }

        private static T[] ResizeArray<T>(T[] existing, int newSize, T defaultValue)
        {
            T[] result = new T[newSize];
            if (existing != null)
            {
                int copyCount = Mathf.Min(existing.Length, newSize);
                for (int i = 0; i < copyCount; i++)
                    result[i] = existing[i];
            }
            for (int i = (existing != null ? Mathf.Min(existing.Length, newSize) : 0); i < newSize; i++)
                result[i] = defaultValue;
            return result;
        }
    }
}
