using System;

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
        T Get<T>(int index);
        void Set<T>(int index, T value);
        object GetBoxed(int index);
        void SetBoxed(int index, object value);
    }
}
