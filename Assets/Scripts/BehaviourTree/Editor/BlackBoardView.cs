using System;
using System.Collections.Generic;
using System.Linq;
using BehaviourTree.Core;
using BehaviourTree.Editor;
using BehaviourTree.Editor.Propagation;
using BehaviourTree.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[UxmlElement("BlackBoardView")]
public partial class BlackBoardView : VisualElement
{
    private VariableChangePropagator propagator;
    private VisualElement blackBoardViewContainer;
    private BlackboardDefinition cachedDefinition;
    private HashSet<string> previousVariableNames = new();
    private Dictionary<string, string> previousVariableTypes = new();
    private bool typeSyncDone;

    // Creator fields
    private TextField creatorNameField;
    private Button creatorButton;
    private VisualElement creatorRow;

    // ListView fields
    private ListView variableListView;
    private VisualTreeAsset entryTemplate;
    private VisualTreeAsset arrayElementTemplate;
    private VisualTreeAsset creatorTemplate;
    private StyleSheet entryStyleSheet;

    /// <summary>
    /// Tracks callback references per visual element so they can be properly
    /// unregistered on unbind. Prevents stale-lambda accumulation when the
    /// ListView reorders/recycles items.
    /// </summary>
    private sealed class CallbackHandles
    {
        public EventCallback<FocusOutEvent> nameFocusOutCallback;
        public EventCallback<KeyDownEvent> nameKeyCallback;
        public EventCallback<ChangeEvent<string>> typeCallback;
        public EventCallback<ChangeEvent<int>> strideCallback;
        public Action deleteAction;
    }

    private readonly Dictionary<VisualElement, CallbackHandles> boundCallbacks = new();

    public BlackBoardView()
    {
        style.flexGrow = 1;

        blackBoardViewContainer = new VisualElement { style = { flexGrow = 1 } };
        Add(blackBoardViewContainer);

        Label placeholder = new Label("Add a blackboard definition")
        {
            style =
            {
                color = GraphEditorTheme.instance.panelPlaceholder,
                unityTextAlign = TextAnchor.MiddleCenter,
                marginTop = 40,
                fontSize = 13
            }
        };
        blackBoardViewContainer.Add(placeholder);
    }

    /// <summary>
    /// When true, the type-creation popup shows a SquadData toggle that creates
    /// dynamically-resized array variables. Set by commander/squad editors.
    /// </summary>
    public bool IsSquadContext { get; set; }

    public void BuildBlackboardView(BlackboardDefinition blackboardDefinition)
    {
        cachedDefinition = blackboardDefinition;
        previousVariableNames.Clear();
        previousVariableTypes.Clear();
        typeSyncDone = false;
        blackBoardViewContainer.Clear();

        // Filter legacy null holes from [SerializeReference] list
        if (cachedDefinition?.sharedVariables != null)
            cachedDefinition.sharedVariables.RemoveAll(v => v == null);

        // ── Build UI from template ────────────────────────────────
        BuildCreatorUI();  // always load template (has ListView + creator + separator)

        // Hide creator section when no definition is loaded
        Label creatorHeader = blackBoardViewContainer.Q<Label>("creator-header");
        if (creatorHeader != null)
            creatorHeader.visible = cachedDefinition != null;
        if (creatorRow != null)
            creatorRow.visible = cachedDefinition != null;
        VisualElement separator = blackBoardViewContainer.Q<VisualElement>("separator");
        if (separator != null)
            separator.visible = cachedDefinition != null;

        // ── ListView configuration ────────────────────────────────
        LoadEntryTemplate();

        variableListView = blackBoardViewContainer.Q<ListView>("variable-list-view");
        if (variableListView != null)
        {
            variableListView.reorderable = true;
            variableListView.reorderMode = ListViewReorderMode.Animated;
            variableListView.showBorder = true;
            variableListView.showFoldoutHeader = false;
            variableListView.showAddRemoveFooter = false;
            variableListView.selectionType = SelectionType.None;
            variableListView.virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight;
            variableListView.fixedItemHeight = 24;
            variableListView.itemsSource = cachedDefinition?.sharedVariables;
            variableListView.makeItem = () => entryTemplate.CloneTree();
            variableListView.bindItem = BindVariableListItem;
            variableListView.unbindItem = UnbindVariableListItem;
            variableListView.itemIndexChanged += OnVariableItemIndexChanged;
        }

        // Periodic rename/type-change detection
        schedule.Execute(() =>
        {
            HandleRenames();
            HandleTypeChanges();
        }).Every(100);

        // Undo/redo — rebuild ListView and re-run propagation
        Undo.undoRedoPerformed -= OnUndoRedoPerformed;
        Undo.undoRedoPerformed += OnUndoRedoPerformed;
        RegisterCallback<DetachFromPanelEvent>(OnDetach);
    }

