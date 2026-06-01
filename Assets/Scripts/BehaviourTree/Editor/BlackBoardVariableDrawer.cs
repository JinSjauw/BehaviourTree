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
        SerializedProperty showFoldoutProp = property.FindPropertyRelative("showInitialValue");

        float lineHeight = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;

        Rect foldoutRect = new Rect(position.x, position.y, 14, lineHeight);
        Rect nameRect = new Rect(position.x + 18, position.y, position.width * 0.40f, lineHeight);
        Rect typeRect = new Rect(position.x + 18 + position.width * 0.42f, position.y, position.width * 0.58f - 18, lineHeight);

        // Draw foldout triangle (only for value types)
        bool typeResolved = FieldTypeHelper.TryGetSystemTypeFromName(typeProp.stringValue, out Type resolvedType);
        bool isValueType = resolvedType != null && resolvedType.IsValueType && !resolvedType.IsEnum;

        if (isValueType)
        {
            showFoldoutProp.boolValue = EditorGUI.Foldout(foldoutRect, showFoldoutProp.boolValue, GUIContent.none);
        }

        nameProp.stringValue = EditorGUI.TextField(nameRect, nameProp.stringValue);

        // Placeholder text when name is empty
        if (string.IsNullOrEmpty(nameProp.stringValue))
        {
            EditorGUI.LabelField(nameRect, " Variable Name", PlaceholderStyle);
        }

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

        if (!typeResolved)
        {
            Rect warnRect = new Rect(position.x + 18, position.y + lineHeight + spacing, position.width - 18, lineHeight * 2f);
            EditorGUI.HelpBox(warnRect, "Unresolved type name. Please re-select a supported type.", MessageType.Warning);
        }

        // ── Row 1+: initial value (only if value type and foldout open) ──
        if (isValueType && showFoldoutProp.boolValue)
        {
            float valueY = position.y + lineHeight + spacing + (!typeResolved ? (lineHeight * 2f + spacing) : 0f);
            float valueHeight = lineHeight;

            // Vector fields need two rows (label + x/y or x/y/z)
            if (resolvedType == typeof(Vector2) || resolvedType == typeof(Vector3))
            {
                valueHeight = lineHeight * 2f;
            }

            Rect valueRect = new Rect(position.x + 18, valueY, position.width - 18, valueHeight);

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
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        SerializedProperty typeProp = property.FindPropertyRelative("typeName");
        SerializedProperty showFoldoutProp = property.FindPropertyRelative("showInitialValue");

        bool typeResolved = FieldTypeHelper.TryGetSystemTypeFromName(typeProp.stringValue, out Type resolvedType);
        bool isValueType = resolvedType != null && resolvedType.IsValueType && !resolvedType.IsEnum;

        float lineHeight = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;

        float warnHeight = !typeResolved ? (lineHeight * 2f + spacing) : 0f;
        if (isValueType && showFoldoutProp.boolValue)
        {
            // Vector fields are two lines tall
            if (resolvedType == typeof(Vector2) || resolvedType == typeof(Vector3))
            {
                return warnHeight + lineHeight * 3f + spacing * 3f;
            }
            else
            {
                return warnHeight + lineHeight * 2f + spacing * 3f;
            }
        }
        else
        {
            return warnHeight + lineHeight;
        }
    }
}
