using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[UxmlElement("InspectorView")]
public partial class InspectorView : VisualElement
{
    private VisualElement _contentContainer;

    Editor editor;

    public InspectorView()
    {
        style.flexGrow = 1;
        style.paddingLeft = 8;
        style.paddingRight = 8;
        style.paddingTop = 8;
        style.paddingBottom = 8;
        style.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 1f);

        _contentContainer = new VisualElement { style = { flexGrow = 1 } };
        Add(_contentContainer);

        var placeholder = new Label("Select a node in the GraphView")
        {
            style =
            {
                color = Color.grey,
                unityTextAlign = TextAnchor.MiddleCenter,
                marginTop = 40,
                fontSize = 13
            }
        };
        _contentContainer.Add(placeholder);
    }

    public void UpdateSelection(BehaviourNodeView nodeView)
    {
        Clear();

        UnityEngine.Object.DestroyImmediate(editor);
        editor = Editor.CreateEditor(nodeView.NodeSO);
        IMGUIContainer container = new IMGUIContainer(() => {editor.OnInspectorGUI(); });
        Add(container);
    }
}