    private void LoadEntryTemplate()
    {
        if (entryTemplate == null)
            entryTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(BehaviourTreeEditorPaths.BlackboardVariableEntryUxml);
        if (arrayElementTemplate == null)
            arrayElementTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(BehaviourTreeEditorPaths.ArrayElementRowUxml);
        if (creatorTemplate == null)
            creatorTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(BehaviourTreeEditorPaths.BlackboardCreatorUxml);
        if (entryStyleSheet == null)
            entryStyleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(BehaviourTreeEditorPaths.BlackboardVariableEntryUss);
    }

    // ── ListView bind/unbind/reorder ─────────────────────────────────

    private void BindVariableListItem(VisualElement variableEntry, int index)
    {
        if (cachedDefinition?.sharedVariables == null) return;
        if (index < 0 || index >= cachedDefinition.sharedVariables.Count) return;

        BlackboardVariableBase variable = cachedDefinition.sharedVariables[index];
        if (variable == null) return;
        
        Type currentType = variable.GetValueType();
        int stride = variable.Stride;

        VisualElement entryContainer = variableEntry.Q<VisualElement>("entry-row");
        TextField nameField = variableEntry.Q<TextField>("name-field");
        DropdownField typeDropdown = variableEntry.Q<DropdownField>("type-dropdown");
        VisualElement strideContainer = variableEntry.Q<VisualElement>("stride-container");
        IntegerField strideField = variableEntry.Q<IntegerField>("stride-field");
        VisualElement valueCell = variableEntry.Q<VisualElement>("value-cell");
        Button deleteButton = variableEntry.Q<Button>("delete-button");

        // ── Name ────────────────────────────────────────
        nameField.SetValueWithoutNotify(variable.Name);

        if (variable.isSystemVariable)
        {
            nameField.SetEnabled(false);
            entryContainer.style.backgroundColor = GraphEditorTheme.instance.systemVariableRow;
        }
        else if (variable.isSquadData)
        {
            entryContainer.style.backgroundColor = GraphEditorTheme.instance.squadDataRow;
        }

        CallbackHandles handles = new();

        handles.nameFocusOutCallback = evt =>
        {
            string newName = nameField.value?.Trim();
            if (!string.IsNullOrEmpty(newName) && newName != variable.Name)
            {
                newName = MakeUniqueName(newName, index);
                Undo.RecordObject(cachedDefinition, "Rename Variable");
                variable.Name = newName;
                nameField.SetValueWithoutNotify(newName);
                EditorUtility.SetDirty(cachedDefinition);
                HandleRenames();
            }
            else if (string.IsNullOrEmpty(newName))
            {
                nameField.SetValueWithoutNotify(variable.Name);
            }
        };
        nameField.RegisterCallback(handles.nameFocusOutCallback);

        handles.nameKeyCallback = evt =>
        {
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                nameField.Blur();
        };
        nameField.RegisterCallback(handles.nameKeyCallback);

        // ── Type dropdown ───────────────────────────────
        List<Type> types = VariableTypeRegistry.Types.ToList();
        typeDropdown.choices = types.Select(type => FieldTypeHelper.GetDisplayName(type)).ToList();
        int typeIndex = -1;
        if (currentType != null)
        {
            for (int i = 0; i < types.Count; i++)
            {
                if (types[i] == currentType) { typeIndex = i; break; }
            }
        }
        typeDropdown.index = typeIndex;

        if (variable.isSystemVariable || variable.isSquadData)
            typeDropdown.SetEnabled(false);

        typeDropdown.RegisterValueChangedCallback(handles.typeCallback = evt =>
        {
            int newIndex = types.FindIndex(t => FieldTypeHelper.GetDisplayName(t) == evt.newValue);
            if (newIndex >= 0 && types[newIndex] != currentType)
                ChangeVariableType(variable, index, types[newIndex]);
        });

        // ── Stride ──────────────────────────────────────
        if (variable.IsArray)
        {
            strideContainer.style.display = DisplayStyle.Flex;
            strideField.SetValueWithoutNotify(stride);

            if (variable.isSystemVariable)
                strideField.SetEnabled(false);

            strideField.RegisterValueChangedCallback(handles.strideCallback = evt =>
            {
                int newStride = Mathf.Max(1, evt.newValue);
                variable.Stride = newStride;
                variable.EnsureArraySize();
                EditorUtility.SetDirty(cachedDefinition);
                variableListView.RefreshItem(index);
            });
        }
        else
        {
            strideContainer.style.display = DisplayStyle.None;
        }

        // ── Value cell ──────────────────────────────────
        valueCell.Clear();

        // SquadData stride is runtime-managed — no editor values to show
        if (variable.isSquadData || variable.isSystemVariable)
        {
            // value cell intentionally left empty for SquadData variables
        }
        else if (currentType != null
            && VariableTypeRegistry.TryGetFieldFactory(currentType, out Func<VisualElement> factory)
            && VariableTypeRegistry.TryGetBinder(currentType, out Action<VisualElement, BlackboardVariableBase, int> binder))
        {
            if (stride <= 1)
            {
                VisualElement editor = factory();
                binder(editor, variable, 0);
                editor.style.flexGrow = 1;
                valueCell.Add(editor);
            }
            else
            {
                string typeName = currentType != null ? FieldTypeHelper.GetDisplayName(currentType) : "?";
                Foldout foldout = new Foldout
                {
                    text = $"{typeName}[{stride}]",
                    value = false
                };

                for (int elementIndex = 0; elementIndex < stride; elementIndex++)
                {
                    VisualElement row = arrayElementTemplate.CloneTree();
                    row.Q<Label>("element-index").text = $"[{elementIndex}]";
                    VisualElement editorCell = row.Q<VisualElement>("element-editor");
                    VisualElement editor = factory();
                    editor.style.flexGrow = 1;
                    binder(editor, variable, elementIndex);
                    editorCell.Add(editor);
                    foldout.Add(row);
                }
                valueCell.Add(foldout);
            }
        }
        else
        {
            valueCell.Add(new Label($"(no editor for {currentType?.Name ?? "null"})")
            {
                style = { color = GraphEditorTheme.instance.panelPlaceholder }
            });
        }

        // ── Delete ──────────────────────────────────────
        if (variable.isSystemVariable)
        {
            if(deleteButton != null && deleteButton.visible) deleteButton.visible = false;
        }
        else
        {
            handles.deleteAction = () =>
            {
                Undo.RecordObject(cachedDefinition, "Remove Variable");
                cachedDefinition.sharedVariables.RemoveAt(index);
                EditorUtility.SetDirty(cachedDefinition);
                variableListView.Rebuild();
            };
            deleteButton.clicked += handles.deleteAction;
        }

        boundCallbacks[variableEntry] = handles;

        // ── Apply USS ───────────────────────────────────
        if (entryStyleSheet != null && !variableEntry.styleSheets.Contains(entryStyleSheet))
            variableEntry.styleSheets.Add(entryStyleSheet);
    }

