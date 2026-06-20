using System;
using System.Collections.Generic;
using UnityEngine;

namespace BehaviourTree.Core
{
    public sealed class ManagedBlackboardStorage : IBlackboardStorage
    {
        private BlackboardDefinition definition;
        private IReadOnlyList<BlackboardVariableBase> runtimeVariables; // unified view cached on init
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
                runtimeVariables = null;
                return;
            }

            // Build unified view: old struct vars + new generic vars
            runtimeVariables = definition.GetAllVariables();
            InitializeFromVariables(runtimeVariables);
        }

        /// <summary>Initialize from a list of new-style BlackboardVariableBase entries.</summary>
        public void Initialize(IReadOnlyList<BlackboardVariableBase> variables)
        {
            definition = null;
            runtimeVariables = variables;
            InitializeFromVariables(variables);
        }

        private void InitializeFromVariables(IReadOnlyList<BlackboardVariableBase> variables)
        {
            if (variables == null || variables.Count == 0)
            {
                values = null;
                slotTypes = null;
                slotKinds = null;
                return;
            }

            int varCount = variables.Count;

            int totalSlotCount = 0;
            for (int i = 0; i < varCount; i++)
            {
                int stride = variables[i].Stride;
                totalSlotCount += (stride > 1) ? stride : 1;
            }

            values = new object[totalSlotCount];
            slotTypes = new Type[totalSlotCount];
            slotKinds = new BlackboardSlotKind[totalSlotCount];

            int slotIndex = 0;
            for (int varIndex = 0; varIndex < varCount; varIndex++)
            {
                BlackboardVariableBase variable = variables[varIndex];
                Type slotType = ResolveVariableType(variable);
                int variableStride = variable.Stride;
                int actualStride = (variableStride > 1) ? variableStride : 1;

                for (int slotOffset = 0; slotOffset < actualStride; slotOffset++)
                {
                    slotTypes[slotIndex + slotOffset] = slotType;
                    slotKinds[slotIndex + slotOffset] = (slotType != null && !slotType.IsValueType) ? BlackboardSlotKind.Reference : BlackboardSlotKind.Value;
                    values[slotIndex + slotOffset] = variable.GetBoxedValue(slotOffset);
                }

                slotIndex += actualStride;
            }
        }

        private static Type ResolveVariableType(BlackboardVariableBase variable)
        {
            Type type = variable.GetValueType();
            if (type == null && Debug.isDebugBuild)
                Debug.LogWarning($"[Blackboard] Unresolved typeName '{variable.TypeName}' for variable '{variable.Name}'.");
            return type;
        }

        /// <summary>
        /// Given a variable index into the unified variable list,
        /// returns the base slot index and stride in the flat values array.
        /// </summary>
        public void GetVariableSlotRange(int variableIndex, out int baseSlot, out int stride)
        {
            if (runtimeVariables == null || variableIndex < 0 || variableIndex >= runtimeVariables.Count)
            {
                baseSlot = -1;
                stride = -1;
                Debug.LogError($"[ManagedBlackboardStorage] GetVariableSlotRange: variableIndex {variableIndex} is out of bounds ({(runtimeVariables?.Count ?? 0)} variables). Returning sentinel (-1, -1).");
                return;
            }

            baseSlot = 0;
            stride = 1;

            for (int prevIndex = 0; prevIndex < variableIndex; prevIndex++)
            {
                int prevStride = runtimeVariables[prevIndex].Stride;
                baseSlot += (prevStride > 1) ? prevStride : 1;
            }

            stride = runtimeVariables[variableIndex].Stride;
            if (stride <= 1) stride = 1;
        }

        /// <summary>
        /// Rebuilds the internal storage arrays from the given variable list,
        /// preserving existing data where slot offsets overlap between old and new layouts.
        /// Used by CommanderTreeRunner when resizing squad-data strides dynamically.
        /// </summary>
        public void ResizeFromVariables(IReadOnlyList<BlackboardVariableBase> variables)
        {
            if (variables == null || variables.Count == 0)
            {
                values = null;
                slotTypes = null;
                slotKinds = null;
                runtimeVariables = null;
                return;
            }

            int newSlotCount = 0;
            for (int i = 0; i < variables.Count; i++)
            {
                int stride = variables[i].Stride;
                newSlotCount += (stride > 1) ? stride : 1;
            }

            object[] newValues = new object[newSlotCount];
            Type[] newSlotTypes = new Type[newSlotCount];
            BlackboardSlotKind[] newSlotKinds = new BlackboardSlotKind[newSlotCount];

            int oldSlot = 0;
            int newSlot = 0;

            for (int varIndex = 0; varIndex < variables.Count; varIndex++)
            {
                int oldStride = 1;
                if (runtimeVariables != null && varIndex < runtimeVariables.Count)
                {
                    int stride = runtimeVariables[varIndex].Stride;
                    oldStride = (stride > 1) ? stride : 1;
                }

                int newStrideRaw = variables[varIndex].Stride;
                int newStride = (newStrideRaw > 1) ? newStrideRaw : 1;
                int copyCount = Mathf.Min(oldStride, newStride);

                Type slotType = (slotTypes != null && oldSlot < slotTypes.Length)
                    ? slotTypes[oldSlot]
                    : ResolveVariableType(variables[varIndex]);
                BlackboardSlotKind kind = (slotType != null && !slotType.IsValueType) ? BlackboardSlotKind.Reference : BlackboardSlotKind.Value;

                for (int j = 0; j < copyCount; j++)
                {
                    if (values != null && oldSlot + j < values.Length)
                        newValues[newSlot + j] = values[oldSlot + j];
                    else
                        newValues[newSlot + j] = variables[varIndex].GetBoxedValue(j);

                    newSlotTypes[newSlot + j] = slotType;
                    newSlotKinds[newSlot + j] = kind;
                }

                // New slots beyond old stride get default values
                for (int j = copyCount; j < newStride; j++)
                {
                    newValues[newSlot + j] = variables[varIndex].GetBoxedValue(j);
                    newSlotTypes[newSlot + j] = slotType;
                    newSlotKinds[newSlot + j] = kind;
                }

                oldSlot += oldStride;
                newSlot += newStride;
            }

            values = newValues;
            slotTypes = newSlotTypes;
            slotKinds = newSlotKinds;
            runtimeVariables = variables;
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

#if UNITY_EDITOR
            Debug.LogError($"[Blackboard] Unexpected type in BB: index {index} expected {typeof(T).Name} but found {val?.GetType().Name ?? "null"}. Data loss may have occurred.");
#endif
            return default;
        }

        public void Set<T>(int index, T value)
        {
            if (!CanWrite<T>(index, value))
            {
                Debug.LogWarning($"[Blackboard.Set] Type mismatch at index {index}: expected {(slotTypes != null && index < slotTypes.Length ? slotTypes[index]?.Name : "unknown")}, got {typeof(T).Name}");
                return;
            }

            values[index] = value;
        }

        public object GetBoxed(int index)
        {
            if (values == null || index < 0 || index >= values.Length)
                return null;
            return values[index];
        }

        public void SetBoxed(int index, object value)
        {
            if (values == null || index < 0 || index >= values.Length)
                return;
            if (!CanWriteBoxed(index, value)) return;
            values[index] = value;
        }

        private bool CanWrite<T>(int index, T value)
        {
            if (values == null || index < 0 || index >= values.Length) return false;
            Type slotType = slotTypes?[index];
            if (slotType == null) return true; // unresolved type — allow write

            if (!slotType.IsValueType)
            {
                // Reference types: allow subclasses, accept null
                if (value == null) return true;
                return slotType.IsAssignableFrom(typeof(T));
            }

            // Value types: require exact match, reject null
            if (value == null) return false;
            return slotType == typeof(T);
        }

        private bool CanWriteBoxed(int index, object value)
        {
            if (values == null || index < 0 || index >= values.Length) return false;
            Type slotType = slotTypes?[index];
            if (slotType == null) return true;

            if (!slotType.IsValueType)
            {
                if (value == null) return true;
                return slotType.IsAssignableFrom(value.GetType());
            }

            if (value == null) return false;
            return slotType == value.GetType();
        }
    }
}
