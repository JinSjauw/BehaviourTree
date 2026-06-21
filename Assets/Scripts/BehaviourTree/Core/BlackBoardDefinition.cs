using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace BehaviourTree.Core
{
    public class BlackboardDefinition : ScriptableObject
    {
        /// <summary>The tree asset this baked definition was derived from. Set by TreeBaker.
        /// Used by SquadInstance to match bindings when the baked definition is a different
        /// ScriptableObject instance from the original asset definition.</summary>
        [System.NonSerialized] public BehaviourTreeAssetBase sourceTreeAsset;
        [System.NonSerialized] public string sourceTreeGuid;

        /// <summary>Polymorphic variable storage. Supports any type via BlackboardVariable&lt;T&gt;.</summary>
        [SerializeReference] public List<BlackboardVariableBase> sharedVariables = new();

        /// <summary>Total number of variables.</summary>
        public int VariableCount => sharedVariables?.Count ?? 0;

        /// <summary>Returns a read-only view of all variables. No allocation — returns the raw list directly.</summary>
        public IReadOnlyList<BlackboardVariableBase> GetAllVariables()
        {
            if (sharedVariables == null) return Array.Empty<BlackboardVariableBase>();
            return sharedVariables;
        }

        /// <summary>Finds a variable by name.</summary>
        public BlackboardVariableBase FindVariable(string variableName)
        {
            if (string.IsNullOrEmpty(variableName) || sharedVariables == null)
                return null;

            foreach (BlackboardVariableBase v in sharedVariables)
            {
                if (v.Name == variableName)
                    return v;
            }
            return null;
        }

        /// <summary>Gets the index of a variable by name. Returns -1 if not found.</summary>
        public int GetVariableIndex(string variableName)
        {
            if (string.IsNullOrEmpty(variableName) || sharedVariables == null)
                return -1;

            for (int i = 0; i < sharedVariables.Count; i++)
            {
                if (sharedVariables[i].Name == variableName)
                    return i;
            }
            return -1;
        }

        /// <summary>Creates and adds a new variable of the given type.</summary>
        public BlackboardVariable<T> AddVariable<T>(string name, int stride = 1, T initialValue = default)
        {
            BlackboardVariable<T> variable = new BlackboardVariable<T>(name, stride, initialValue);
            if (sharedVariables == null)
                sharedVariables = new List<BlackboardVariableBase>();
            sharedVariables.Add(variable);
            return variable;
        }

        /// <summary>
        /// Copies a variable definition (name, stride, type) without its values.
        /// Creates a new variable with default(T) and adds it to this definition.
        /// Does nothing if a variable with the same name already exists.
        /// </summary>
        public BlackboardVariableBase CopyVariable(BlackboardVariableBase source)
        {
            if (source == null) return null;

            if (FindVariable(source.Name) != null) return null;

            Type valueType = source.GetValueType();
            if (valueType == null) return null;

            Type genericType = typeof(BlackboardVariable<>).MakeGenericType(valueType);
            BlackboardVariableBase clone = (BlackboardVariableBase)Activator.CreateInstance(genericType);
            clone.Name = source.Name;
            clone.Stride = source.Stride;

            if (sharedVariables == null)
                sharedVariables = new List<BlackboardVariableBase>();
            sharedVariables.Add(clone);
            return clone;
        }

        /// <summary>
        /// Ensures a base channel variable exists on the definition. If the variable
        /// already exists, repairs isSystemVariable if needed and warns on type mismatch.
        /// Returns true if a new variable was created (caller should SetDirty).
        /// </summary>
        public static bool EnsureBaseChannel<T>(
            BlackboardDefinition bbDef, string name, bool isSquadData)
        {
            if (bbDef == null || bbDef.sharedVariables == null)
                return false;

            BlackboardVariableBase existing = bbDef.FindVariable(name);
            if (existing != null)
            {
                bool changed = false;

                if (existing.GetValueType() != typeof(T))
                {
                    Debug.LogWarning(
                        $"[BaseChannel] '{name}' has wrong type " +
                        $"(expected {typeof(T).Name}, got {existing.GetValueType()?.Name ?? "null"}).");
                }

                if (!existing.isSystemVariable)
                {
                    existing.isSystemVariable = true;
                    changed = true;
                }

                return changed;
            }

            Debug.LogWarning($"[BaseChannel] Missing '{name}' on '{bbDef.name}' — auto-creating.");

            bbDef.AddVariable<T>(name, stride: 1, initialValue: default);
            BlackboardVariableBase created = bbDef.FindVariable(name);
            if (created != null)
            {
                created.isSquadData = isSquadData;
                created.isSystemVariable = true;
            }

            return true;
        }
    }
}
