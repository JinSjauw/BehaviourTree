using System;
using UnityEngine;

namespace BehaviourTree.Core
{
    /// <summary>
    /// JSON-based serialization for BlackboardVariableBase instances of arbitrary types.
    /// Used for custom types that can't be stored in the typed variable lists.
    /// </summary>
    public static class BlackboardVariableJsonUtility
    {
        /// <summary>
        /// Serializes a BlackboardVariableBase to a portable JSON representation.
        /// For typed BlackboardVariable&lt;T&gt;, serializes the value directly.
        /// </summary>
        public static string Serialize(BlackboardVariableBase variable)
        {
            SerializedVariableData data = new SerializedVariableData
            {
                name = variable.Name,
                stride = variable.Stride,
                typeName = variable.TypeName
            };

            Type valueType = variable.GetValueType();
            if (valueType == null)
            {
                data.valueJson = "null";
                return JsonUtility.ToJson(data);
            }

            if (variable.Stride > 1)
            {
                data.valueJson = SerializeArray(variable, valueType);
            }
            else
            {
                object value = variable.GetBoxedValue(0);
                data.valueJson = JsonUtility.ToJson(new ValueWrapper { value = JsonUtility.ToJson(value) });
            }

            return JsonUtility.ToJson(data);
        }

        /// <summary>
        /// Deserializes a JSON string back into a BlackboardVariableBase.
        /// Returns a BlackboardVariable&lt;object&gt; if the type cannot be resolved.
        /// </summary>
        public static BlackboardVariableBase Deserialize(string json)
        {
            if (string.IsNullOrEmpty(json))
                return null;

            SerializedVariableData data;
            try
            {
                data = JsonUtility.FromJson<SerializedVariableData>(json);
            }
            catch
            {
                Debug.LogError($"[BlackboardVariableJson] Failed to parse JSON: {json}");
                return null;
            }

            Type valueType = Type.GetType(data.typeName);
            if (valueType == null)
            {
                Debug.LogWarning($"[BlackboardVariableJson] Unresolved type '{data.typeName}' for variable '{data.name}'. Returning null.");
                return null;
            }

            // Use reflection to create a BlackboardVariable<T> of the correct type
            Type genericType = typeof(BlackboardVariable<>).MakeGenericType(valueType);
            BlackboardVariableBase variable = (BlackboardVariableBase)Activator.CreateInstance(genericType);
            variable.Name = data.name;
            variable.Stride = data.stride;

            if (variable.Stride > 1)
            {
                DeserializeArray(variable, valueType, data.valueJson);
            }
            else
            {
                ValueWrapper wrapper;
                try
                {
                    wrapper = JsonUtility.FromJson<ValueWrapper>(data.valueJson);
                }
                catch
                {
                    wrapper = new ValueWrapper { value = data.valueJson };
                }
                if (!string.IsNullOrEmpty(wrapper.value) && wrapper.value != "null")
                {
                    object deserialized = JsonUtility.FromJson(wrapper.value, valueType);
                    variable.SetBoxedValue(deserialized, 0);
                }
            }

            return variable;
        }

        /// <summary>
        /// Creates a BlackboardVariable&lt;T&gt; from a typed value.
        /// </summary>
        public static BlackboardVariable<T> Create<T>(string name, int stride, T initialValue)
        {
            return new BlackboardVariable<T>(name, stride, initialValue);
        }

        private static string SerializeArray(BlackboardVariableBase variable, Type elementType)
        {
            ArrayWrapper wrapper = new ArrayWrapper();
            int stride = variable.Stride;
            wrapper.elements = new string[stride];
            for (int i = 0; i < stride; i++)
            {
                object element = variable.GetBoxedValue(i);
                wrapper.elements[i] = element != null ? JsonUtility.ToJson(element) : "null";
            }
            return JsonUtility.ToJson(wrapper);
        }

        private static void DeserializeArray(BlackboardVariableBase variable, Type elementType, string arrayJson)
        {
            if (string.IsNullOrEmpty(arrayJson))
                return;

            ArrayWrapper wrapper;
            try
            {
                wrapper = JsonUtility.FromJson<ArrayWrapper>(arrayJson);
            }
            catch
            {
                return;
            }

            if (wrapper.elements == null)
                return;

            for (int i = 0; i < wrapper.elements.Length && i < variable.Stride; i++)
            {
                if (!string.IsNullOrEmpty(wrapper.elements[i]) && wrapper.elements[i] != "null")
                {
                    object element = JsonUtility.FromJson(wrapper.elements[i], elementType);
                    variable.SetBoxedValue(element, i);
                }
            }
        }

        [Serializable]
        private struct SerializedVariableData
        {
            public string name;
            public int stride;
            public string typeName;
            public string valueJson;
        }

        [Serializable]
        private struct ValueWrapper
        {
            public string value;
        }

        [Serializable]
        private struct ArrayWrapper
        {
            public string[] elements;
        }
    }
}
