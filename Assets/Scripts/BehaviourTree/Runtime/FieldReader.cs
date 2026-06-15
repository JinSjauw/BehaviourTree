using System;
using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree
{
    /// <summary>
    /// Ref struct that reads values from a ReadOnlySpan<FieldData>.
    /// Handles both constant and blackboard-variable lookup.
    /// Also provides Set methods to write back to the blackboard (variable fields only).
    /// Generic Get&lt;T&gt;/Set&lt;T&gt; methods support any type via boxing or boxed constants.
    /// </summary>
    public ref struct FieldReader
    {
        private readonly ReadOnlySpan<FieldData> fields;
        private readonly object[] boxedConstants;
        private readonly BlackBoard blackboard;

        public FieldReader(ReadOnlySpan<FieldData> fields, BlackBoard blackboard)
            : this(fields, blackboard, null) { }

        public FieldReader(ReadOnlySpan<FieldData> fields, BlackBoard blackboard, object[] boxedConstants)
        {
            this.fields = fields;
            this.blackboard = blackboard;
            this.boxedConstants = boxedConstants;
        }

        // ─── Generic (new — supports any type) ───────────────────────

        /// <summary>
        /// Reads a value from the field data at the given index.
        /// Constants: returns packed value (int/float/bool) or boxed constant.
        /// Variables: reads from the blackboard.
        /// </summary>
        public T Get<T>(int index)
        {
            ref readonly FieldData fd = ref fields[index];
            if (fd.IsConstant)
            {
                return ReadPackedConstant<T>(fd);
            }
            if (fd.IsBoxedConstant)
            {
                return fd.GetBoxedConstant<T>(boxedConstants);
            }
            // Variable — read from blackboard
            return blackboard.Get<T>(fd.value);
        }

        /// <summary>Sets a value to the blackboard at the given field index (variable fields only).</summary>
        public void Set<T>(int index, T value)
        {
            ref readonly FieldData fd = ref fields[index];
            if (fd.IsVariable)
            {
                blackboard.Set(fd.value, value);
            }
            else
            {
                Debug.LogWarning($"[FieldReader.Set] Field {index} is constant — write ignored.");
            }
        }

        private static T ReadPackedConstant<T>(FieldData fd)
        {
            Type t = typeof(T);
            if (t == typeof(int))    return (T)(object)fd.GetInt();
            if (t == typeof(float))  return (T)(object)fd.GetFloat();
            if (t == typeof(bool))   return (T)(object)fd.GetBool();
            if (t.IsEnum)            return (T)Enum.ToObject(t, fd.GetInt());
            Debug.LogWarning($"[FieldReader.Get<{t.Name}>] Type cannot be packed as a 4-byte constant. Use a BB variable or boxed constant.");
            return default;
        }

        // ─── Int ──────────────────────────────────────────────────────

        public int GetInt(int index)
        {
            ref readonly FieldData fd = ref fields[index];
            if (fd.IsConstant) return fd.GetInt();
            return blackboard.Get<int>(fd.value);
        }

        public void SetInt(int index, int value)
        {
            ref readonly FieldData fd = ref fields[index];
            if (fd.IsVariable)
                blackboard.Set(fd.value, value);
            else
                Debug.LogWarning($"[FieldReader.SetInt] Field {index} is constant — write ignored.");
        }

        // ─── Float ────────────────────────────────────────────────────

        public float GetFloat(int index)
        {
            ref readonly FieldData fd = ref fields[index];
            if (fd.IsConstant) return fd.GetFloat();
            return blackboard.Get<float>(fd.value);
        }

        public void SetFloat(int index, float value)
        {
            ref readonly FieldData fd = ref fields[index];
            if (fd.IsVariable)
                blackboard.Set(fd.value, value);
            else
                Debug.LogWarning($"[FieldReader.SetFloat] Field {index} is constant — write ignored.");
        }

        // ─── Bool ─────────────────────────────────────────────────────

        public bool GetBool(int index)
        {
            ref readonly FieldData fd = ref fields[index];
            if (fd.IsConstant) return fd.GetBool();
            return blackboard.Get<bool>(fd.value);
        }

        public void SetBool(int index, bool value)
        {
            ref readonly FieldData fd = ref fields[index];
            if (fd.IsVariable)
                blackboard.Set(fd.value, value);
            else
                Debug.LogWarning($"[FieldReader.SetBool] Field {index} is constant — write ignored.");
        }

        public T GetEnum<T>(int index) where T : struct
        {
            Type enumType = typeof(T);
            if (!enumType.IsEnum)
            {
                Debug.LogWarning($"[FieldReader.GetEnum] {enumType.Name} is not an enum. Returning default.");
                return default;
            }

            ref readonly FieldData fd = ref fields[index];
            if (fd.IsConstant)
                return (T)Enum.ToObject(enumType, fd.value);

            object raw = blackboard.Get<object>(fd.value);
            if (raw is T typed) return typed;
            if (raw is int rawInt) return (T)Enum.ToObject(enumType, rawInt);
            return default;
        }

        public void SetEnum<T>(int index, T value) where T : struct
        {
            Type t = typeof(T);
            if (!t.IsEnum)
            {
                Debug.LogWarning($"[FieldReader.SetEnum] {t.Name} is not an enum. Write ignored.");
                return;
            }

            ref readonly FieldData fd = ref fields[index];
            if (fd.IsVariable)
                blackboard.Set(fd.value, value);
            else
                Debug.LogWarning($"[FieldReader.SetEnum] Field {index} is constant — write ignored.");
        }

        // ─── Vector2 ──────────────────────────────────────────────────

        public Vector2 GetVector2(int index)
        {
            ref readonly FieldData fd = ref fields[index];
            if (fd.IsConstant)
            {
                Debug.LogWarning($"[FieldReader.GetVector2] field is constant. Vector2 not supported as packed constant.");
                return Vector2.zero;
            }
            return blackboard.Get<Vector2>(fd.value);
        }

        public void SetVector2(int index, Vector2 value)
        {
            ref readonly FieldData fd = ref fields[index];
            if (fd.IsVariable)
                blackboard.Set(fd.value, value);
            else
                Debug.LogWarning($"[FieldReader.SetVector2] Field {index} is constant — write ignored.");
        }

        // ─── Vector3 ──────────────────────────────────────────────────

        public Vector3 GetVector3(int index)
        {
            ref readonly FieldData fd = ref fields[index];
            if (fd.IsConstant)
            {
                Debug.LogWarning($"[FieldReader.GetVector3] field is constant. Vector3 not supported as packed constant.");
                return Vector3.zero;
            }
            return blackboard.Get<Vector3>(fd.value);
        }

        public void SetVector3(int index, Vector3 value)
        {
            ref readonly FieldData fd = ref fields[index];
            if (fd.IsVariable)
                blackboard.Set(fd.value, value);
            else
                Debug.LogWarning($"[FieldReader.SetVector3] Field {index} is constant — write ignored.");
        }

        // ─── GameObject ───────────────────────────────────────────────

        public GameObject GetGameObject(int index)
        {
            ref readonly FieldData fd = ref fields[index];
            if (fd.IsConstant)
            {
                Debug.LogWarning($"[FieldReader.GetGameObject] field is constant. GameObject not supported as packed constant.");
                return null;
            }
            return blackboard.Get<GameObject>(fd.value);
        }

        public void SetGameObject(int index, GameObject value)
        {
            ref readonly FieldData fd = ref fields[index];
            if (fd.IsVariable)
                blackboard.Set(fd.value, value);
            else
                Debug.LogWarning($"[FieldReader.SetGameObject] Field {index} is constant — write ignored.");
        }

        // ─── Transform ────────────────────────────────────────────────

        public Transform GetTransform(int index)
        {
            ref readonly FieldData fd = ref fields[index];
            if (fd.IsConstant)
            {
                Debug.LogWarning($"[FieldReader.GetTransform] field is constant. Transform not supported as packed constant.");
                return null;
            }
            return blackboard.Get<Transform>(fd.value);
        }

        public void SetTransform(int index, Transform value)
        {
            ref readonly FieldData fd = ref fields[index];
            if (fd.IsVariable)
                blackboard.Set(fd.value, value);
            else
                Debug.LogWarning($"[FieldReader.SetTransform] Field {index} is constant — write ignored.");
        }
    }
}
