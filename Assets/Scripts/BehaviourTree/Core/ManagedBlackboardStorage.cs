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

            int varCount = definition.sharedVariables.Count;

            // First pass: compute total slot count accounting for stride
            int totalSlotCount = 0;
            for (int i = 0; i < varCount; i++)
            {
                int stride = definition.sharedVariables[i].stride;
                totalSlotCount += (stride > 1) ? stride : 1;
            }

            values = new object[totalSlotCount];
            slotTypes = new Type[totalSlotCount];
            slotKinds = new BlackboardSlotKind[totalSlotCount];

            // Second pass: fill slots, expanding strided variables
            int slotIndex = 0;
            for (int varIndex = 0; varIndex < varCount; varIndex++)
            {
                BlackboardVariable variable = definition.sharedVariables[varIndex];
                Type slotType = null;
                if (!FieldTypeHelper.TryGetSystemTypeFromName(variable.typeName, out slotType))
                {
                    if (Debug.isDebugBuild)
                    {
                        Debug.LogWarning($"[Blackboard] Unresolved typeName '{variable.typeName}' for variable '{variable.name}' (variableIndex {varIndex}).");
                    }
                }

                int variableStride = variable.stride;
                int actualStride = (variableStride > 1) ? variableStride : 1;

                for (int slotOffset = 0; slotOffset < actualStride; slotOffset++)
                {
                    slotTypes[slotIndex + slotOffset] = slotType;
                    slotKinds[slotIndex + slotOffset] = (slotType != null && !slotType.IsValueType) ? BlackboardSlotKind.Reference : BlackboardSlotKind.Value;
                    values[slotIndex + slotOffset] = variable.GetInitialValue(slotOffset);
                }

                slotIndex += actualStride;
            }
        }

        /// <summary>
        /// Given a variable index into definition.sharedVariables,
        /// returns the base slot index and stride in the flat values array.
        /// For stride=1 variables, the slot is at the exact index.
        /// For stride>1 variables (per-agent arrays), baseSlot is the start.
        /// </summary>
        public void GetVariableSlotRange(int variableIndex, out int baseSlot, out int stride)
        {
            baseSlot = 0;
            stride = 1;

            if (definition == null || definition.sharedVariables == null
                || variableIndex < 0 || variableIndex >= definition.sharedVariables.Count)
            {
                Debug.LogError($"[ManagedBlackboardStorage] GetVariableSlotRange: variableIndex {variableIndex} is out of bounds (definition has {definition?.sharedVariables?.Count ?? 0} variables).");
                return;
            }

            for (int prevIndex = 0; prevIndex < variableIndex; prevIndex++)
            {
                int prevStride = definition.sharedVariables[prevIndex].stride;
                baseSlot += (prevStride > 1) ? prevStride : 1;
            }

            stride = definition.sharedVariables[variableIndex].stride;
            if (stride <= 1) stride = 1;
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
                Debug.LogWarning($"[Blackboard] Invalid index or values[] is NULL, returning default {values} : {index} : {typeof(T).Name}");
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
            if (expectedType == null)
            {
                Debug.LogError($"[Blackboard] Invalid index or slotTypes[] is NULL");
                return false;
            }

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
            if (expectedType == null) 
            {
                Debug.LogError($"[Blackboard] Invalid index or slotTypes[] is NULL");
                return false; 
            }

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
