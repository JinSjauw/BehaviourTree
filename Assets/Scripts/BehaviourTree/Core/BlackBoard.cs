using System.Collections.Generic;
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
        protected Dictionary<BlackBoardFields, object> blackBoardValues = new Dictionary<BlackBoardFields, object>();

        /// <summary>Index-based storage for blackboard variables. Index corresponds to position in BlackboardDefinition.</summary>
        protected object[] values;

        public abstract void RegisterFields();

        /// <summary>Initialize index-based storage from a definition.</summary>
        public void Initialize(BlackboardDefinition definition)
        {
            if (definition == null) return;
            values = new object[definition.variables.Count];
        }

        public object GetField(BlackBoardFields fieldType)
        {
            if (!blackBoardValues.ContainsKey(fieldType)) return null;
            return blackBoardValues[fieldType];
        }

        public void UpdateField(BlackBoardFields fieldType, object value)
        {
            if (!blackBoardValues.ContainsKey(fieldType)) return;
            blackBoardValues[fieldType] = value;
        }

        /// <summary>Get a value by index in the blackboard array.</summary>
        public T Get<T>(int index)
        {
            if (values == null || index < 0 || index >= values.Length)
                return default;
            object val = values[index];
            if (val is T tVal)
                return tVal;
            return default;
        }

        /// <summary>Set a value by index in the blackboard array.</summary>
        public void Set<T>(int index, T value)
        {
            if (values == null || index < 0 || index >= values.Length)
                return;
            values[index] = value;
        }
    }
}

