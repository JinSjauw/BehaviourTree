using System;
using System.Collections.Generic;
using BehaviourTree.Core;
using BehaviourTree.Editor;
using BehaviourTree.Runtime;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[UxmlElement("TrackedVariablesView")]
public partial class TrackedVariablesView : VisualElement
{
    // ── UI elements ──────────────────────────────────────────
    private ListView bindingListView;
    private Button addBindingButton;
    private Button addBindingFromSceneButton;
    private VisualElement sceneObjectRow;
    private ObjectField sceneGameObjectField;
    private Button sceneAddButton;
    private Label emptyStateLabel;

    // ── State ────────────────────────────────────────────────
    private TreeRunner currentRunner;
    private List<TrackedBinding> activeBindings;
    private List<TrackedBinding> displayBindings = new();

    public TrackedVariablesView()
    {
        style.flexGrow = 1;

        // Load UXML
        string uxmlPath = BehaviourTreeEditorPaths.TrackedVariablesViewUxml;
        VisualTreeAsset treeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
        if (treeAsset != null)
        {
            VisualElement ui = treeAsset.CloneTree();
            Add(ui);
        }

        // Load USS
        string ussPath = BehaviourTreeEditorPaths.TrackedVariablesViewUss;
        StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(ussPath);
        if (styleSheet != null)
            styleSheets.Add(styleSheet);

        // Query elements

        bindingListView = this.Q<ListView>("binding-list-view");
        addBindingButton = this.Q<Button>("add-binding-button");
        addBindingFromSceneButton = this.Q<Button>("add-binding-from-scene-button");
        sceneObjectRow = this.Q<VisualElement>("scene-object-row");
        sceneGameObjectField = this.Q<ObjectField>("scene-gameobject-field");
        sceneGameObjectField.objectType = typeof(GameObject);
        sceneGameObjectField.allowSceneObjects = true;

        sceneAddButton = this.Q<Button>("scene-add-button");
        emptyStateLabel = this.Q<Label>("empty-state-label");

        // Configure ListView
        bindingListView.makeItem = MakeBindingRow;
        bindingListView.bindItem = BindBindingRow;
        bindingListView.itemsSource = displayBindings;

        // Wire buttons
        if (addBindingButton != null) addBindingButton.clicked += OnAddBindingClicked;
        if (addBindingFromSceneButton != null) addBindingFromSceneButton.clicked += OnAddBindingFromSceneClicked;
        if (sceneAddButton != null) sceneAddButton.clicked += OnSceneAddClicked;

        ShowEmptyState("Select a GameObject with a TreeRunner in the scene.");
    }

    // ── Public API ───────────────────────────────────────────

    public void Refresh(TreeRunner runner)
    {
        currentRunner = runner;

        if (currentRunner == null)
        {
            ShowEmptyState("Select a GameObject with a TreeRunner in the scene.");
            return;
        }

        activeBindings = ResolveActiveBindingGroup(currentRunner);
        displayBindings.Clear();
        if (activeBindings != null)
        {
            for (int i = 0; i < activeBindings.Count; i++)
                displayBindings.Add(activeBindings[i]);
        }

        bindingListView.Rebuild();

        if (displayBindings.Count == 0)
            ShowEmptyState("No tracked bindings. Click [+ Add Binding] to create one.");
        else
            HideEmptyState();
    }

    /// <summary>
    /// Finds the TrackedBindingGroup that matches the currently open tree asset.
    /// Matches by GUID first, then by direct reference (fallback for old data).
    /// </summary>
    private static List<TrackedBinding> ResolveActiveBindingGroup(TreeRunner runner)
    {
        if (runner == null || runner.trackedBindingGroups == null)
            return null;

        BehaviourTreeAssetBase currentTree = BehaviourTreeEditor.currentTree;
        string currentTreeGuid = GetAssetGuid(currentTree);

        for (int i = 0; i < runner.trackedBindingGroups.Count; i++)
        {
            TrackedBindingGroup group = runner.trackedBindingGroups[i];
            if (group == null) continue;

            // Match by GUID
            if (!string.IsNullOrEmpty(currentTreeGuid) && group.targetTreeGuid == currentTreeGuid)
                return group.bindings;

            // Fallback: match by direct reference
            if (currentTree != null && group.targetTree == currentTree)
                return group.bindings;
        }

        return null;
    }

