using System;
using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree
{
    /// <summary>
    /// Ref struct that reads values from a ReadOnlySpan<FieldData>.
    /// Handles both constant and blackboard-variable lookup.
    /// </summary>
    public ref struct FieldReader
    {
        private readonly ReadOnlySpan<FieldData> fields;
        private readonly BlackBoard blackboard;

        public FieldReader(ReadOnlySpan<FieldData> fields, BlackBoard blackboard)
        {
            this.fields = fields;
            this.blackboard = blackboard;
        }

        public int GetInt(int index)
        {
            ref readonly FieldData fd = ref fields[index];
            if (fd.IsConstant)
                return fd.GetInt();
            return blackboard.Get<int>(fd.value);
        }

        public float GetFloat(int index)
        {
            ref readonly FieldData fd = ref fields[index];
            if (fd.IsConstant)
                return fd.GetFloat();
            return blackboard.Get<float>(fd.value);
        }

        public bool GetBool(int index)
        {
            ref readonly FieldData fd = ref fields[index];
            if (fd.IsConstant)
                return fd.GetBool();
            return blackboard.Get<bool>(fd.value);
        }

        public Vector2 GetVector2(int index)
        {
            ref readonly FieldData fd = ref fields[index];
            if (fd.IsConstant)
                return blackboard.Get<Vector2>(fd.value);
            return blackboard.Get<Vector2>(fd.value);
        }

        public Vector3 GetVector3(int index)
        {
            ref readonly FieldData fd = ref fields[index];
            if (fd.IsConstant)
                return blackboard.Get<Vector3>(fd.value);
            return blackboard.Get<Vector3>(fd.value);
        }

        public GameObject GetGameObject(int index)
        {
            ref readonly FieldData fd = ref fields[index];
            if (fd.IsConstant)
                return blackboard.Get<GameObject>(fd.value);
            return blackboard.Get<GameObject>(fd.value);
        }

        public Transform GetTransform(int index)
        {
            ref readonly FieldData fd = ref fields[index];
            if (fd.IsConstant)
                return blackboard.Get<Transform>(fd.value);
            return blackboard.Get<Transform>(fd.value);
        }
    }
}