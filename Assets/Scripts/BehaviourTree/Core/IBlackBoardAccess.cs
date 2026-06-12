using UnityEngine;

namespace BehaviourTree.Core
{
    /// <summary>
    /// Minimal blackboard read/write contract used by FieldBinding to resolve
    /// [SharedVar] fields without coupling to the concrete BlackBoard implementation.
    /// OOP BlackBoard and DOTS DotsBlackboard both satisfy this interface.
    /// </summary>
    public interface IBlackBoardAccess
    {
        T Get<T>(int slot);
        void Set<T>(int slot, T value);
        object GetBoxed(int slot);
        void SetBoxed(int slot, object value);
    }
}
