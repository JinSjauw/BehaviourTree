using System;
using System.Linq;
using BehaviourTree.Core;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(BlackboardVariable))]
public class BlackboardVariableDrawer : PropertyDrawer
{
    private static GUIStyle cachedPlaceholderStyle;
    private static string[] cachedDisplayNames;

    private const float StrideLabelWidth = 14f;
    private const float StrideFieldWidth = 30f;

    private static GUIStyle PlaceholderStyle
    {
        get
        {
            if (cachedPlaceholderStyle == null)
            {
                cachedPlaceholderStyle = new GUIStyle(EditorStyles.label)
                {
                    fontStyle = FontStyle.Italic,
                    normal = { textColor = Color.gray }
                };
            }
            return cachedPlaceholderStyle;
        }
    }

    private static string[] DisplayNames
    {
        get
        {
            if (cachedDisplayNames == null)
                cachedDisplayNames = FieldTypeHelper.AllFieldTypes.Select(ft => FieldTypeHelper.GetDisplayName(ft)).ToArray();
            return cachedDisplayNames;
        }
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        SerializedProperty nameProp = property.FindPropertyRelative("name");
        SerializedProperty typeProp = property.FindPropertyRelative("typeName");
        SerializedProperty strideProp = property.FindPropertyRelative("stride");
        SerializedProperty showFoldoutProp = property.FindPropertyRelative("showInitialValue");

        float lineHeight = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;

        // ── Row 0 layout ──
        Rect foldoutRect = new Rect(position.x, position.y, 14, lineHeight);
        float strideWidth = StrideLabelWidth + StrideFieldWidth + 4f;
        float typeWidth = (position.width - 18 - strideWidth) * 0.55f;
        float nameWidth = (position.width - 18 - strideWidth) * 0.45f;

        Rect nameRect = new Rect(position.x + 18, position.y, nameWidth, lineHeight);
        Rect typeRect = new Rect(position.x + 18 + nameWidth + 4f, position.y, typeWidth, lineHeight);
        Rect strideLabelRect = new Rect(typeRect.xMax + 4f, position.y, StrideLabelWidth, lineHeight);
        Rect strideRect = new Rect(strideLabelRect.xMax, position.y, StrideFieldWidth, lineHeight);

        // ── Foldout triangle (only for value types) ──
        bool typeResolved = FieldTypeHelper.TryGetSystemTypeFromName(typeProp.stringValue, out Type resolvedType);
        bool isValueType = resolvedType != null && resolvedType.IsValueType && !resolvedType.IsEnum;

        if (isValueType)
            showFoldoutProp.boolValue = EditorGUI.Foldout(foldoutRect, showFoldoutProp.boolValue, GUIContent.none);

        // ── Name field ──
        nameProp.stringValue = EditorGUI.TextField(nameRect, nameProp.stringValue);
        if (string.IsNullOrEmpty(nameProp.stringValue))
            EditorGUI.LabelField(nameRect, " Variable Name", PlaceholderStyle);

        // ── Type dropdown ──
        int currentIndex = 0;
        bool typeMatched = false;
        for (int i = 0; i < FieldTypeHelper.AllFieldTypes.Count; i++)
        {
            Type type = FieldTypeHelper.GetSystemType(FieldTypeHelper.AllFieldTypes[i]);
            if (type.FullName == typeProp.stringValue || type.AssemblyQualifiedName == typeProp.stringValue)
            {
                currentIndex = i;
                typeMatched = true;
                break;
            }
        }
        EditorGUI.BeginChangeCheck();
        int nextIndex = EditorGUI.Popup(typeRect, currentIndex, DisplayNames);
        if (EditorGUI.EndChangeCheck() || !typeMatched)
        {
            typeProp.stringValue = FieldTypeHelper.GetSystemType(FieldTypeHelper.AllFieldTypes[nextIndex]).FullName;
            typeResolved = FieldTypeHelper.TryGetSystemTypeFromName(typeProp.stringValue, out resolvedType);
            isValueType = resolvedType != null && resolvedType.IsValueType && !resolvedType.IsEnum;
        }

        // ── Stride field ──
        EditorGUI.LabelField(strideLabelRect, "N");
        EditorGUI.BeginChangeCheck();
        int newStride = EditorGUI.IntField(strideRect, strideProp.intValue);
        if (EditorGUI.EndChangeCheck())
        {
            strideProp.intValue = Mathf.Max(1, newStride);
        }

        // ── Unresolved type warning ──
        if (!typeResolved)
        {
            Rect warnRect = new Rect(position.x + 18, position.y + lineHeight + spacing, position.width - 18, lineHeight * 2f);
            EditorGUI.HelpBox(warnRect, "Unresolved type name. Please re-select a supported type.", MessageType.Warning);
        }

        // ── Row 1+: initial value ──
        if (isValueType && showFoldoutProp.boolValue)
        {
            float valueY = position.y + lineHeight + spacing + (!typeResolved ? (lineHeight * 2f + spacing) : 0f);
            int effectiveStride = Mathf.Max(1, strideProp.intValue);

            if (effectiveStride == 1)
            {
                DrawSingleInitialValue(position, valueY, resolvedType, property);
            }
            else
            {
                DrawArrayInitialValues(position, valueY, effectiveStride, resolvedType, property);
            }
        }
    }