    /// <summary>
    /// Gets or creates the TrackedBindingGroup for the currently open tree asset.
    /// Populates the GUID on creation for build-time matching.
    /// </summary>
    private static TrackedBindingGroup GetOrCreateActiveGroup(TreeRunner runner)
    {
        if (runner == null || runner.trackedBindingGroups == null)
            return null;

        BehaviourTreeAssetBase currentTree = BehaviourTreeEditor.currentTree;
        string currentTreeGuid = GetAssetGuid(currentTree);

        for (int i = 0; i < runner.trackedBindingGroups.Count; i++)
        {
            TrackedBindingGroup group = runner.trackedBindingGroups[i];
            if (group == null) continue;

            // Match by GUID
            if (!string.IsNullOrEmpty(currentTreeGuid) && group.targetTreeGuid == currentTreeGuid)
                return group;

            // Fallback: match by direct reference (also backfill GUID)
            if (currentTree != null && group.targetTree == currentTree)
            {
                if (string.IsNullOrEmpty(group.targetTreeGuid) && !string.IsNullOrEmpty(currentTreeGuid))
                    group.targetTreeGuid = currentTreeGuid;
                return group;
            }
        }

        TrackedBindingGroup newGroup = new TrackedBindingGroup
        {
            targetTree = currentTree,
            targetTreeGuid = currentTreeGuid
        };
        runner.trackedBindingGroups.Add(newGroup);
        return newGroup;
    }

    private static string GetAssetGuid(BehaviourTreeAssetBase asset)
    {
        if (asset == null) return null;
        string path = AssetDatabase.GetAssetPath(asset);
        if (string.IsNullOrEmpty(path)) return null;
        return AssetDatabase.AssetPathToGUID(path);
    }

    // ── Empty State ──────────────────────────────────────────

    private void ShowEmptyState(string message)
    {
        bindingListView.style.display = DisplayStyle.None;
        emptyStateLabel.text = message;
        emptyStateLabel.style.display = DisplayStyle.Flex;
    }

    private void HideEmptyState()
    {
        bindingListView.style.display = DisplayStyle.Flex;
        emptyStateLabel.style.display = DisplayStyle.None;
    }

    // ── Add Binding ──────────────────────────────────────────

    private void OnAddBindingClicked()
    {
        if (currentRunner == null) return;

        List<GameObject> gameObjects = GetSourceGameObjects();
        ComponentMemberSearchProvider provider = ScriptableObject.CreateInstance<ComponentMemberSearchProvider>();
        provider.Initialize(gameObjects, info => { AddBinding(info); });

        Rect buttonRect = addBindingButton.worldBound;
        SearchWindow.Open(new SearchWindowContext(GUIUtility.GUIToScreenPoint(buttonRect.position)), provider);
    }

