using System;
using BehaviourTree.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[UxmlElement("BlackBoardView")]
public partial class BlackBoardView : VisualElement
{
    private VisualElement blackBoardViewContainer;
    private SerializedObject cachedSerializedObject;
    private BlackboardDefinition cachedDefinition;

    private Vector2 scrollPos;

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
        cachedDefinition = blackboardDefinition;
        blackBoardViewContainer.Clear();

        if (cachedSerializedObject == null || cachedSerializedObject.targetObject != blackboardDefinition)
        {
            cachedSerializedObject?.Dispose();
            cachedSerializedObject = blackboardDefinition != null ? new SerializedObject(blackboardDefinition) : null;
        }

        IMGUIContainer imgui = new IMGUIContainer(() =>
        {
            SerializedObject so = cachedSerializedObject;
            if (so == null) return;
            so.Update();
            SerializedProperty varsProp = so.FindProperty("sharedVariables");

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            EditorGUILayout.PropertyField(varsProp, includeChildren: true);
            EditorGUILayout.EndScrollView();

            so.ApplyModifiedProperties();
        });

        blackBoardViewContainer.Add(imgui);
    }
}