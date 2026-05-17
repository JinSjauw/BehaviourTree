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

        private BlackboardDefinition definition;
        private IBlackboardStorage storage;

        public BlackboardDefinition Definition => definition;
        public IBlackboardStorage Storage => storage;

        /// <summary>Initialize index-based storage from a definition.</summary>
        public void Initialize(BlackboardDefinition blackboardDefinition)
        {
            if(definition == null || definition != blackboardDefinition)
            {
                definition = blackboardDefinition;   
            }

            if (definition == null) return;

            int count = definition.sharedVariables.Count;
            storage ??= new ManagedBlackboardStorage();
            storage.Initialize(definition);

            while (serializedReferences.Count < count)
            {
                serializedReferences.Add(null);
            }

            for (int i = 0; i < count; i++)
            {
                if (storage.GetSlotKind(i) == BlackboardSlotKind.Reference && serializedReferences[i] != null)
                {
                    storage.SetBoxed(i, serializedReferences[i]);
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
            storage = null;
            serializedReferences.Clear();
        }

        /// <summary>Get a value by index in the blackboard array.</summary>
        public T Get<T>(int index)
        {
            if (storage == null)
            {
#if UNITY_EDITOR
                Debug.LogWarning($"[Blackboard] Storage is NULL, returning default");
#endif
                return default;
            }
            return storage.Get<T>(index);
        }

        /// <summary>Set a value by index in the blackboard array.</summary>
        public void Set<T>(int index, T value)
        {
            if (storage == null)
            {
#if UNITY_EDITOR
                Debug.LogWarning($"[Blackboard] Storage is NULL");
#endif
                return;
            }

            storage.Set(index, value);

            // Keep serialized reference in sync for reference types
            if (definition != null && index >= 0 && index < definition.sharedVariables.Count && index < serializedReferences.Count)
            {
                if (storage.GetSlotKind(index) == BlackboardSlotKind.Reference)
                {
                    if (value == null)
                    {
                        serializedReferences[index] = null;
                    }
                    else if (value is UnityEngine.Object unityObject)
                    {
                        serializedReferences[index] = unityObject;
                    }
                }
            }
        }
    }
}
