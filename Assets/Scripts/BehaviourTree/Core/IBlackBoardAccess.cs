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
        int GetInt(int slot);
        void SetInt(int slot, int value);
        float GetFloat(int slot);
        void SetFloat(int slot, float value);
        bool GetBool(int slot);
        void SetBool(int slot, bool value);
        Vector2 GetVector2(int slot);
        void SetVector2(int slot, Vector2 value);
        Vector3 GetVector3(int slot);
        void SetVector3(int slot, Vector3 value);
        GameObject GetGameObject(int slot);
        void SetGameObject(int slot, GameObject value);
        Transform GetTransform(int slot);
        void SetTransform(int slot, Transform value);
    }
}
