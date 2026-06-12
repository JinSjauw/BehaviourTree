using System;
using System.Runtime.InteropServices;

namespace BehaviourTree.Core
{
    /// <summary>
    /// Packed field data for a single parameter.
    /// mode: 0 = packed constant (int/float/bool/enum in 4 bytes)
    ///       1 = blackboard variable (value holds the slot index)
    ///       2 = boxed constant (value holds index into boxedConstants array)
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Explicit)]
    public struct FieldData
    {
        [FieldOffset(0)] public byte mode;

        /// <summary>4 bytes — constant bits, blackboard slot index, or boxed constant index.</summary>
        [FieldOffset(1)] public int value;

        [StructLayout(LayoutKind.Explicit)]
        private struct FloatIntUnion
        {
            [FieldOffset(0)] public float floatValue;
            [FieldOffset(0)] public int intValue;
        }

        // ── Factory methods ─────────────────────────────────────────

        public static FieldData FromConstant(int value) => new FieldData { mode = 0, value = value };
        public static FieldData FromConstant(float value)
        {
            return new FieldData { mode = 0, value = new FloatIntUnion { floatValue = value }.intValue };
        }
        public static FieldData FromConstant(bool value)
        {
            return new FieldData { mode = 0, value = value ? 1 : 0 };
        }

        public static FieldData FromVariable(int blackboardSlotIndex) => new FieldData { mode = 1, value = blackboardSlotIndex };

        /// <summary>Creates a FieldData referencing a boxed constant in the runtime boxedConstants array.</summary>
        public static FieldData FromBoxedConstant(int boxedIndex) => new FieldData { mode = 2, value = boxedIndex };

        // ── Queries ──────────────────────────────────────────────────

        public bool IsVariable => mode == 1;
        public bool IsConstant => mode == 0;
        public bool IsBoxedConstant => mode == 2;

        // ── Packed value readers (mode == 0 only) ────────────────────

        public int GetInt() => value;
        public float GetFloat() => new FloatIntUnion { intValue = value }.floatValue;
        public bool GetBool() => value != 0;

        // ── Boxed constant reader ────────────────────────────────────

        /// <summary>Reads a boxed constant from a parallel object array.</summary>
        public T GetBoxedConstant<T>(object[] boxedConstants)
        {
            if (!IsBoxedConstant || boxedConstants == null || value < 0 || value >= boxedConstants.Length)
                return default;
            object obj = boxedConstants[value];
            if (obj is T typed)
                return typed;
            return default;
        }
    }
}
