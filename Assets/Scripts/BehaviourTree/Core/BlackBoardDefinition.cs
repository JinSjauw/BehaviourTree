using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace BehaviourTree.Core
{
    public class BlackboardDefinition : ScriptableObject
    {
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
    }
}
