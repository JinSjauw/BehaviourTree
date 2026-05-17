using System;
using UnityEngine;

namespace BehaviourTree.Core
{
    [Serializable]
    public struct BlackboardVariable
    {
        public string name;
        public string typeName;

        // ── Initial values for value types ──
        public bool showInitialValue;
        public int intValue;
        public float floatValue;
        public bool boolValue;
        public Vector2 vector2Value;
        public Vector3 vector3Value;

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
        /// Returns the boxed initial value for value types, or null for
        /// reference types (GameObject, Transform).
        /// </summary>
        public object GetInitialValue()
        {
            Type type = FieldTypeHelper.GetSystemTypeFromName(typeName);
            if (type == null) return null;

            if (type == typeof(int))      return intValue;
            if (type == typeof(float))    return floatValue;
            if (type == typeof(bool))     return boolValue;
            if (type == typeof(Vector2))  return vector2Value;
            if (type == typeof(Vector3))  return vector3Value;

            // GameObject / Transform — leave null
            return null;
        }
    }
}
