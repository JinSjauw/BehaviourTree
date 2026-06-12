using System;
using System.Collections.Generic;

namespace BehaviourTree.Core
{
    public enum BlackboardSlotKind
    {
        Value = 0,
        Reference = 1,
    }

    public interface IBlackboardStorage
    {
        BlackboardDefinition Definition { get; }
        int Count { get; }
        BlackboardSlotKind GetSlotKind(int index);
        void Initialize(BlackboardDefinition definition);
        void Initialize(IReadOnlyList<BlackboardVariableBase> variables);
        T Get<T>(int index);
        void Set<T>(int index, T value);
        object GetBoxed(int index);
        void SetBoxed(int index, object value);

        /// <summary>
        /// Given a variable index into the unified variable list,
        /// returns the base slot index and stride in the flat values array.
        /// </summary>
        void GetVariableSlotRange(int variableIndex, out int baseSlot, out int stride);
    }
}
