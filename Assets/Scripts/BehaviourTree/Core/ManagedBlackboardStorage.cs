using System;
using UnityEngine;

namespace BehaviourTree.Core
{
    public sealed class ManagedBlackboardStorage : IBlackboardStorage
    {
        private BlackboardDefinition definition;
        private object[] values;
        private Type[] slotTypes;
        private BlackboardSlotKind[] slotKinds;

        public BlackboardDefinition Definition => definition;
        public int Count => values?.Length ?? 0;

        public void Initialize(BlackboardDefinition definition)
        {
            this.definition = definition;
            if (definition == null)
            {
                values = null;
                slotTypes = null;
                slotKinds = null;
                return;
            }

            int count = definition.sharedVariables.Count;
            values = new object[count];
            slotTypes = new Type[count];
            slotKinds = new BlackboardSlotKind[count];

            for (int i = 0; i < count; i++)
            {
                BlackboardVariable variable = definition.sharedVariables[i];
                Type t = FieldTypeHelper.GetSystemTypeFromName(variable.typeName);
                slotTypes[i] = t;
                slotKinds[i] = (t != null && !t.IsValueType) ? BlackboardSlotKind.Reference : BlackboardSlotKind.Value;
                values[i] = variable.GetInitialValue();
            }
        }

        public BlackboardSlotKind GetSlotKind(int index)
        {
            if (slotKinds == null || index < 0 || index >= slotKinds.Length) return BlackboardSlotKind.Value;
            return slotKinds[index];
        }

        public T Get<T>(int index)
        {
            if (values == null || index < 0 || index >= values.Length)
            {
#if UNITY_EDITOR
                Debug.LogWarning($"[Blackboard] Invalid index or values[] is NULL, returning default");
#endif
                return default;
            }

            object val = values[index];
            if (val is T tVal)
            {
                return tVal;
            }

            if (val != null)
            {
#if UNITY_EDITOR
                Debug.LogWarning(
                    $"[Blackboard] Type mismatch at index {index} — " +
                    $"Expected: {typeof(T).Name}, Retrieved: {val.GetType().Name}. " +
                    $"Returning default.");
#endif
            }

            return default;
        }

        public void Set<T>(int index, T value)
        {
            if (values == null || index < 0 || index >= values.Length)
            {
#if UNITY_EDITOR
                Debug.LogWarning($"[Blackboard] Invalid index or values[] is NULL");
#endif
                return;
            }

            if (!CanWrite(index, value))
            {
                return;
            }

            values[index] = value;
        }
        public object GetBoxed(int index)
        {
            if (values == null || index < 0 || index >= values.Length) return null;
            return values[index];
        }

        public void SetBoxed(int index, object value)
        {
            if (values == null || index < 0 || index >= values.Length)
            {
#if UNITY_EDITOR
                Debug.LogWarning($"[Blackboard] Invalid index or values[] is NULL");
#endif
                return;
            }

            if (!CanWriteBoxed(index, value))
            {
                return;
            }

            values[index] = value;
        }

        private bool CanWrite<T>(int index, T value)
        {
            Type expectedType = slotTypes != null && index >= 0 && index < slotTypes.Length ? slotTypes[index] : null;
            if (expectedType == null) return true;

            if (value == null)
            {
                if (expectedType.IsValueType)
                {
#if UNITY_EDITOR
                    Debug.LogWarning(
                        $"[Blackboard] Type mismatch at index: {index}" +
                        $"Stored = {expectedType.Name}, Trying to write NULL" +
                        $"Cancelling write"
                    );
#endif
                    return false;
                }
                return true;
            }

            Type writeType = typeof(T);
            bool ok = expectedType.IsValueType ? writeType == expectedType : expectedType.IsAssignableFrom(writeType);
            if (!ok)
            {
#if UNITY_EDITOR
                Debug.LogWarning(
                    $"[Blackboard] Type mismatch at index: {index}" +
                    $"Stored = {expectedType.Name}, Trying to write type: {writeType.Name}" +
                    $"Cancelling write"
                );
#endif
                return false;
            }

            return true;
        }

        private bool CanWriteBoxed(int index, object value)
        {
            Type expectedType = slotTypes != null && index >= 0 && index < slotTypes.Length ? slotTypes[index] : null;
            if (expectedType == null) return true;

            if (value == null)
            {
                if (expectedType.IsValueType)
                {
#if UNITY_EDITOR
                    Debug.LogWarning(
                        $"[Blackboard] Type mismatch at index: {index}" +
                        $"Stored = {expectedType.Name}, Trying to write NULL" +
                        $"Cancelling write"
                    );
#endif
                    return false;
                }
                return true;
            }

            Type writeType = value.GetType();
            bool ok = expectedType.IsValueType ? writeType == expectedType : expectedType.IsAssignableFrom(writeType);
            if (!ok)
            {
#if UNITY_EDITOR
                Debug.LogWarning(
                    $"[Blackboard] Type mismatch at index: {index}" +
                    $"Stored = {expectedType.Name}, Trying to write type: {writeType.Name}" +
                    $"Cancelling write"
                );
#endif
                return false;
            }

            return true;
        }
    }
}