    private void OnAddBindingFromSceneClicked()
    {
        sceneObjectRow.style.display = sceneObjectRow.style.display == DisplayStyle.None
            ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void OnSceneAddClicked()
    {
        if (currentRunner == null) return;

        GameObject sceneGO = sceneGameObjectField?.value as GameObject;
        if (sceneGO == null) return;

        List<GameObject> gameObjects = new List<GameObject> { sceneGO };

        ComponentMemberSearchProvider provider = ScriptableObject.CreateInstance<ComponentMemberSearchProvider>();
        provider.Initialize(gameObjects, info => { AddBinding(info); });

        Rect buttonRect = sceneAddButton.worldBound;
        SearchWindow.Open(new SearchWindowContext(GUIUtility.GUIToScreenPoint(buttonRect.position)), provider);
    }

    private void AddBinding(SelectedMemberInfo info)
    {
        if (currentRunner == null) return;

        Undo.RecordObject(currentRunner, "Add Tracked Binding");

        TrackedBinding binding = new TrackedBinding
        {
            targetComponent = info.TargetComponent,
            memberName = info.MemberName,
            isProperty = info.IsProperty,
            memberTypeName = info.MemberType?.AssemblyQualifiedName,
            blackboardVariableName = string.Empty
        };

        TrackedBindingGroup group = GetOrCreateActiveGroup(currentRunner);
        group.bindings.Add(binding);
        EditorUtility.SetDirty(currentRunner);
        Refresh(currentRunner);
    }

    // ── Remove Binding ───────────────────────────────────────

    private void RemoveBinding(TrackedBinding binding)
    {
        if (currentRunner == null || activeBindings == null) return;

        Undo.RecordObject(currentRunner, "Remove Tracked Binding");
        activeBindings.Remove(binding);
        EditorUtility.SetDirty(currentRunner);
        Refresh(currentRunner);
    }

    // ── Variable Selection ───────────────────────────────────

    private void OpenVariablePicker(TrackedBinding binding)
    {
        BlackboardDefinition blackboardDefinition = BehaviourTreeEditor.currentBlackboardDef;
        if (blackboardDefinition == null) return;

        Type filterType = ResolveTrackedBindingMemberType(binding);

        VariableSearchPopup popup = new VariableSearchPopup(blackboardDefinition, filterType, selectedVariable =>
        {
            if (currentRunner == null) return;

            Undo.RecordObject(currentRunner, "Set Tracked Binding Variable");
            binding.blackboardVariableName = selectedVariable.Name;
            EditorUtility.SetDirty(currentRunner);
            Refresh(currentRunner);
        });

        VisualElement target = GetVariableButtonForBinding(binding);
        Rect popupRect = target != null ? target.worldBound : new Rect(Vector2.zero, Vector2.zero);
        UnityEditor.PopupWindow.Show(popupRect, popup);
    }

    private static Type ResolveTrackedBindingMemberType(TrackedBinding binding)
    {
        if (string.IsNullOrEmpty(binding.memberTypeName))
            return null;
        return Type.GetType(binding.memberTypeName);
    }

    // ── Open Member Picker for existing binding ──────────────

    private void OpenMemberPicker(TrackedBinding binding)
    {
        if (currentRunner == null) return;

        List<GameObject> gameObjects = GetSourceGameObjects();
        ComponentMemberSearchProvider provider = ScriptableObject.CreateInstance<ComponentMemberSearchProvider>();
        provider.Initialize(gameObjects, info =>
        {
            if (currentRunner == null) return;

            Undo.RecordObject(currentRunner, "Edit Tracked Binding");
            binding.targetComponent = info.TargetComponent;
            binding.memberName = info.MemberName;
            binding.isProperty = info.IsProperty;
            binding.memberTypeName = info.MemberType?.AssemblyQualifiedName;
            EditorUtility.SetDirty(currentRunner);
            Refresh(currentRunner);
        });

        VisualElement target = GetMemberButtonForBinding(binding);
        Rect buttonRect = target != null ? target.worldBound : new Rect(Vector2.zero, Vector2.zero);
        SearchWindow.Open(new SearchWindowContext(GUIUtility.GUIToScreenPoint(buttonRect.position)), provider);
    }

    // ── Button reference helpers ─────────────────────────────

    private VisualElement GetMemberButtonForBinding(TrackedBinding binding)
    {
        int index = displayBindings.IndexOf(binding);
        if (index < 0) return null;
        VisualElement row = bindingListView.GetRootElementForIndex(index);
        return row?.Q<Button>("component-member-button");
    }

    private VisualElement GetVariableButtonForBinding(TrackedBinding binding)
    {
        int index = displayBindings.IndexOf(binding);
        if (index < 0) return null;
        VisualElement row = bindingListView.GetRootElementForIndex(index);
        return row?.Q<Button>("variable-button");
    }

    // ── Source GameObjects ───────────────────────────────────

    private List<GameObject> GetSourceGameObjects()
    {
        List<GameObject> result = new List<GameObject>();
        if (currentRunner == null) return result;

        result.Add(currentRunner.gameObject);

        Transform runnerTransform = currentRunner.transform;
        for (int i = 0; i < runnerTransform.childCount; i++)
            result.Add(runnerTransform.GetChild(i).gameObject);


        return result;
    }

    // ── ListView Row Construction ────────────────────────────

    private static VisualTreeAsset bindingRowTemplate;

    private VisualElement MakeBindingRow()
    {
        if (bindingRowTemplate == null)
            bindingRowTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(BehaviourTreeEditorPaths.TrackedBindingRowUxml);
        return bindingRowTemplate.CloneTree();
    }

    private void BindBindingRow(VisualElement element, int index)
    {
        if (index < 0 || index >= displayBindings.Count) return;

        TrackedBinding binding = displayBindings[index];

        Label numLabel = element.Q<Label>("row-number");
        if (numLabel != null)
            numLabel.text = (index + 1).ToString();

        Button memberButton = element.Q<Button>("component-member-button");
        if (memberButton != null)
        {
            string componentDisplay = binding.targetComponent != null
                ? binding.targetComponent.GetType().Name : "(select)";
            string memberDisplay = !string.IsNullOrEmpty(binding.memberName)
                ? binding.memberName : "(select)";
            memberButton.text = $"{componentDisplay}.{memberDisplay}";
            memberButton.clickable = new Clickable(() => OpenMemberPicker(binding));
        }

        Button variableButton = element.Q<Button>("variable-button");
        if (variableButton != null)
        {
            variableButton.text = !string.IsNullOrEmpty(binding.blackboardVariableName)
                ? binding.blackboardVariableName : "(select)";
            variableButton.clickable = new Clickable(() => OpenVariablePicker(binding));
        }

        Button removeButton = element.Q<Button>("remove-button");
        if (removeButton != null)
            removeButton.clickable = new Clickable(() => RemoveBinding(binding));
    }
}
