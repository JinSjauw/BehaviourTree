using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using BehaviourTree.Core;
using BehaviourTree.Editor;
using BehaviourTree.Runtime;
using System;
using System.Linq;
using System.IO;

public class BehaviourTreeEditor : EditorWindow
{
    private BehaviourTreeEditorGraphView treeGraphView;
    
    private InspectorView inspectorView;
    private BlackBoardView blackBoardView;
    private ToolbarMenu assetBarMenu;
    private TabView tabView;
    private Tab inspectorTab;
    private TreeSearchProvider treeSearchProvider;

    public static BlackboardDefinition currentBlackboardDef { get; private set; }
    public static BehaviourTreeAsset currentTree { get; private set; }
    public static TreeRunner currentRunner { get; private set; }

    [MenuItem("BehaviourTree/Open Behaviour Tree Graph", priority = 29)]
    public static void OpenWindow()
    {
        currentTree = null;
        currentBlackboardDef = null;
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
        VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(BehaviourTreeEditorPaths.EditorUxml);
        
        // Null check for UXML asset
        if (visualTree == null)
        {
            Debug.LogError("Failed to load BehaviourTreeEditor.uxml");
            return;
        }

        VisualElement root = visualTree.CloneTree();
        root.style.flexGrow = 1; // Fix the thin strip

        StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(BehaviourTreeEditorPaths.EditorUss);

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
        tabView = root.Q<TabView>("TabView");
        inspectorTab = tabView?.Q<Tab>("InspectorTab");

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
            Debug.LogError("Could not find AssetBarMenu in UXML");
        }
        else
        {
            BuildAssetBarMenu();
        }

        OnSelectionChange();

        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private void OnPlayModeStateChanged(PlayModeStateChange change)
    {
        switch (change)
        {
            case PlayModeStateChange.EnteredPlayMode:
                EditorApplication.update -= PollDebugState;
                EditorApplication.update += PollDebugState;
                break;
            case PlayModeStateChange.ExitingPlayMode:
                EditorApplication.update -= PollDebugState;
                treeGraphView?.ClearRuntimeDebugProxies();
                break;
            case PlayModeStateChange.EnteredEditMode:
                OnSelectionChange();
                break;
        }
    }

    private void SyncTree(DropdownMenuAction action)
    {
        if(currentTree == null) return;
        currentTree.SyncNodesListFromAssets();
    }

    private void OnEnable()
    {
        EditorApplication.projectChanged += OnProjectChanged;
    }

    private void BuildAssetBarMenu()
    {
        if (assetBarMenu == null) return;

        var menu = assetBarMenu.menu;
        menu.ClearItems();

        menu.AppendAction("Create New Tree", CreateNewTree);
        menu.AppendSeparator();

        // Populate Open Tree submenu with recent BehaviourTreeAsset files (last modified, limited to 15)
        const int maxRecentEntries = 5;
        string[] guids = AssetDatabase.FindAssets("t:BehaviourTreeAsset");

        var recentTrees = guids
            .Select(guid => new
            {
                Path = AssetDatabase.GUIDToAssetPath(guid),
                Asset = AssetDatabase.LoadAssetAtPath<BehaviourTreeAsset>(AssetDatabase.GUIDToAssetPath(guid))
            })
            .Where(t => t.Asset != null)
            .Select(t => new
            {
                t.Asset,
                LastWrite = File.GetLastWriteTime(t.Path)
            })
            .OrderByDescending(t => t.LastWrite)
            .Take(maxRecentEntries);

        foreach (var entry in recentTrees)
        {
            BehaviourTreeAsset capturedAsset = entry.Asset;
            menu.AppendAction("Open Tree/" + capturedAsset.name, a =>
            {
                Selection.activeObject = capturedAsset;
                AssetDatabase.OpenAsset(capturedAsset);
            });
        }

        menu.AppendSeparator("Open Tree/");
        menu.AppendAction("Open Tree/Browse...", BrowseOpenTree);

        menu.AppendSeparator();
        menu.AppendAction("Bake Tree", BakeTree);
        menu.AppendAction("Save Tree", SaveTree);
        menu.AppendAction("Sync Tree", SyncTree);
    }

    private void BrowseOpenTree(DropdownMenuAction action)
    {
        if (treeSearchProvider == null)
            treeSearchProvider = ScriptableObject.CreateInstance<TreeSearchProvider>();

        Rect bounds = assetBarMenu.worldBound;
        Vector2 screenPos = new Vector2(position.x + bounds.x + bounds.width, position.y + bounds.y + bounds.height);
        SearchWindow.Open(new SearchWindowContext(screenPos), treeSearchProvider);
    }