    /// <summary>
    /// Returns a version of <paramref name="name"/> that is unique among
    /// <c>cachedDefinition.sharedVariables</c>, excluding the entry at
    /// <paramref name="excludeIndex"/>. Appends "1", "2", etc. as needed.
    /// </summary>
    private string MakeUniqueName(string name, int excludeIndex)
    {
        if (cachedDefinition?.sharedVariables == null)
            return name;

        string candidate = name;
        int suffix = 1;
        while (true)
        {
            bool conflict = false;
            for (int i = 0; i < cachedDefinition.sharedVariables.Count; i++)
            {
                if (i == excludeIndex) continue;
                BlackboardVariableBase other = cachedDefinition.sharedVariables[i];
                if (other != null && other.Name == candidate)
                {
                    conflict = true;
                    break;
                }
            }
            if (!conflict) return candidate;
            candidate = name + suffix;
            suffix++;
        }
    }

    private void UnbindVariableListItem(VisualElement ve, int index)
    {
        VisualElement valueCell = ve.Q<VisualElement>("value-cell");
        valueCell?.Clear();

        if (boundCallbacks.TryGetValue(ve, out CallbackHandles handles))
        {
            TextField nameField = ve.Q<TextField>("name-field");
            DropdownField typeDropdown = ve.Q<DropdownField>("type-dropdown");
            IntegerField strideField = ve.Q<IntegerField>("stride-field");
            Button deleteButton = ve.Q<Button>("delete-button");

            if (nameField != null)
            {
                if (handles.nameFocusOutCallback != null)
                    nameField.UnregisterCallback(handles.nameFocusOutCallback);
                if (handles.nameKeyCallback != null)
                    nameField.UnregisterCallback(handles.nameKeyCallback);
            }
            if (typeDropdown != null && handles.typeCallback != null)
                typeDropdown.UnregisterValueChangedCallback(handles.typeCallback);
            if (strideField != null && handles.strideCallback != null)
                strideField.UnregisterValueChangedCallback(handles.strideCallback);
            if (deleteButton != null && handles.deleteAction != null)
                deleteButton.clicked -= handles.deleteAction;

            boundCallbacks.Remove(ve);
        }

        // Reset mutable UI state to defaults so stale values don't bleed through
        TextField nameFieldReset = ve.Q<TextField>("name-field");
        if (nameFieldReset != null)
            nameFieldReset.SetEnabled(true);

        DropdownField typeDropdownReset = ve.Q<DropdownField>("type-dropdown");
        if (typeDropdownReset != null)
            typeDropdownReset.SetEnabled(true);

        IntegerField strideFieldReset = ve.Q<IntegerField>("stride-field");
        if (strideFieldReset != null)
            strideFieldReset.SetEnabled(true);

        Button deleteButtonReset = ve.Q<Button>("delete-button");
        if (deleteButtonReset != null)
            deleteButtonReset.visible = true;

        VisualElement entryContainer = ve.Q<VisualElement>("entry-row");
        if (entryContainer != null)
            entryContainer.style.backgroundColor = StyleKeyword.Null;

        VisualElement strideContainer = ve.Q<VisualElement>("stride-container");
        if (strideContainer != null)
            strideContainer.style.display = DisplayStyle.None;
    }

