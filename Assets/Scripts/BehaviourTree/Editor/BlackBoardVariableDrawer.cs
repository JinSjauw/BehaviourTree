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

        // Use unified FieldTypeHelper instead of old BlackboardTypes
        string[] displayNames = FieldTypeHelper.AllFieldTypes
            .Select(ft => FieldTypeHelper.GetDisplayName(ft))
            .ToArray();

        // Find current index by matching typeName to the System.Type full name or assembly-qualified name
        int currentIndex = 0;
        for (int i = 0; i < FieldTypeHelper.AllFieldTypes.Count; i++)
        {
            System.Type t = FieldTypeHelper.GetSystemType(FieldTypeHelper.AllFieldTypes[i]);
            if (t.FullName == typeProp.stringValue || t.AssemblyQualifiedName == typeProp.stringValue)
            {
                currentIndex = i;
                break;
            }
        }
        currentIndex = EditorGUI.Popup(typeRect, currentIndex, displayNames);
        typeProp.stringValue = FieldTypeHelper.GetSystemType(FieldTypeHelper.AllFieldTypes[currentIndex]).FullName;
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        => EditorGUIUtility.singleLineHeight;
}