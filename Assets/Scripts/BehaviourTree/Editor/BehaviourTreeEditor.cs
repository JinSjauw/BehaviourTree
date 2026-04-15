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
        
        // Each editor window contains a root VisualElement object
        VisualElement root = visualTree.CloneTree();
        root.style.flexGrow = 1; // 👈 Fix the thin strip

        var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Scripts/BehaviourTree/Editor/BehaviourTreeEditor.uss");
        root.styleSheets.Add(styleSheet);

        rootVisualElement.Add(root);

        //// Instantiate UXML
        //VisualElement labelFromUXML = m_VisualTreeAsset.Instantiate();
        //root.Add(labelFromUXML);
        treeGraphView = root.Q<BehaviourTreeEditorGraphView>();
        inspectorView = root.Q<InspectorView>();

        OnSelectionChange();
    }

    private void OnSelectionChange()
    {
        BehaviourTreeAsset tree = Selection.activeObject as BehaviourTreeAsset;
        if (tree) 
        {
            treeGraphView.PopulateView(tree);
        }
    }
}
