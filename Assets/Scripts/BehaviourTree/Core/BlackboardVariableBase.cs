using System;
using UnityEngine;

namespace BehaviourTree.Core
{
    /// <summary>
    /// Non-generic base for all blackboard variable types.
    /// Provides name, stride, and type-agnostic value access.
    /// Subclassed by BlackboardVariable&lt;T&gt; for type-safe storage.
    /// </summary>
    [Serializable]
    public abstract class BlackboardVariableBase
    {
        [SerializeField] private string variableName;
        [SerializeField] private int variableStride = 1;
        [SerializeField] private string variableTypeName;

        public string Name
        {
            get => variableName;
            set => variableName = value;
        }

        public int Stride
        {
            get => variableStride;
            set => variableStride = Mathf.Max(1, value);
        }

        public string TypeName
        {
            get => variableTypeName;
            protected set => variableTypeName = value;
        }

        public bool IsArray => variableStride > 1;

        /// <summary>Resolves the System.Type from the stored type name.</summary>
    public Type GetValueType()
    {
        if (string.IsNullOrEmpty(variableTypeName))
            return null;

        // Cache the resolved Type to avoid expensive Type.GetType() reflection every call.
        // Invalidated automatically when variableTypeName changes (serialized field).
        if (cachedTypeName == variableTypeName && cachedType != null)
            return cachedType;

        cachedTypeName = variableTypeName;
        cachedType = FieldTypeHelper.TryGetSystemTypeFromName(variableTypeName, out Type resolved) ? resolved : null;
        return cachedType;
    }

    [NonSerialized] private Type cachedType;
    [NonSerialized] private string cachedTypeName;

        /// <summary>Returns the boxed value for a given slot element index.</summary>
        public abstract object GetBoxedValue(int elementIndex = 0);

        /// <summary>Sets the boxed value for a given slot element index.</summary>
        public abstract void SetBoxedValue(object value, int elementIndex = 0);

        /// <summary>Creates a shallow clone with the same name, stride, and type but default values.</summary>
        public virtual BlackboardVariableBase Clone()
        {
            return null;
        }

        /// <summary>Returns true if the resolved type is a value type (not a UnityEngine.Object or other reference type).</summary>
        public bool IsValueType()
        {
            Type type = GetValueType();
            return type != null && type.IsValueType && !type.IsEnum;
        }
    }
}