    private void OnVariableItemIndexChanged(int oldIndex, int newIndex)
    {
        EditorUtility.SetDirty(cachedDefinition);
    }

    private void ChangeVariableType(BlackboardVariableBase oldVar, int listIndex, Type newType)
    {
        string oldName = oldVar.Name;
        int oldStride = oldVar.Stride;

        Type genericType = typeof(BlackboardVariable<>).MakeGenericType(newType);
        BlackboardVariableBase newVar = (BlackboardVariableBase)Activator.CreateInstance(genericType);
        newVar.Name = oldName;
        newVar.Stride = oldStride;

        Undo.RecordObject(cachedDefinition, "Change Variable Type");
        cachedDefinition.sharedVariables[listIndex] = newVar;
        EditorUtility.SetDirty(cachedDefinition);
        variableListView.Rebuild();
    }

    // ── Creator UI ──────────────────────────────────────────────────

    private void BuildCreatorUI()
    {
        LoadEntryTemplate();

        if (creatorTemplate != null)
        {
            VisualElement creatorRoot = creatorTemplate.CloneTree();
            blackBoardViewContainer.Add(creatorRoot);

            creatorNameField = creatorRoot.Q<TextField>("creator-name-field");
            creatorButton = creatorRoot.Q<Button>("creator-button");
            creatorRow = creatorRoot.Q<VisualElement>("creator-row");
            creatorButton.clicked += () => OpenVariableTypePopup();
        }
        else
        {
            // Fallback — build inline if template is missing
            Label header = new Label("Add Variable")
            {
                style =
                {
                    fontSize = 14,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginBottom = 6,
                    marginTop = 4
                }
            };
            blackBoardViewContainer.Add(header);

            creatorRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    marginBottom = 4,
                    flexShrink = 0,
                    alignItems = Align.Center
                }
            };

            creatorNameField = new TextField { style = { flexGrow = 1, marginRight = 4 }, value = "newVariable" };

            creatorButton = new Button(() => OpenVariableTypePopup())
            {
                text = "Add",
                style =
                {
                    width = 60,
                    height = 21,
                    flexShrink = 0
                }
            };

