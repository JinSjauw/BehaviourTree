using System;
using BehaviourTree.Core;
using UnityEditor;
using UnityEngine;

namespace BehaviourTree.Editor
{
    [CustomPropertyDrawer(typeof(BlackboardVariableBase), true)]
    public class BlackBoardVariableDrawer : UnityEditor.PropertyDrawer
    {
        private static readonly Type[] CommonTypes = FieldTypeHelper.CommonTypes;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty nameProp = property.FindPropertyRelative("variableName");
            SerializedProperty strideProp = property.FindPropertyRelative("variableStride");
            SerializedProperty singleValueProp = property.FindPropertyRelative("singleValue");
            SerializedProperty arrayValuesProp = property.FindPropertyRelative("arrayValues");

            if (nameProp == null)
            {
                EditorGUI.LabelField(position, "Element", property.managedReferenceValue?.GetType().Name ?? "(null)");
                return;
            }

            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = 2f;
            Rect r = new Rect(position.x, position.y, position.width, lineHeight);

            BlackboardVariableBase bv = property.managedReferenceValue as BlackboardVariableBase;
            Type currentType = bv?.GetValueType();
            int stride = strideProp?.intValue ?? 1;
            bool isValueType = currentType != null && currentType.IsValueType;

            float savedLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 44f;

            // ── Row 1: Name ───────────────────────────────────────────
            EditorGUI.PropertyField(r, nameProp, new GUIContent("Name"));
            r.y += lineHeight + spacing;

            // ── Row 2: Type dropdown [│ Stride if array] ─────────────
            if (stride > 1)
            {
                float strideWidth = 44f;
                Rect typeRect = new Rect(r.x, r.y, r.width - strideWidth - 4f, r.height);
                Rect strideRect = new Rect(r.x + typeRect.width + 4f, r.y, strideWidth, r.height);

                int currentTypeIndex = GetCommonTypeIndex(currentType);
                int newTypeIndex = EditorGUI.Popup(typeRect, "Type", currentTypeIndex, GetTypeDisplayNames());
                if (newTypeIndex != currentTypeIndex && newTypeIndex >= 0 && newTypeIndex < CommonTypes.Length)
                {
                    ChangeType(property, bv, CommonTypes[newTypeIndex]);
                    EditorGUIUtility.labelWidth = savedLabelWidth;
                    return;
                }

                EditorGUI.BeginChangeCheck();
                int newStride = EditorGUI.IntField(strideRect, stride);
                if (EditorGUI.EndChangeCheck() && strideProp != null)
                {
                    newStride = Mathf.Max(1, newStride);
                    strideProp.intValue = newStride;
                    if (bv != null)
                        bv.Stride = newStride;
                    property.serializedObject.ApplyModifiedProperties();
                }
            }
            else
            {
                Rect typeRect = new Rect(r.x, r.y, r.width, r.height);
                int currentTypeIndex = GetCommonTypeIndex(currentType);
                int newTypeIndex = EditorGUI.Popup(typeRect, "Type", currentTypeIndex, GetTypeDisplayNames());
                if (newTypeIndex != currentTypeIndex && newTypeIndex >= 0 && newTypeIndex < CommonTypes.Length)
                {
                    ChangeType(property, bv, CommonTypes[newTypeIndex]);
                    EditorGUIUtility.labelWidth = savedLabelWidth;
                    return;
                }
            }
            r.y += lineHeight + spacing;

            // ── Row 3: Value / Stride header ─────────────────────────
            if (stride > 1)
            {
                // Array mode: show "Values" label, editor fields follow below
                EditorGUI.LabelField(r, "Values", $"[{stride}]");
                r.y += lineHeight + spacing;

                // Ensure array size matches stride
                if (arrayValuesProp != null)
                {
                    while (arrayValuesProp.arraySize < stride)
                        arrayValuesProp.InsertArrayElementAtIndex(arrayValuesProp.arraySize);
                    while (arrayValuesProp.arraySize > stride)
                        arrayValuesProp.DeleteArrayElementAtIndex(arrayValuesProp.arraySize - 1);

                    for (int i = 0; i < stride; i++)
                    {
                        SerializedProperty elementProp = arrayValuesProp.GetArrayElementAtIndex(i);
                        Rect elementRect = new Rect(r.x, r.y, r.width, r.height);
                        DrawValueField(elementRect, $"[{i}]", elementProp, currentType);
                        r.y += lineHeight + spacing;
                    }
                }
            }
            else if (isValueType && singleValueProp != null)
            {
                DrawValueField(r, "Value", singleValueProp, currentType);
            }
            else if (!isValueType && singleValueProp != null)
            {
                // Reference type — show ObjectField
                EditorGUI.PropertyField(r, singleValueProp, new GUIContent("Value"));
            }

