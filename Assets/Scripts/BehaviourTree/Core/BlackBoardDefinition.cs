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
    }
}
