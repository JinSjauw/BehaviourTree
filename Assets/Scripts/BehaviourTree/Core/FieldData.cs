using System;
using System.Runtime.InteropServices;

namespace BehaviourTree
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
            [FieldOffset(0)] public float f;
            [FieldOffset(0)] public int i;
        }

        public static FieldData FromConstant(int v) => new FieldData { mode = 0, value = v };
        public static FieldData FromConstant(float v)
        {
            return new FieldData { mode = 0, value = new FloatIntUnion { f = v }.i };
        }
        public static FieldData FromConstant(bool v)
        {
            return new FieldData { mode = 0, value = v ? 1 : 0 };
        }

        public static FieldData FromVariable(int blackboardIndex) => new FieldData { mode = 1, value = blackboardIndex };

        public bool IsVariable => mode == 1;
        public bool IsConstant => mode == 0;
        public int GetInt() => value;
        public float GetFloat() => new FloatIntUnion { i = value }.f;
        public bool GetBool() => value != 0;
    }
}
