using UnityEngine;

namespace BehaviourTree.Core 
{
    public enum BlackBoardType
    {
        SELF = 0,
        SQUAD = 1,
    }

    public abstract class BlackBoard : MonoBehaviour
    {
        /// <summary>Index-based storage for blackboard variables. Index corresponds to position in BlackboardDefinition.</summary>
        protected object[] values;

        /// <summary>Initialize index-based storage from a definition.</summary>
        public void Initialize(BlackboardDefinition definition)
        {
            if (definition == null) return;
            values = new object[definition.sharedVariables.Count];
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
        }
    }
}

