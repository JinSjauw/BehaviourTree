using BehaviourTree;
using BehaviourTree.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class BehaviourTreeEditor : EditorWindow
{
    BehaviourTreeEditorGraphView treeGraphView;
    
    InspectorView inspectorView;

    [MenuItem("BehaviourTree/BTNodeGraph")]
    public static void OpenWindow()
    {
        BehaviourTreeEditor wnd = GetWindow<BehaviourTreeEditor>();
        wnd.titleContent = new GUIContent("BehaviourTreeEditor");
    }

    public void CreateGUI()
    {
        VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Scripts/BehaviourTree/Editor/BehaviourTreeEditor.uxml");
        
        // Null check for UXML asset
        if (visualTree == null)
        {
            Debug.LogError("Failed to load BehaviourTreeEditor.uxml");
            return;
        }

        VisualElement root = visualTree.CloneTree();
        root.style.flexGrow = 1; // Fix the thin strip

        var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Scripts/BehaviourTree/Editor/BehaviourTreeEditor.uss");

        // Null check for USS stylesheet
        if (styleSheet != null)
        {
            root.styleSheets.Add(styleSheet);
        }
        else
        {
            Debug.LogWarning("Failed to load BehaviourTreeEditor.uss");
        }

        rootVisualElement.Add(root);

        treeGraphView = root.Q<BehaviourTreeEditorGraphView>();
        inspectorView = root.Q<InspectorView>();

        if (treeGraphView == null)
        {
            Debug.LogError("Could not find BehaviourTreeEditorGraphView in UXML");
        }

        if (inspectorView == null)
        {
            Debug.LogError("Could not find InspectorView in UXML");
        }

        OnSelectionChange();
    }

    private void OnSelectionChange()
    {
        BehaviourTreeAsset tree = Selection.activeObject as BehaviourTreeAsset;

        // Null check for tree asset before using it
        if (tree == null) return;

        if(!AssetDatabase.CanOpenAssetInEditor(tree.GetEntityId()))
        {
            return;
        }

        // Null check for graph view before using it
        if (treeGraphView != null)
        {
            try
            {
                treeGraphView.PopulateView(tree);
                treeGraphView.OnNodeSelected = OnNodeSelectionChanged;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error populating view: {ex.Message}");
            }
        }
    }

    private void OnNodeSelectionChanged(BehaviourNodeView nodeView)
    {
        inspectorView.UpdateSelection(nodeView);
    }

    private void OnDestroy()
    {
        if (treeGraphView != null)
        {
            treeGraphView.OnNodeSelected = null;
        }
    }
}

