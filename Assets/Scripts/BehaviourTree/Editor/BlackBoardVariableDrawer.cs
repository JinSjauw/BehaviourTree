using System.Linq;
using BehaviourTree.Core;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(BlackboardVariable))]
public class BlackboardVariableDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var nameProp = property.FindPropertyRelative("name");
        var typeProp = property.FindPropertyRelative("typeName");

        float lineHeight = EditorGUIUtility.singleLineHeight;
        Rect nameRect = new Rect(position.x, position.y, position.width * 0.45f, lineHeight);
        Rect typeRect = new Rect(position.x + position.width * 0.47f, position.y, position.width * 0.53f, lineHeight);

        nameProp.stringValue = EditorGUI.TextField(nameRect, nameProp.stringValue);

        string[] displayNames = BlackboardTypes.AllowedTypeNames.Select(t => t.displayName).ToArray();
        int currentIndex = System.Array.FindIndex(BlackboardTypes.AllowedTypeNames, t => t.typeName == typeProp.stringValue);
        if (currentIndex < 0) currentIndex = 0;

        currentIndex = EditorGUI.Popup(typeRect, currentIndex, displayNames);
        typeProp.stringValue = BlackboardTypes.AllowedTypeNames[currentIndex].typeName;
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        => EditorGUIUtility.singleLineHeight;
}