            EditorGUIUtility.labelWidth = savedLabelWidth;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            SerializedProperty nameProp = property.FindPropertyRelative("variableName");
            if (nameProp == null)
                return EditorGUIUtility.singleLineHeight + 2f;

            SerializedProperty strideProp = property.FindPropertyRelative("variableStride");
            int stride = strideProp?.intValue ?? 1;
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = 2f;

            // 3 rows (name, type+stride, value/header) + array elements
            float height = (lineHeight + spacing) * 3;
            if (stride > 1)
                height += (lineHeight + spacing) * stride;

            return height;
        }

        private static void DrawValueField(Rect r, string label, SerializedProperty prop, Type type)
        {
            if (prop == null || type == null) return;

            float savedLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 40f;

            if (type == typeof(int) || type == typeof(uint))
                prop.intValue = EditorGUI.IntField(r, label, prop.intValue);
            else if (type == typeof(float))
                prop.floatValue = EditorGUI.FloatField(r, label, prop.floatValue);
            else if (type == typeof(bool))
                prop.boolValue = EditorGUI.Toggle(r, label, prop.boolValue);
            else if (type == typeof(Vector2))
                prop.vector2Value = EditorGUI.Vector2Field(r, label, prop.vector2Value);
            else if (type == typeof(Vector3))
                prop.vector3Value = EditorGUI.Vector3Field(r, label, prop.vector3Value);
            else if (type == typeof(Vector4))
                prop.vector4Value = EditorGUI.Vector4Field(r, label, prop.vector4Value);
            else if (type == typeof(Color))
                prop.colorValue = EditorGUI.ColorField(r, label, prop.colorValue);
            else if (type == typeof(Quaternion))
            {
                Vector4 v = new Vector4(prop.quaternionValue.x, prop.quaternionValue.y, prop.quaternionValue.z, prop.quaternionValue.w);
                v = EditorGUI.Vector4Field(r, label, v);
                prop.quaternionValue = new Quaternion(v.x, v.y, v.z, v.w);
            }
            else if (type == typeof(string))
                prop.stringValue = EditorGUI.TextField(r, label, prop.stringValue);
            else
                EditorGUI.LabelField(r, label, $"(type: {type.Name})");

            EditorGUIUtility.labelWidth = savedLabelWidth;
        }

        private static void ChangeType(SerializedProperty property, BlackboardVariableBase oldVar, Type newType)
        {
            if (oldVar == null) return;

            string oldName = oldVar.Name;
            int oldStride = oldVar.Stride;

            Type sharedVarType = typeof(BlackboardVariable<>).MakeGenericType(newType);
            BlackboardVariableBase newVar = (BlackboardVariableBase)Activator.CreateInstance(sharedVarType);
            newVar.Name = oldName;
            newVar.Stride = oldStride;

            property.managedReferenceValue = newVar;
            property.serializedObject.ApplyModifiedProperties();
        }

        private static int GetCommonTypeIndex(Type type)
        {
            if (type == null) return -1;
            for (int i = 0; i < CommonTypes.Length; i++)
            {
                if (CommonTypes[i] == type)
                    return i;
            }
            return -1;
        }

        private static string[] GetTypeDisplayNames()
        {
            string[] names = new string[CommonTypes.Length];
            for (int i = 0; i < CommonTypes.Length; i++)
                names[i] = FieldTypeHelper.GetDisplayName(CommonTypes[i]);
            return names;
        }
    }
}
