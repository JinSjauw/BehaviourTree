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

        /// <summary>4 bytes – either constant bits or variable index.</summary>
        [FieldOffset(1)] public int value;

        // Helpers to pack / unpack floats and bigger types

        public static FieldData FromConstant(int v) => new FieldData { mode = 0, value = v };
        public static unsafe FieldData FromConstant(float v)
        {
            FieldData fd = default;
            fd.mode = 0;
            *(float*)&fd.value = v;
            return fd;
        }
        public static unsafe FieldData FromConstant(bool v)
        {
            FieldData fd = default;
            fd.mode = 0;
            fd.value = v ? 1 : 0;
            return fd;
        }

        public static FieldData FromVariable(int blackboardIndex) => new FieldData { mode = 1, value = blackboardIndex };

        public bool IsVariable => mode == 1;
        public bool IsConstant => mode == 0;

        public int GetInt() => value;
        public unsafe float GetFloat() { int tmp = value; return *(float*)&tmp; }
        public bool GetBool() => value != 0;
    }
}