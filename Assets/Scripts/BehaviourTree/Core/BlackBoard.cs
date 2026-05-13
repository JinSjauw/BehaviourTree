using System;
using System.Collections.Generic;
using UnityEngine;

namespace BehaviourTree.Core 
{
    public enum BlackBoardType
    {
        SELF = 0,
        SQUAD = 1,
    }

    public class BlackBoard : MonoBehaviour
    {        
        /// <summary>Serialized reference-values exclusivly for GameObject / Transform slots.
        /// These are kept in sync with the runtime values[] array.
        /// Index maps 1:1 to the definition's sharedVariables list.
        /// Value types are stored as null </summary>
        [SerializeField] private List<UnityEngine.Object> serializedReferences = new();

        // ── Runtime ────────────────────────────────────────
        /// <summary>Index-based storage for blackboard variables. Index corresponds to position in BlackboardDefinition.</summary>
        private BlackboardDefinition definition;
        private object[] values;

        public BlackboardDefinition Definition => definition;

        /// <summary>Initialize index-based storage from a definition.</summary>
        public void Initialize(BlackboardDefinition blackboardDefinition)
        {
            if(definition == null || definition != blackboardDefinition)
            {
                definition = blackboardDefinition;   
            }

            if (definition == null) return;

            int count = definition.sharedVariables.Count;
            values = new object[count];

            while (serializedReferences.Count < count)
            {
                serializedReferences.Add(null);
            }

            for (int i = 0; i < count; i++)
            {
                values[i] = definition.sharedVariables[i].GetInitialValue();
                
                Type type = FieldTypeHelper.GetSystemTypeFromName(definition.sharedVariables[i].typeName);
                if (type != null && !type.IsValueType && serializedReferences[i] != null)
                {
                    values[i] = serializedReferences[i];
                }
            }
        }

        public void BuildSerializedReferences(BlackboardDefinition blackboardDefinition)
        {
            definition = blackboardDefinition;

            if (definition == null) return;            

            // Ensure serializedReferences list matches definition length
            int count = definition.sharedVariables.Count;
            while (serializedReferences.Count < count)
            {
                serializedReferences.Add(null);
            }
            while (serializedReferences.Count > count)
            {
                serializedReferences.RemoveAt(serializedReferences.Count - 1);
            }
        }

        public void ClearSerializedReferences()
        {
            definition = null;
            serializedReferences.Clear();
        }

        /// <summary>Get a value by index in the blackboard array.</summary>
        public T Get<T>(int index)
        {
            if (values == null || index < 0 || index >= values.Length)
            {
                Debug.LogWarning($"[Blackboard] Invalid index or values[] is NULL, returning default");
                return default;
            }

            object val = values[index];
            if (val is T tVal)
            {
                return tVal;
            }
            else if(val != null)
            {
                Debug.LogWarning(
                    $"[Blackboard] Type mismatch at index {index} — " +
                    $"Expected: {typeof(T).Name}, Retrieved: {val.GetType().Name}. " +
                    $"Returning default.");
                return default;
            }

            return default;
        }

        /// <summary>Set a value by index in the blackboard array.</summary>
        public void Set<T>(int index, T value)
        {
            if (values == null || index < 0 || index >= values.Length)
            {
                Debug.LogWarning($"[Blackboard] Invalid index or values[] is NULL");
                return;
            }

            object existingObject = values[index];

            if(existingObject != null && existingObject is not T)
            {
                Debug.LogWarning(
                    $"[Blackboard] Type mismatch at index: {index}" +
                    $"Stored = {existingObject.GetType().Name}, Trying to write type: {typeof(T).Name}" +
                    $"Cancelling write"
                );

                return;
            }

            values[index] = value;

            // Keep serialized reference in sync for reference types
            if (definition != null && index < definition.sharedVariables.Count)
            {
                Type type = FieldTypeHelper.GetSystemTypeFromName(definition.sharedVariables[index].typeName);
                if (type != null && !type.IsValueType && value is UnityEngine.Object unityObject)
                {
                    if (index < serializedReferences.Count)
                    {
                        serializedReferences[index] = unityObject;
                    }
                }
            }
        }
    }
}