    private void CreateNewTree(DropdownMenuAction action)
    {
        string path = EditorUtility.SaveFilePanelInProject("Create Behaviour Tree", "NewTree", "asset", "Create a new BehaviourTreeAsset");
        if (string.IsNullOrEmpty(path)) return;

        BehaviourTreeAsset treeAsset = CreateInstance<BehaviourTreeAsset>();
        treeAsset.name = System.IO.Path.GetFileNameWithoutExtension(path);
        AssetDatabase.CreateAsset(treeAsset, path);

        treeAsset.nodesList = new System.Collections.Generic.List<BehaviourNode>();
        treeAsset.CreateBlackBoard();
        EditorUtility.SetDirty(treeAsset);
        AssetDatabase.SaveAssets();

        Selection.activeObject = treeAsset;
        BuildAssetBarMenu();
        OnSelectionChange();
    }

    private void SaveTree(DropdownMenuAction action)
    {
        if(currentTree == null) return;
        EditorUtility.SetDirty(currentTree);
        AssetDatabase.SaveAssets();
    }

    private void PollDebugState()
    {
        treeGraphView?.RefreshDebugVisuals(currentRunner);
    }

    private BehaviourTreeAsset OnSelectTree()
    {
        GameObject selected = Selection.activeGameObject;

        if(selected != null && selected.TryGetComponent(out TreeRunner runner))
        {
            currentRunner = runner;

            return runner.GetSourceTree() as BehaviourTreeAsset;
        }
        else
        {
            return Selection.activeObject as BehaviourTreeAsset;
        }
    }

    private void OnDisable()
    {
        EditorApplication.projectChanged -= OnProjectChanged;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.update -= PollDebugState;

        ClearGraph();
    }

    private void BakeTree(DropdownMenuAction dropdownMenuAction)
    {
        if(currentTree == null || currentBlackboardDef == null) return;

        Debug.Log("Baking Tree!");

        RuntimeBehaviourTreeAsset runtimeAsset = CreateInstance<RuntimeBehaviourTreeAsset>();
        runtimeAsset.name = currentTree.name + "_Runtime";
        runtimeAsset.sourceTree = currentTree;

        runtimeAsset.blackboardDefinition = TreeBaker.BakeTree(currentTree.root, currentBlackboardDef, 
        ref runtimeAsset.runtimeNodeData, 
        ref runtimeAsset.runtimeFieldData, 
        ref runtimeAsset.runtimeNodeGuids,
        out runtimeAsset.maxTreeDepth);

        string path = $"Assets/{runtimeAsset.name}.asset";
        AssetDatabase.CreateAsset(runtimeAsset, path);
        if (runtimeAsset.blackboardDefinition != null)
        {
            runtimeAsset.blackboardDefinition.name = runtimeAsset.name + "_BB_Definition";
            AssetDatabase.AddObjectToAsset(runtimeAsset.blackboardDefinition, runtimeAsset);
        }
        AssetDatabase.SaveAssets();
    }

    private void OnSelectionChange()
    {
        BehaviourTreeAsset selectedAsset = OnSelectTree();

        if(selectedAsset == null) return;

        if (selectedAsset == currentTree) return;

        currentTree = selectedAsset;
        
        // Null check for tree asset before using it
        if (currentTree == null)
        {
            currentBlackboardDef = null;
            treeGraphView?.ClearView();
            return;
        }
        
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
                inspectorView?.ClearView();
                treeGraphView.OnNodeSelected = OnNodeSelectionChanged;
                treeGraphView.PopulateView(currentTree);
                blackBoardView.BuildBlackboardView(currentTree.blackboardDefinition);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error populating view: {ex.Message}");
            }
        }
    }

    private void OnProjectChanged()
    {
        BuildAssetBarMenu();

        if (currentTree == null) return;

        if (!AssetDatabase.Contains(currentTree))
        {
            currentTree = null;
            currentBlackboardDef = null;
            treeGraphView?.ClearView();
            return;
        }

        treeGraphView?.RefreshTitle();
    }

    private void OnNodeSelectionChanged(BehaviourNodeView nodeView)
    {
        inspectorView.UpdateSelection(nodeView);
        if (tabView != null && inspectorTab != null)
            tabView.activeTab = inspectorTab;
    }

    private void ClearGraph()
    {
        currentTree = null;
        currentBlackboardDef = null;

        if (treeGraphView != null)
        {
            treeGraphView.OnNodeSelected = null;
            treeGraphView.ClearView();
        }

        if (treeSearchProvider != null)
        {
            DestroyImmediate(treeSearchProvider);
            treeSearchProvider = null;
        }
    }
}

