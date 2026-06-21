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

    /// <summary>
    /// Current scroll offset inside the inspector's BeginScrollView.
    /// Exposed so IMGUI editors (e.g. CustomNodeEditor) can correct GetLastRect()
    /// coordinates that are in scroll-content space.
    /// </summary>
    public static Vector2 InspectorScrollOffset { get; private set; }

    private VisualElement inspectorViewContainer;
    private IMGUIContainer cachedInspectorContainer;

    Editor editor;
    private bool isReadOnly;
    private BehaviourNodeView currentNodeView;
    private Vector2 inspectorScrollPos;

    public InspectorView()
    {
        style.flexGrow = 1;
        style.paddingLeft = 8;
        style.paddingRight = 8;
        style.paddingTop = 8;
        style.paddingBottom = 8;
        style.backgroundColor = GraphEditorTheme.instance.panelBg;

        // ── Separator ────────────────────────────────────────────────
        VisualElement separator = new VisualElement
        {
            name = "InspectorSeparator",
            style =
            {
                height = 1,
                backgroundColor = GraphEditorTheme.instance.panelSeparator,
                marginTop = 6,
                marginBottom = 6,
                flexShrink = 0
            }
        };
        Add(separator);

        // ── Header ───────────────────────────────────────────────────
        Label header = new Label("Inspector")
        {
            name = "InspectorTitle",
            style =
            {
                fontSize = 14,
                unityFontStyleAndWeight = FontStyle.Bold,
                marginBottom = 6,
                marginTop = 4
            }
        };
        Add(header);

        inspectorViewContainer = new VisualElement { style = { flexGrow = 1 } };
        Add(inspectorViewContainer);

        cachedInspectorContainer = new IMGUIContainer(() =>
        {
            if (editor != null && editor.target)
            {
                // Capture scroll offset for popup anchoring in IMGUI editors
                InspectorScrollOffset = inspectorScrollPos;

                if (isReadOnly) EditorGUI.BeginDisabledGroup(true);
                IsRenderingReadOnly = isReadOnly;
                CurrentProxyMappings = currentNodeView?.VariableMappings;
                inspectorScrollPos = EditorGUILayout.BeginScrollView(inspectorScrollPos);
                editor.OnInspectorGUI();
                EditorGUILayout.EndScrollView();

                if (editor is CustomNodeEditor customEditor && customEditor.nodeNameChangedThisFrame)
                {
                    currentNodeView?.RefreshTitle();
                    customEditor.nodeNameChangedThisFrame = false;
                }

                if (editor is CustomNodeEditor customEditor2 && customEditor2.nodeVisualsChangedThisFrame)
                {
                    currentNodeView?.GraphView?.RefreshAllNodeIcons();
                    customEditor2.nodeVisualsChangedThisFrame = false;
                }

                IsRenderingReadOnly = false;
                CurrentProxyMappings = null;
                InspectorScrollOffset = Vector2.zero;
                if (isReadOnly) EditorGUI.EndDisabledGroup();
            }
        });

        var placeholder = new Label("Select a node in the GraphView")
        {
            style =
        {
            color = GraphEditorTheme.instance.panelPlaceholder,
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
        currentNodeView = null;

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

        currentNodeView = nodeView;
        isReadOnly = nodeView.IsReadOnlyProxy;

        inspectorViewContainer.Clear();

        editor = Editor.CreateEditor(nodeView.NodeSO);

        inspectorViewContainer.Add(cachedInspectorContainer);
    }
}