            creatorRow.Add(creatorNameField);
            creatorRow.Add(creatorButton);
            blackBoardViewContainer.Add(creatorRow);
        }
    }

    private void OpenVariableTypePopup()
    {
        // worldBound returns window-space coordinates including the window header
        // — PopupWindow.Show accepts them directly, per Unity docs.
        VariableTypeSearchPopup popup = new VariableTypeSearchPopup((Type type, bool isArray, int stride, bool isSquadData) =>
        {
            CreateVariable(type, isArray, stride, isSquadData);
        }, IsSquadContext);
        UnityEditor.PopupWindow.Show(creatorButton.worldBound, popup);
    }

    private void CreateVariable(Type selectedType, bool isArray, int stride, bool isSquadData = false)
    {
        if (cachedDefinition == null) return;

        string name = creatorNameField.value?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            Debug.LogWarning("[BlackBoardView] Variable name cannot be empty.");
            return;
        }

        name = MakeUniqueName(name, -1);

        Type sharedVarType = typeof(BlackboardVariable<>).MakeGenericType(selectedType);
        BlackboardVariableBase variable = (BlackboardVariableBase)Activator.CreateInstance(sharedVarType);
        variable.Name = name;
        variable.Stride = stride;
        variable.IsArray = isArray;
        variable.isSquadData = isSquadData;

        Undo.RecordObject(cachedDefinition, "Add Blackboard Variable");
        if (cachedDefinition.sharedVariables == null)
            cachedDefinition.sharedVariables = new List<BlackboardVariableBase>();
        cachedDefinition.sharedVariables.Add(variable);
        EditorUtility.SetDirty(cachedDefinition);

        creatorNameField.value = string.Empty;
        variableListView?.Rebuild();
    }

    // ── Rename / type-change / delete detection ────────────────────

    private void HandleRenames()
    {
        HashSet<string> currentNames = SnapshotNameSet();

        if (!currentNames.SetEquals(previousVariableNames))
        {
            List<string> removed = previousVariableNames.Except(currentNames).ToList();
            List<string> added = currentNames.Except(previousVariableNames).ToList();

            VariableChangePropagator p = GetPropagator();
            p.SetDefinition(cachedDefinition);

            int pairCount = Math.Min(removed.Count, added.Count);
            for (int i = 0; i < pairCount; i++)
                p.Rename(removed[i], added[i]);

            // Deletions: leftover removed items with no matching added name
            for (int i = pairCount; i < removed.Count; i++)
            {
                string name = removed[i];
                string type = previousVariableTypes.TryGetValue(name, out string t) ? t : null;
                p.Delete(name, type);
            }

            p.Flush();
        }

        previousVariableNames = currentNames;
    }

    private HashSet<string> SnapshotNameSet()
    {
        HashSet<string> names = new();
        if (cachedDefinition?.sharedVariables == null) return names;
        foreach (BlackboardVariableBase v in cachedDefinition.sharedVariables)
        {
            if (v != null && !string.IsNullOrEmpty(v.Name))
                names.Add(v.Name);
        }
        return names;
    }

    private void HandleTypeChanges()
    {
        Dictionary<string, string> currentTypes = SnapshotTypeMap();

        VariableChangePropagator p = GetPropagator();
        p.SetDefinition(cachedDefinition);

        if (!typeSyncDone && currentTypes.Count > 0)
        {
            typeSyncDone = true;
            foreach (KeyValuePair<string, string> kvp in currentTypes)
                p.TypeChange(kvp.Key, null, kvp.Value);
        }
        else
        {
            foreach (KeyValuePair<string, string> kvp in currentTypes)
            {
                string name = kvp.Key;
                string newType = kvp.Value;
                if (previousVariableTypes.TryGetValue(name, out string oldType) && oldType != newType)
                    p.TypeChange(name, oldType, newType);
            }
        }

        p.Flush();
        previousVariableTypes = currentTypes;
    }

    private VariableChangePropagator GetPropagator()
    {
        if (propagator == null)
        {
            propagator = new VariableChangePropagator();
            propagator.Register(new TreeNodesPropagationHandler());
            propagator.Register(new TrackedBindingsPropagationHandler());
            propagator.Register(new SquadBindingsPropagationHandler());
        }
        return propagator;
    }

    private Dictionary<string, string> SnapshotTypeMap()
    {
        Dictionary<string, string> types = new();
        if (cachedDefinition?.sharedVariables == null) return types;
        foreach (BlackboardVariableBase variable in cachedDefinition.sharedVariables)
        {
            if (variable != null && !string.IsNullOrEmpty(variable.Name))
                types[variable.Name] = variable.GetValueType()?.AssemblyQualifiedName;
        }
        return types;
    }

    // ── Undo / redo ──────────────────────────────────────────────

    private void OnUndoRedoPerformed()
    {
        if (cachedDefinition == null || variableListView == null) return;

        // itemsSource may point to a stale list reference after undo;
        // re-assign and rebuild so the ListView reflects the restored state.
        variableListView.itemsSource = cachedDefinition.sharedVariables;
        variableListView.Rebuild();

        // Immediately re-run propagation so renames/deletions from undo
        // are picked up without waiting for the 100ms schedule tick.
        HandleRenames();
        HandleTypeChanges();
    }

    private void OnDetach(DetachFromPanelEvent evt)
    {
        Undo.undoRedoPerformed -= OnUndoRedoPerformed;
    }
}
