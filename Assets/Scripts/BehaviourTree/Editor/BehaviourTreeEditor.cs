using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using BehaviourTree.Core;
using BehaviourTree.Editor;
using BehaviourTree.Runtime;

public class BehaviourTreeEditor : EditorWindow
{
    private BehaviourTreeEditorGraphView treeGraphView;
    
    private InspectorView inspectorView;
    private BlackBoardView blackBoardView;
    private ToolbarMenu assetBarMenu;

    public static BlackboardDefinition currentBlackboardDef { get; private set; }
    public static BehaviourTreeAsset currentTree { get; private set; }

    [MenuItem("BehaviourTree/BTNodeGraph")]
    public static void OpenWindow()
    {
        BehaviourTreeEditor wnd = GetWindow<BehaviourTreeEditor>();
        wnd.titleContent = new GUIContent("Behaviour Tree Editor");
    }

    [OnOpenAsset]
    public static bool OnOpenAsset(int instanceID, int line)
    {
        if(Selection.activeObject is BehaviourTreeAsset)
        {
            OpenWindow();
            return true;
        }
        return false;
    }

    public void CreateGUI()
    {
        VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Scripts/BehaviourTree/Editor/UIDocuments/BehaviourTreeEditor.uxml");
        
        // Null check for UXML asset
        if (visualTree == null)
        {
            Debug.LogError("Failed to load BehaviourTreeEditor.uxml");
            return;
        }

        VisualElement root = visualTree.CloneTree();
        root.style.flexGrow = 1; // Fix the thin strip

        var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Scripts/BehaviourTree/Editor/UIDocuments/BehaviourTreeEditor.uss");

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
        blackBoardView = root.Q<BlackBoardView>();
        assetBarMenu = root.Q<ToolbarMenu>("AssetBarMenu");

        if (treeGraphView == null)
        {
            Debug.LogError("Could not find BehaviourTreeEditorGraphView in UXML");
        }

        if (inspectorView == null)
        {
            Debug.LogError("Could not find InspectorView in UXML");
        }

        if(blackBoardView == null)
        {
            Debug.LogError("Could not find BlackBoardView in UXML");
        }

        if(assetBarMenu == null)
        {
            Debug.LogError("Could not find BlackBoardView in UXML");
        }
        else
        {
            assetBarMenu.menu.AppendAction("Bake Tree", BakeTree);    
        }

        OnSelectionChange();
    }

    private void BakeTree(DropdownMenuAction dropdownMenuAction)
    {
        if(currentTree == null || currentBlackboardDef == null) return;

        Debug.Log("Baking Tree!");

        RuntimeBTreeAsset runtimeAsset = CreateInstance<RuntimeBTreeAsset>();
        runtimeAsset.name = currentTree.name + "_Runtime";
        runtimeAsset.blackboardDefinition = currentBlackboardDef;

        TreeBaker.BakeTree(currentTree.rootCopy, currentBlackboardDef, ref runtimeAsset.runtimeNodeData, ref runtimeAsset.runtimeFieldData);

        string path = $"Assets/{runtimeAsset.name}.asset";
        AssetDatabase.CreateAsset(runtimeAsset, path);
        AssetDatabase.SaveAssets();
    }

    private void OnSelectionChange()
    {
        //currentBlackBoardDefinition = null;

        currentTree = Selection.activeObject as BehaviourTreeAsset;
        // Null check for tree asset before using it
        if (currentTree == null) return;
        
        if(!AssetDatabase.CanOpenAssetInEditor(currentTree.GetEntityId()))
        {
            return;
        }

        if(currentTree.blackboardDefinition == null)
        {
            currentTree.CreateBlackBoard();
        }

        currentBlackboardDef = null;
        currentBlackboardDef = currentTree?.blackboardDefinition;

        // Null check for graph view before using it
        if (treeGraphView != null)
        {
            try
            {
                treeGraphView.OnNodeSelected = OnNodeSelectionChanged;
                treeGraphView.PopulateView(currentTree);
                blackBoardView.BuildBlackboardView(currentTree.blackboardDefinition);
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
        currentTree = null;
        currentBlackboardDef = null;

        if (treeGraphView != null)
        {
            treeGraphView.OnNodeSelected = null;
        }
    }
}