    private void DrawSingleInitialValue(Rect position, float y, Type resolvedType, SerializedProperty property)
    {
        float lineHeight = EditorGUIUtility.singleLineHeight;
        float valueHeight = lineHeight;
        if (resolvedType == typeof(Vector2) || resolvedType == typeof(Vector3))
            valueHeight = lineHeight * 2f;

        Rect valueRect = new Rect(position.x + 18, y, position.width - 18, valueHeight);

        if (resolvedType == typeof(int))
        {
            var prop = property.FindPropertyRelative("intValue");
            prop.intValue = EditorGUI.IntField(valueRect, "Initial Value", prop.intValue);
        }
        else if (resolvedType == typeof(float))
        {
            var prop = property.FindPropertyRelative("floatValue");
            prop.floatValue = EditorGUI.FloatField(valueRect, "Initial Value", prop.floatValue);
        }
        else if (resolvedType == typeof(bool))
        {
            var prop = property.FindPropertyRelative("boolValue");
            prop.boolValue = EditorGUI.Toggle(valueRect, "Initial Value", prop.boolValue);
        }
        else if (resolvedType == typeof(Vector2))
        {
            var prop = property.FindPropertyRelative("vector2Value");
            prop.vector2Value = EditorGUI.Vector2Field(valueRect, "Initial Value", prop.vector2Value);
        }
        else if (resolvedType == typeof(Vector3))
        {
            var prop = property.FindPropertyRelative("vector3Value");
            prop.vector3Value = EditorGUI.Vector3Field(valueRect, "Initial Value", prop.vector3Value);
        }
    }

    private void DrawArrayInitialValues(Rect position, float startY, int stride, Type resolvedType, SerializedProperty property)
    {
        float lineHeight = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;
        float elemWidth = position.width - 18;

        string arrayFieldName = GetArrayFieldName(resolvedType);
        if (arrayFieldName == null) return;

        SerializedProperty arrayProp = property.FindPropertyRelative(arrayFieldName);

        // Ensure array size matches stride
        while (arrayProp.arraySize < stride)
            arrayProp.InsertArrayElementAtIndex(arrayProp.arraySize);
        while (arrayProp.arraySize > stride)
            arrayProp.DeleteArrayElementAtIndex(arrayProp.arraySize - 1);

        for (int i = 0; i < stride; i++)
        {
            float y = startY + i * (lineHeight + spacing);
            float x = position.x + 18;

            Rect labelRect = new Rect(x, y, 24, lineHeight);
            Rect fieldRect = new Rect(x + 24, y, elemWidth - 24, lineHeight);

            EditorGUI.LabelField(labelRect, $"[{i}]", EditorStyles.miniLabel);

            SerializedProperty elemProp = arrayProp.GetArrayElementAtIndex(i);
            DrawArrayElementField(fieldRect, resolvedType, elemProp);
        }
    }

    private static string GetArrayFieldName(Type resolvedType)
    {
        if (resolvedType == typeof(int))      return "initialIntValues";
        if (resolvedType == typeof(float))    return "initialFloatValues";
        if (resolvedType == typeof(bool))     return "initialBoolValues";
        if (resolvedType == typeof(Vector2))  return "initialVector2Values";
        if (resolvedType == typeof(Vector3))  return "initialVector3Values";
        return null;
    }

    private void DrawArrayElementField(Rect rect, Type resolvedType, SerializedProperty elemProp)
    {
        if (resolvedType == typeof(int))
            elemProp.intValue = EditorGUI.IntField(rect, elemProp.intValue);
        else if (resolvedType == typeof(float))
            elemProp.floatValue = EditorGUI.FloatField(rect, elemProp.floatValue);
        else if (resolvedType == typeof(bool))
            elemProp.boolValue = EditorGUI.Toggle(rect, elemProp.boolValue);
        else if (resolvedType == typeof(Vector2))
            elemProp.vector2Value = EditorGUI.Vector2Field(rect, GUIContent.none, elemProp.vector2Value);
        else if (resolvedType == typeof(Vector3))
            elemProp.vector3Value = EditorGUI.Vector3Field(rect, GUIContent.none, elemProp.vector3Value);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        SerializedProperty typeProp = property.FindPropertyRelative("typeName");
        SerializedProperty showFoldoutProp = property.FindPropertyRelative("showInitialValue");
        SerializedProperty strideProp = property.FindPropertyRelative("stride");

        bool typeResolved = FieldTypeHelper.TryGetSystemTypeFromName(typeProp.stringValue, out Type resolvedType);
        bool isValueType = resolvedType != null && resolvedType.IsValueType && !resolvedType.IsEnum;

        float lineHeight = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;

        float warnHeight = !typeResolved ? (lineHeight * 2f + spacing) : 0f;

        if (isValueType && showFoldoutProp.boolValue)
        {
            int effectiveStride = Mathf.Max(1, strideProp.intValue);

            if (effectiveStride == 1)
            {
                float valueHeight = (resolvedType == typeof(Vector2) || resolvedType == typeof(Vector3))
                    ? lineHeight * 2f
                    : lineHeight;
                return warnHeight + lineHeight + spacing + valueHeight + spacing;
            }
            else
            {
                float arrayHeight = effectiveStride * lineHeight + (effectiveStride - 1) * spacing;
                return warnHeight + lineHeight + spacing + arrayHeight + spacing;
            }
        }

        return warnHeight + lineHeight;
    }
}