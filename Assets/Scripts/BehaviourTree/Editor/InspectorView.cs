using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using BehaviourTree.Editor;

[UxmlElement("InspectorView")]
public partial class InspectorView : VisualElement
{
    public static bool IsRenderingReadOnly { get; private set; }
    public static Dictionary<string, string> CurrentProxyMappings { get; private set; }

    private VisualElement inspectorViewContainer;

    Editor editor;

    public InspectorView()
    {
        style.flexGrow = 1;
        style.paddingLeft = 8;
        style.paddingRight = 8;
        style.paddingTop = 8;
        style.paddingBottom = 8;
        style.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 1f);

        inspectorViewContainer = new VisualElement { style = { flexGrow = 1 } };
        Add(inspectorViewContainer);

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
        inspectorViewContainer.Add(placeholder);
    }

    public void ClearView()
    {
        if (editor != null)
        {
            UnityEngine.Object.DestroyImmediate(editor);
            editor = null;
        }

        inspectorViewContainer.Clear();

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
        inspectorViewContainer.Add(placeholder);
    }

    public void UpdateSelection(BehaviourNodeView nodeView)
    {
        if(editor != null)
        {
            UnityEngine.Object.DestroyImmediate(editor);
            editor = null;
        }
        
        inspectorViewContainer.Clear();

        editor = Editor.CreateEditor(nodeView.NodeSO);

        bool readOnly = nodeView.IsReadOnlyProxy;

        IMGUIContainer container = new IMGUIContainer(() =>
        {
            if (editor.target)
            {
                if (readOnly) EditorGUI.BeginDisabledGroup(true);
                IsRenderingReadOnly = readOnly;
                CurrentProxyMappings = nodeView.VariableMappings;
                editor.OnInspectorGUI();
                IsRenderingReadOnly = false;
                CurrentProxyMappings = null;
                if (readOnly) EditorGUI.EndDisabledGroup();
            }
        });

        inspectorViewContainer.Add(container);
    }
}

