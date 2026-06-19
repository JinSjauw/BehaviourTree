using System;
using System.Collections.Generic;
using System.Linq;
using BehaviourTree.Core;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace BehaviourTree.Editor
{
    /// <summary>
    /// Central registry of types available for blackboard variable creation.
    /// Each registered type has a field factory (creates the UI Toolkit editor
    /// VisualElement) and a binder (reads/writes values from a BlackboardVariableBase).
    ///
    /// Built-in types (int, float, bool, string, Vector2/3/4, Color, Quaternion,
    /// GameObject, Transform, Material) are auto-registered in the static constructor.
    /// Custom types call Register() once, typically from [InitializeOnLoadMethod].
    /// </summary>
    public static class VariableTypeRegistry
    {
        private static readonly Dictionary<Type, Func<VisualElement>> fieldFactories = new();
        private static readonly Dictionary<Type, Action<VisualElement, BlackboardVariableBase, int>> binders = new();

        static VariableTypeRegistry()
        {
            // Value types
            Register(typeof(int),       () => new IntegerField());
            Register(typeof(float),     () => new FloatField());
            Register(typeof(bool),      () => new Toggle());
            Register(typeof(string),    () => new TextField());
            Register(typeof(Vector2),   () => new Vector2Field());
            Register(typeof(Vector3),   () => new Vector3Field());
            Register(typeof(Vector4),   () => new Vector4Field());
            Register(typeof(Color),     () => new ColorField());
            Register(typeof(Quaternion),() => new Vector4Field());

            // Reference types
            Register(typeof(GameObject),() => new ObjectField { objectType = typeof(GameObject), allowSceneObjects = false });
            Register(typeof(Transform), () => new ObjectField { objectType = typeof(Transform), allowSceneObjects = false });
            Register(typeof(Material),  () => new ObjectField { objectType = typeof(Material), allowSceneObjects = false });
        }

        /// <summary>All registered types (derived from factory keys).</summary>
        public static IReadOnlyList<Type> Types => fieldFactories.Keys.ToList();

        /// <summary>
        /// Register a type with a factory that creates the value editor VisualElement,
        /// and an optional custom binder. If binder is null, a default binder is generated
        /// using type-specific dispatch against GetBoxedValue/SetBoxedValue.
        /// </summary>
        public static void Register(
            Type type,
            Func<VisualElement> fieldFactory,
            Action<VisualElement, BlackboardVariableBase, int> binder = null)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            if (fieldFactory == null)
                throw new ArgumentNullException(nameof(fieldFactory));

            fieldFactories[type] = fieldFactory;
            binders[type] = binder ?? GenerateDefaultBinder(type);
        }

        /// <summary>Returns true and the factory if the type has a registered field editor.</summary>
        public static bool TryGetFieldFactory(Type type, out Func<VisualElement> factory)
        {
            return fieldFactories.TryGetValue(type, out factory);
        }

        /// <summary>Returns true and the binder if the type has a registered binder.</summary>
        public static bool TryGetBinder(Type type, out Action<VisualElement, BlackboardVariableBase, int> binder)
        {
            return binders.TryGetValue(type, out binder);
        }

        // ── Default binder generation (type-specific, no reflection overhead) ────

        private static Action<VisualElement, BlackboardVariableBase, int> GenerateDefaultBinder(Type elementType)
        {
            if (elementType == typeof(int))
                return (ve, bv, index) =>
                {
                    IntegerField f = (IntegerField)ve;
                    f.SetValueWithoutNotify(bv.GetBoxedValue(index) is int v ? v : 0);
                    f.RegisterValueChangedCallback(e => bv.SetBoxedValue(e.newValue, index));
                };

            if (elementType == typeof(float))
                return (ve, bv, index) =>
                {
                    FloatField f = (FloatField)ve;
                    f.SetValueWithoutNotify(bv.GetBoxedValue(index) is float v ? v : 0f);
                    f.RegisterValueChangedCallback(e => bv.SetBoxedValue(e.newValue, index));
                };

            if (elementType == typeof(bool))
                return (ve, bv, index) =>
                {
                    Toggle f = (Toggle)ve;
                    f.SetValueWithoutNotify(bv.GetBoxedValue(index) is bool v && v);
                    f.RegisterValueChangedCallback(e => bv.SetBoxedValue(e.newValue, index));
                };

            if (elementType == typeof(string))
                return (ve, bv, index) =>
                {
                    TextField f = (TextField)ve;
                    f.SetValueWithoutNotify(bv.GetBoxedValue(index) as string ?? string.Empty);
                    f.RegisterValueChangedCallback(e => bv.SetBoxedValue(e.newValue, index));
                };

            if (elementType == typeof(Vector2))
                return (ve, bv, index) =>
                {
                    Vector2Field f = (Vector2Field)ve;
                    f.SetValueWithoutNotify(bv.GetBoxedValue(index) is Vector2 v ? v : Vector2.zero);
                    f.RegisterValueChangedCallback(e => bv.SetBoxedValue(e.newValue, index));
                };

            if (elementType == typeof(Vector3))
                return (ve, bv, index) =>
                {
                    Vector3Field f = (Vector3Field)ve;
                    f.SetValueWithoutNotify(bv.GetBoxedValue(index) is Vector3 v ? v : Vector3.zero);
                    f.RegisterValueChangedCallback(e => bv.SetBoxedValue(e.newValue, index));
                };

            if (elementType == typeof(Vector4))
                return (ve, bv, index) =>
                {
                    Vector4Field f = (Vector4Field)ve;
                    f.SetValueWithoutNotify(bv.GetBoxedValue(index) is Vector4 v ? v : Vector4.zero);
                    f.RegisterValueChangedCallback(e => bv.SetBoxedValue(e.newValue, index));
                };

            if (elementType == typeof(Color))
                return (ve, bv, index) =>
                {
                    ColorField f = (ColorField)ve;
                    f.SetValueWithoutNotify(bv.GetBoxedValue(index) is Color v ? v : Color.white);
                    f.RegisterValueChangedCallback(e => bv.SetBoxedValue(e.newValue, index));
                };

            if (elementType == typeof(Quaternion))
                return (ve, bv, index) =>
                {
                    Vector4Field f = (Vector4Field)ve;
                    Quaternion q = bv.GetBoxedValue(index) is Quaternion qv ? qv : Quaternion.identity;
                    f.SetValueWithoutNotify(new Vector4(q.x, q.y, q.z, q.w));
                    f.RegisterValueChangedCallback(e =>
                    {
                        Vector4 vec = e.newValue;
                        bv.SetBoxedValue(new Quaternion(vec.x, vec.y, vec.z, vec.w), index);
                    });
                };

            // Reference types — ObjectField implements INotifyValueChanged<UnityEngine.Object>
            if (typeof(UnityEngine.Object).IsAssignableFrom(elementType))
                return (ve, bv, index) =>
                {
                    ObjectField f = (ObjectField)ve;
                    f.SetValueWithoutNotify(bv.GetBoxedValue(index) as UnityEngine.Object);
                    f.RegisterValueChangedCallback(e => bv.SetBoxedValue(e.newValue, index));
                };

            // Unknown type — user must supply a custom binder
            Debug.LogWarning($"[VariableTypeRegistry] No default binder for type '{elementType.Name}'. " +
                "Pass a custom binder to Register(), or ensure the factory creates a VisualElement " +
                "that implements INotifyValueChanged<T> for the matching type.");
            return (ve, bv, index) => { };
        }
    }
}
