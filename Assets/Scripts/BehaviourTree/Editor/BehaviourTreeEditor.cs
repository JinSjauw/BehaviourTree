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
    private TrackedVariablesView trackedVariablesView;
    private SquadTabView squadTabView;
    private CommanderTabView commanderTabView;
    private ToolbarMenu assetBarMenu;
    private TabView tabView;
    private Tab inspectorTab;
    private Tab commanderTabInstance;
    private TreeSearchProvider treeSearchProvider;

    public static BlackboardDefinition currentBlackboardDef { get; private set; }
    public static BaseEditorTreeAsset currentTree { get; private set; }
    public static BehaviourTreeRunnerBase currentRunner { get; private set; }

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
        if (Selection.activeObject is BaseEditorTreeAsset)
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
        trackedVariablesView = root.Q<TrackedVariablesView>();
        squadTabView = root.Q<SquadTabView>();
        assetBarMenu = root.Q<ToolbarMenu>("AssetBarMenu");
        tabView = root.Q<TabView>("TabView");
        inspectorTab = tabView?.Q<Tab>("InspectorTab");
        commanderTabView = new CommanderTabView();
        commanderTabInstance = new Tab("Commander") { name = "CommanderTab", style = { flexGrow = 1 } };
        commanderTabInstance.Add(commanderTabView);

        if (treeGraphView == null)
        {
            Debug.LogError("Could not find BehaviourTreeEditorGraphView in UXML");
        }

        if (inspectorView == null)
        {
            Debug.LogError("Could not find InspectorView in UXML");
        }

        if (blackBoardView == null)
        {
            Debug.LogError("Could not find BlackBoardView in UXML");
        }

        if (assetBarMenu == null)
        {
            Debug.LogError("Could not find AssetBarMenu in UXML");
        }
        else
        {
            BuildAssetBarMenu();
        }

        OnSelectionChange();

        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

        if (EditorApplication.isPlaying)
        {
            EditorApplication.update -= PollDebugState;
            EditorApplication.update += PollDebugState;
        }
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
        if (currentTree == null) return;
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

        // Populate Open Tree submenu with recent AgentTreeAsset/CommanderTreeAsset files (last modified, limited to 15)
        const int maxRecentEntries = 5;
        string[] guids = AssetDatabase.FindAssets("t:AgentTreeAsset t:CommanderTreeAsset");

        var recentTrees = guids
            .Select(guid => new
            {
                Path = AssetDatabase.GUIDToAssetPath(guid),
                Asset = AssetDatabase.LoadAssetAtPath<BaseEditorTreeAsset>(AssetDatabase.GUIDToAssetPath(guid))
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
            BaseEditorTreeAsset capturedAsset = entry.Asset;
            menu.AppendAction("Open Tree/" + capturedAsset.name, _ =>
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

        // ── Squad entries ──
        menu.AppendSeparator();
        {
            string[] squadGuids = AssetDatabase.FindAssets("t:SquadDefinition");
            System.Collections.Generic.List<SquadDefinition> recentSquads = squadGuids
                .Select(g => AssetDatabase.LoadAssetAtPath<SquadDefinition>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(s => s != null)
                .OrderByDescending(s => File.GetLastWriteTime(AssetDatabase.GetAssetPath(s)))
                .Take(5)
                .ToList();

            foreach (SquadDefinition squad in recentSquads)
            {
                SquadDefinition captured = squad;
                menu.AppendAction("Open Squad/" + captured.name, _ => OpenSquadEditor(captured));
            }
            menu.AppendSeparator("Open Squad/");
            menu.AppendAction("Open Squad/Browse...", BrowseOpenSquad);
            menu.AppendAction("Create New Squad", CreateNewSquad);
        }
    }

    private static void OpenSquadEditor(SquadDefinition squad)
    {
        SquadDefinitionEditor wnd = GetWindow<SquadDefinitionEditor>();
        wnd.titleContent = new GUIContent("Squad Editor");
        wnd.LoadSquad(squad);
    }

    private void BrowseOpenSquad(DropdownMenuAction action)
    {
        SquadDefinitionEditor wnd = GetWindow<SquadDefinitionEditor>();
        wnd.titleContent = new GUIContent("Squad Editor");

        string path = EditorUtility.OpenFilePanel("Open Squad Definition", "Assets", "asset");
        if (string.IsNullOrEmpty(path)) return;

        string projectRelative = "Assets" + path.Replace("\\", "/")
            .Replace(Application.dataPath.Replace("\\", "/"), "");

        SquadDefinition squad = AssetDatabase.LoadAssetAtPath<SquadDefinition>(projectRelative);
        if (squad != null)
            wnd.LoadSquad(squad);
    }

    private void CreateNewSquad(DropdownMenuAction action)
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Create Squad Definition", "NewSquad", "asset",
            "Create a new SquadDefinition");

        if (string.IsNullOrEmpty(path)) return;

        SquadDefinition squad = CreateInstance<SquadDefinition>();
        squad.name = System.IO.Path.GetFileNameWithoutExtension(path);
        AssetDatabase.CreateAsset(squad, path);
        EditorUtility.SetDirty(squad);
        AssetDatabase.SaveAssets();

        OpenSquadEditor(squad);
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
        string path = EditorUtility.SaveFilePanelInProject("Create Agent Tree", "NewTree", "asset", "Create a new AgentTreeAsset");
        if (string.IsNullOrEmpty(path)) return;

        AgentTreeAsset treeAsset = CreateInstance<AgentTreeAsset>();
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
        if (currentTree == null) return;
        EditorUtility.SetDirty(currentTree);
        AssetDatabase.SaveAssets();
    }

    private void PollDebugState()
    {
        treeGraphView?.RefreshDebugVisuals(currentRunner);
    }

    private BaseEditorTreeAsset OnSelectTree()
    {
        GameObject selected = Selection.activeGameObject;

        if (selected != null && selected.TryGetComponent(out BehaviourTreeRunnerBase runner))
        {
            currentRunner = runner;
            trackedVariablesView?.Refresh(runner);

            return runner.GetSourceTree() as BaseEditorTreeAsset;
        }
        else
        {
            trackedVariablesView?.Refresh(null);
            return Selection.activeObject as BaseEditorTreeAsset;
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
        if (currentTree == null || currentBlackboardDef == null) return;

        Debug.Log("Baking Tree!");

        RuntimeBehaviourTreeAsset runtimeAsset = CreateInstance<RuntimeBehaviourTreeAsset>();
        runtimeAsset.name = currentTree.name + "_Runtime";
        runtimeAsset.sourceTree = currentTree;

        runtimeAsset.blackboardDefinition = TreeBaker.BakeTree(currentTree.root, currentTree,
        ref runtimeAsset.runtimeNodeData,
        ref runtimeAsset.runtimeFieldData,
        ref runtimeAsset.fieldTypeNames,
        ref runtimeAsset.boxedConstants,
        ref runtimeAsset.runtimeNodeGuids,
        out runtimeAsset.maxTreeDepth);

        string assetPath = $"Assets/{runtimeAsset.name}.asset";
        AssetDatabase.CreateAsset(runtimeAsset, assetPath);
        if (runtimeAsset.blackboardDefinition != null)
        {
            runtimeAsset.blackboardDefinition.name = runtimeAsset.name + "_BB_Definition";
            AssetDatabase.AddObjectToAsset(runtimeAsset.blackboardDefinition, runtimeAsset);
        }
        AssetDatabase.SaveAssets();
    }

    private void OnSelectionChange()
    {
        BaseEditorTreeAsset selectedAsset = OnSelectTree();

        if (selectedAsset == null) return;

        // Reset proxy flag when a different tree is selected
        if (selectedAsset != currentTree && treeGraphView != null) treeGraphView.DebugProxiesAreSetup = false;

        if (selectedAsset == currentTree)
        {
            // Reconfigure tabs — runner type may have changed (agent ↔ commander)
            // without the tree asset changing
            ConfigureTabsForTreeType();

            // If the runner is stale (destroyed, deselected, or scene changed),
            // fall through to repopulate and reset state.
            bool runnerIsStale = currentRunner == null;

            // In play mode, allow repopulation to set up debug proxies
            bool needsPlayModeRepop = EditorApplication.isPlaying
                && treeGraphView != null
                && !treeGraphView.DebugProxiesAreSetup;

            if (!runnerIsStale && !needsPlayModeRepop)
                return;
        }

        currentTree = selectedAsset;

        // Refresh tracked variables view — the binding group depends on currentTree
        if (selectedAsset != null)
        {
            trackedVariablesView?.Refresh(currentRunner);
            squadTabView?.Refresh(currentTree);
            commanderTabView?.Refresh(currentTree);
        }

        // Null check for tree asset before using it
        if (currentTree == null)
        {
            currentBlackboardDef = null;
            treeGraphView?.ClearView();
            return;
        }

        if (!AssetDatabase.CanOpenAssetInEditor(currentTree.GetEntityId()))
        {
            return;
        }

        if (currentTree.blackboardDefinition == null && currentTree.CommanderBlackboardDefinition == null)
        {
            currentTree.CreateBlackBoard();
        }

        currentBlackboardDef = null;
        currentBlackboardDef = currentTree?.blackboardDefinition;

        // Configure tabs based on tree type
        ConfigureTabsForTreeType();

        // Null check for graph view before using it
        if (treeGraphView != null)
        {
            try
            {
                inspectorView?.ClearView();
                treeGraphView.OnNodeSelected = OnNodeSelectionChanged;
                treeGraphView.PopulateView(currentTree);
                if (blackBoardView != null)
                    blackBoardView.IsSquadContext = currentTree is CommanderTreeAsset;
                BlackboardDefinition bbDef = currentTree is CommanderTreeAsset
                    ? currentTree.CommanderBlackboardDefinition
                    : currentTree.blackboardDefinition;
                blackBoardView.BuildBlackboardView(bbDef);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error populating view: {ex.Message}");
            }
        }
    }

    private void ConfigureTabsForTreeType()
    {
        if (tabView == null || commanderTabInstance == null) return;

        // Commander tab visible if:
        // - Runner is CommanderTreeRunner (play mode), OR
        // - Tree asset is CommanderTreeAsset (editor mode, no runner selected)
        bool isCommander = currentRunner is CommanderTreeRunner
            || (currentRunner == null && currentTree is CommanderTreeAsset);
        bool tabInView = commanderTabInstance.parent == tabView;

        if (isCommander && !tabInView)
            tabView.Add(commanderTabInstance);
        else if (!isCommander && tabInView)
            tabView.Remove(commanderTabInstance);
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
