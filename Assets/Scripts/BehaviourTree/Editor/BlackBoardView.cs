using System;
using BehaviourTree.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[UxmlElement("BlackBoardView")]
public partial class BlackBoardView : VisualElement
{
    private VisualElement blackBoardViewContainer;

    Editor editor;

    public BlackBoardView()
    {
        style.flexGrow = 1;
        style.paddingLeft = 8;
        style.paddingRight = 8;
        style.paddingTop = 8;
        style.paddingBottom = 8;
        style.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 1f);

        blackBoardViewContainer = new VisualElement { style = { flexGrow = 1 } };
        Add(blackBoardViewContainer);

        var placeholder = new Label("Add a blackboard definition")
        {
            style =
            {
                color = Color.grey,
                unityTextAlign = TextAnchor.MiddleCenter,
                marginTop = 40,
                fontSize = 13
            }
        };
        blackBoardViewContainer.Add(placeholder);
    }

    public void BuildBlackboardView(BlackboardDefinition blackboardDefinition)
    {
        blackBoardViewContainer.Clear();
        // Use an IMGUIContainer to draw the serialized property
        IMGUIContainer imgui = new IMGUIContainer(() =>
        {
            if (blackboardDefinition == null) return;
            SerializedObject so = new SerializedObject(blackboardDefinition);
            so.Update();
            SerializedProperty varsProp = so.FindProperty("variables");
            EditorGUILayout.PropertyField(varsProp, includeChildren: true);
            so.ApplyModifiedProperties();
        });

        blackBoardViewContainer.Add(imgui);
    }
}