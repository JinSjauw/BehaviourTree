using System;
using UnityEngine;

namespace BehaviourTree.Core
{
    /// <summary>
    /// Typed blackboard variable storing a value of type T.
    /// Supports single values and per-element arrays (stride > 1).
    /// Serialized natively by Unity for common types.
    /// </summary>
    [Serializable]
    public class BlackboardVariable<T> : BlackboardVariableBase
    {
        [SerializeField] private T singleValue;
        [SerializeField] private T[] arrayValues;

        public BlackboardVariable()
        {
            TypeName = typeof(T).AssemblyQualifiedName;
        }

        /// <summary>Constructor with name, stride, and initial value.</summary>
        public BlackboardVariable(string name, int stride, T initialValue) : this()
        {
            Name = name;
            Stride = stride;
            singleValue = initialValue;
        }

        public T Value
        {
            get => singleValue;
            set => singleValue = value;
        }

        public T[] ArrayValues
        {
            get => arrayValues;
            set => arrayValues = value;
        }

        /// <summary>Gets the value for a specific element index. For stride=1, returns singleValue.</summary>
        public T GetValue(int elementIndex = 0)
        {
            if (Stride > 1 && arrayValues != null && elementIndex >= 0 && elementIndex < arrayValues.Length)
                return arrayValues[elementIndex];
            return singleValue;
        }

        /// <summary>Sets the value for a specific element index.</summary>
        public void SetValue(T value, int elementIndex = 0)
        {
            if (Stride > 1)
            {
                EnsureArraySize();
                if (elementIndex >= 0 && elementIndex < arrayValues.Length)
                    arrayValues[elementIndex] = value;
            }
            else
            {
                singleValue = value;
            }
        }

        public override object GetBoxedValue(int elementIndex = 0)
        {
            return GetValue(elementIndex);
        }

        public override void SetBoxedValue(object value, int elementIndex = 0)
        {
            if (value is T typedValue)
                SetValue(typedValue, elementIndex);
        }

        /// <summary>Ensures the array values array matches the stride.</summary>
        public void EnsureArraySize()
        {
            int stride = Stride;
            if (stride <= 1)
            {
                arrayValues = null;
                return;
            }

            T defaultValue = singleValue;
            if (arrayValues == null || arrayValues.Length != stride)
            {
                T[] newArray = new T[stride];
                if (arrayValues != null)
                {
                    int copyCount = Mathf.Min(arrayValues.Length, stride);
                    Array.Copy(arrayValues, newArray, copyCount);
                }
                for (int i = (arrayValues != null ? Mathf.Min(arrayValues.Length, stride) : 0); i < stride; i++)
                    newArray[i] = defaultValue;
                arrayValues = newArray;
            }
        }

        public override BlackboardVariableBase Clone()
        {
            return CloneTyped();
        }

        /// <summary>Creates a deep clone of this variable (new instance, same values).</summary>
        public BlackboardVariable<T> CloneTyped()
        {
            BlackboardVariable<T> clone = new BlackboardVariable<T>
            {
                Name = Name,
                Stride = Stride,
                singleValue = singleValue
            };
            if (arrayValues != null)
            {
                clone.arrayValues = new T[arrayValues.Length];
                Array.Copy(arrayValues, clone.arrayValues, arrayValues.Length);
            }
            return clone;
        }
    }
}
