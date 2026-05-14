using System;
using UnityEngine;

namespace BehaviourTree.Core
{
    /// <summary>
    /// Union struct that holds either a constant value or a blackboard variable name.
    /// Stored per-field on ActionNode.
    /// </summary>
    [Serializable]
    public struct NodeFieldEntry
    {
        /// <summary>Name matching the field in the *_Params struct.</summary>
        public string fieldName;

        /// <summary>Whether this entry points to a blackboard variable.</summary>
        public bool isVariable;

        /// <summary>Blackboard variable name (only used when isVariable == true).</summary>
        public string variableName;

        /// <summary>Type of the constant value (unified enum).</summary>
        public FieldType fieldType;

        // Constant values (only one used, determined by fieldType)
        public int intValue;
        public float floatValue;
        public bool boolValue;
        public Vector2 vector2Value;
        public Vector3 vector3Value;
        public GameObject gameObjectValue;
        public Transform transformValue;
    }
}