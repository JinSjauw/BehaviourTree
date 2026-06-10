using System;
using System.Runtime.InteropServices;

namespace BehaviourTree.Core
{
    /// <summary>
    /// Packed field data for a single parameter.
    /// mode: 0 = constant (value holds the bits), 1 = blackboard variable (value holds the index).
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Explicit)]
    public struct FieldData
    {
        [FieldOffset(0)] public byte mode;

        /// <summary> 4 bytes – either constant bits or blackboard variable index.</summary>
        [FieldOffset(1)] public int value;

        [StructLayout(LayoutKind.Explicit)]
        private struct FloatIntUnion
        {
            [FieldOffset(0)] public float floatValue;
            [FieldOffset(0)] public int intValue;
        }

        public static FieldData FromConstant(int value) => new FieldData { mode = 0, value = value };
        public static FieldData FromConstant(float value)
        {
            return new FieldData { mode = 0, value = new FloatIntUnion { floatValue = value }.intValue };
        }
        public static FieldData FromConstant(bool value)
        {
            return new FieldData { mode = 0, value = value ? 1 : 0 };
        }

        public static FieldData FromVariable(int blackboardIndex) => new FieldData { mode = 1, value = blackboardIndex };

        public bool IsVariable => mode == 1;
        public bool IsConstant => mode == 0;
        public int GetInt() => value;
        public float GetFloat() => new FloatIntUnion { intValue = value }.floatValue;
        public bool GetBool() => value != 0;
    }
}
