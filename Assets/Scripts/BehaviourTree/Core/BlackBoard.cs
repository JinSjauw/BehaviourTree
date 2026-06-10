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

    public class BlackBoard : MonoBehaviour, IBlackBoardAccess
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

            int count = GetTotalSlotCount(definition);
            if (storage == null) storage = new ManagedBlackboardStorage();
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

            // Match total slot count (accounting for stride)
            int count = GetTotalSlotCount(definition);
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

        private static int GetTotalSlotCount(BlackboardDefinition definition)
        {
            if (definition == null || definition.sharedVariables == null)
                return 0;

            int total = 0;
            for (int i = 0; i < definition.sharedVariables.Count; i++)
            {
                int stride = definition.sharedVariables[i].stride;
                total += (stride > 1) ? stride : 1;
            }
            return total;
        }

        public int FindVariableIndex(string keyName)
        {
            if (definition == null) 
            {
#if UNITY_EDITOR
                Debug.LogWarning($"[Blackboard] Definition is NULL");
#endif
                return -1; 
            }

            for (int i = 0; i < definition.sharedVariables.Count; i++)
            {
                if (definition.sharedVariables[i].name == keyName)
                {
                    return i;
                }
            }
            return -1;
        }

        public void Set<T>(string keyName, T value)
        {
            int index = FindVariableIndex(keyName);
            Debug.Log($"[Blackboard] Setting {keyName} to {value} at {index}");
            if (index >= 0)
            {   
                Set(index, value);
            }
        }

        public T Get<T>(string keyName)
        {
            int index = FindVariableIndex(keyName);
            if (index >= 0)
            {
                return Get<T>(index);
            }
            return default;
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
            if (definition != null && index >= 0 && index < serializedReferences.Count)
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

        /// <summary>Get a boxed value by slot index. Used by the bridge for type-agnostic copying.</summary>
        public object GetBoxed(int index)
        {
            if (storage == null)
                return null;
            return storage.GetBoxed(index);
        }

        /// <summary>Set a boxed value by slot index. Used by the bridge for type-agnostic copying.</summary>
        public void SetBoxed(int index, object value)
        {
            if (storage == null)
                return;
            storage.SetBoxed(index, value);

            if (definition != null && index >= 0 && index < serializedReferences.Count)
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

        // ── IBlackBoardAccess explicit methods ──────────────────────────

        int IBlackBoardAccess.GetInt(int slot) => Get<int>(slot);
        void IBlackBoardAccess.SetInt(int slot, int value) => Set(slot, value);

        float IBlackBoardAccess.GetFloat(int slot) => Get<float>(slot);
        void IBlackBoardAccess.SetFloat(int slot, float value) => Set(slot, value);

        bool IBlackBoardAccess.GetBool(int slot) => Get<bool>(slot);
        void IBlackBoardAccess.SetBool(int slot, bool value) => Set(slot, value);

        Vector2 IBlackBoardAccess.GetVector2(int slot) => Get<Vector2>(slot);
        void IBlackBoardAccess.SetVector2(int slot, Vector2 value) => Set(slot, value);

        Vector3 IBlackBoardAccess.GetVector3(int slot) => Get<Vector3>(slot);
        void IBlackBoardAccess.SetVector3(int slot, Vector3 value) => Set(slot, value);

        GameObject IBlackBoardAccess.GetGameObject(int slot) => Get<GameObject>(slot);
        void IBlackBoardAccess.SetGameObject(int slot, GameObject value) => Set(slot, value);

        Transform IBlackBoardAccess.GetTransform(int slot) => Get<Transform>(slot);
        void IBlackBoardAccess.SetTransform(int slot, Transform value) => Set(slot, value);
    }
}
