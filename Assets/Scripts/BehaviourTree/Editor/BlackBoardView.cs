using System;
using System.Collections.Generic;
using System.Linq;
using BehaviourTree.Core;
using BehaviourTree.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[UxmlElement("BlackBoardView")]
public partial class BlackBoardView : VisualElement
{
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
    private StyleSheet entryStyleSheet;

    public BlackBoardView()
    {
        style.flexGrow = 1;
        style.paddingLeft = 8;
        style.paddingRight = 8;
        style.paddingTop = 8;
        style.paddingBottom = 8;
        style.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 1f);

        blackBoardViewContainer = new VisualElement { style = { flexGrow = 1, overflow = Overflow.Hidden } };
        Add(blackBoardViewContainer);

        Label placeholder = new Label("Add a blackboard definition")
        {
            style =
            {
                color = Color.grey,
                unityTextAlign = TextAnchor.MiddleCenter,
                marginTop = 40,
                fontSize = 13
            }
        };
        blackBoardViewContainer.Add(placeholder);
    }

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

        if (cachedDefinition != null)
        {
            BuildCreatorUI();

            // Separator
            VisualElement separator = new VisualElement
            {
                style =
                {
                    height = 1,
                    backgroundColor = new Color(0.4f, 0.4f, 0.4f, 0.6f),
                    marginTop = 6,
                    marginBottom = 6,
                    flexShrink = 0
                }
            };
            blackBoardViewContainer.Add(separator);
        }

        // ── ListView (replaces IMGUI ReorderableList) ────────────────
        LoadEntryTemplate();

        variableListView = new ListView
        {
            reorderable = true,
            reorderMode = ListViewReorderMode.Animated,
            showBorder = true,
            showFoldoutHeader = false,
            showAddRemoveFooter = false,
            selectionType = SelectionType.None,
            virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight,
            fixedItemHeight = 24,
            style = { flexGrow = 1, flexShrink = 1 },
            itemsSource = cachedDefinition?.sharedVariables,
            makeItem = () => entryTemplate.CloneTree(),
            bindItem = BindVariableListItem,
            unbindItem = UnbindVariableListItem,
        };
        variableListView.itemIndexChanged += OnVariableItemIndexChanged;
        blackBoardViewContainer.Add(variableListView);

        // Periodic rename/type-change detection
        schedule.Execute(() =>
        {
            HandleRenames();
            HandleTypeChanges();
        }).Every(100);
    }

    private void LoadEntryTemplate()
    {
        if (entryTemplate == null)
            entryTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(BehaviourTreeEditorPaths.BlackboardVariableEntryUxml);
        if (arrayElementTemplate == null)
            arrayElementTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(BehaviourTreeEditorPaths.ArrayElementRowUxml);
        if (entryStyleSheet == null)
            entryStyleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(BehaviourTreeEditorPaths.BlackboardVariableEntryUss);
    }

    // ── ListView bind/unbind/reorder ─────────────────────────────────

    private void BindVariableListItem(VisualElement ve, int index)
    {
        if (cachedDefinition?.sharedVariables == null) return;
        if (index < 0 || index >= cachedDefinition.sharedVariables.Count) return;

        BlackboardVariableBase variable = cachedDefinition.sharedVariables[index];
        if (variable == null) return;
        
        Type currentType = variable.GetValueType();
        int stride = variable.Stride;

        TextField nameField = ve.Q<TextField>("name-field");
        DropdownField typeDropdown = ve.Q<DropdownField>("type-dropdown");
        VisualElement strideContainer = ve.Q<VisualElement>("stride-container");
        IntegerField strideField = ve.Q<IntegerField>("stride-field");
        VisualElement valueCell = ve.Q<VisualElement>("value-cell");
        Button deleteButton = ve.Q<Button>("delete-button");

        // ── Name ────────────────────────────────────────
        nameField.SetValueWithoutNotify(variable.Name);
        nameField.RegisterValueChangedCallback(evt =>
        {
            variable.Name = evt.newValue;
            EditorUtility.SetDirty(cachedDefinition);
        });

        // ── Type dropdown ───────────────────────────────
        List<Type> types = VariableTypeRegistry.Types.ToList();
        typeDropdown.choices = types.Select(t => FieldTypeHelper.GetDisplayName(t)).ToList();
        int typeIndex = -1;
        if (currentType != null)
        {
            for (int i = 0; i < types.Count; i++)
            {
                if (types[i] == currentType) { typeIndex = i; break; }
            }
        }
        typeDropdown.index = typeIndex;
        typeDropdown.RegisterValueChangedCallback(evt =>
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
            strideField.RegisterValueChangedCallback(evt =>
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
        if (currentType != null
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
                for (int elementIndex = 0; elementIndex < stride; elementIndex++)
                {
                    VisualElement row = arrayElementTemplate.CloneTree();
                    row.Q<Label>("element-index").text = $"[{elementIndex}]";
                    VisualElement editorCell = row.Q<VisualElement>("element-editor");
                    VisualElement editor = factory();
                    editor.style.flexGrow = 1;
                    binder(editor, variable, elementIndex);
                    editorCell.Add(editor);
                    valueCell.Add(row);
                }
            }
        }
        else
        {
            valueCell.Add(new Label($"(no editor for {currentType?.Name ?? "null"})")
            {
                style = { color = Color.grey }
            });
        }

        // ── Delete ──────────────────────────────────────
        deleteButton.clicked += () =>
        {
            Undo.RecordObject(cachedDefinition, "Remove Variable");
            cachedDefinition.sharedVariables.RemoveAt(index);
            EditorUtility.SetDirty(cachedDefinition);
            variableListView.Rebuild();
        };

        // ── Apply USS ───────────────────────────────────
        if (entryStyleSheet != null && !ve.styleSheets.Contains(entryStyleSheet))
            ve.styleSheets.Add(entryStyleSheet);
    }

    private void UnbindVariableListItem(VisualElement ve, int index)
    {
        VisualElement valueCell = ve.Q<VisualElement>("value-cell");
        valueCell?.Clear();
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
        // ── Header ───────────────────────────────────────────────────
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

        // ── Row: Name │ Add ──────────────────────────────────────────
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

    private void OpenVariableTypePopup()
    {
        // worldBound returns window-space coordinates including the window header
        // — PopupWindow.Show accepts them directly, per Unity docs.
        VariableTypeSearchPopup popup = new VariableTypeSearchPopup((Type type, bool isArray, int stride) =>
        {
            CreateVariable(type, isArray, stride);
        });
        UnityEditor.PopupWindow.Show(creatorButton.worldBound, popup);
    }

    private void CreateVariable(Type selectedType, bool isArray, int stride)
    {
        if (cachedDefinition == null) return;

        string name = creatorNameField.value?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            Debug.LogWarning("[BlackBoardView] Variable name cannot be empty.");
            return;
        }

        Type sharedVarType = typeof(BlackboardVariable<>).MakeGenericType(selectedType);
        BlackboardVariableBase variable = (BlackboardVariableBase)Activator.CreateInstance(sharedVarType);
        variable.Name = name;
        variable.Stride = stride;
        variable.IsArray = isArray;

        Undo.RecordObject(cachedDefinition, "Add Blackboard Variable");
        if (cachedDefinition.sharedVariables == null)
            cachedDefinition.sharedVariables = new List<BlackboardVariableBase>();
        cachedDefinition.sharedVariables.Add(variable);
        EditorUtility.SetDirty(cachedDefinition);

        creatorNameField.value = string.Empty;
        variableListView?.Rebuild();
    }

    // ── Rename / type-change detection ──────────────────────────────

    private void HandleRenames()
    {
        HashSet<string> currentNames = SnapshotNameSet();

        if (!currentNames.SetEquals(previousVariableNames))
        {
            List<string> removed = previousVariableNames.Except(currentNames).ToList();
            List<string> added = currentNames.Except(previousVariableNames).ToList();

            int pairCount = Math.Min(removed.Count, added.Count);
            for (int i = 0; i < pairCount; i++)
                PropagateRename(removed[i], added[i]);
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

        if (!typeSyncDone && currentTypes.Count > 0)
        {
            typeSyncDone = true;
            foreach (KeyValuePair<string, string> kvp in currentTypes)
                PropagateTypeChange(kvp.Key, kvp.Value);
        }
        else
        {
            foreach (KeyValuePair<string, string> kvp in currentTypes)
            {
                string name = kvp.Key;
                string newType = kvp.Value;
                if (previousVariableTypes.TryGetValue(name, out string oldType) && oldType != newType)
                    PropagateTypeChange(name, newType);
            }
        }

        previousVariableTypes = currentTypes;
    }

    private Dictionary<string, string> SnapshotTypeMap()
    {
        Dictionary<string, string> types = new();
        if (cachedDefinition?.sharedVariables == null) return types;
        foreach (BlackboardVariableBase variable in cachedDefinition.sharedVariables)
        {
            if (variable != null && !string.IsNullOrEmpty(variable.Name))
                types[variable.Name] = variable.TypeName;
        }
        return types;
    }

    // ── Asset-wide propagation ──────────────────────────────────────

    private void PropagateRename(string oldName, string newName)
    {
        string[] guids = AssetDatabase.FindAssets("t:BehaviourTreeAssetBase");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            BehaviourTreeAssetBase tree = AssetDatabase.LoadAssetAtPath<BehaviourTreeAssetBase>(path);
            if (tree == null || tree.BlackboardDefinition != cachedDefinition) continue;

            UpdateTreeNodes(tree, oldName, newName);
        }
    }

    private static void UpdateTreeNodes(BehaviourTreeAssetBase treeAsset, string oldName, string newName)
    {
        string assetPath = AssetDatabase.GetAssetPath(treeAsset);
        if (string.IsNullOrEmpty(assetPath)) return;

        UnityEngine.Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);

        foreach (UnityEngine.Object obj in subAssets)
        {
            bool changed = false;

            if (obj is LeafNode leaf && leaf.fieldEntries != null)
            {
                Undo.RecordObject(leaf, "Rename Blackboard Variable");
                for (int i = 0; i < leaf.fieldEntries.Count; i++)
                {
                    NodeFieldEntry entry = leaf.fieldEntries[i];
                    if (entry.variableName == oldName)
                    {
                        entry.variableName = newName;
                        leaf.fieldEntries[i] = entry;
                        changed = true;
                    }
                }
            }
            else if (obj is DecoratorNode decorator && decorator.fieldEntries != null)
            {
                Undo.RecordObject(decorator, "Rename Blackboard Variable");
                for (int i = 0; i < decorator.fieldEntries.Count; i++)
                {
                    NodeFieldEntry entry = decorator.fieldEntries[i];
                    if (entry.variableName == oldName)
                    {
                        entry.variableName = newName;
                        decorator.fieldEntries[i] = entry;
                        changed = true;
                    }
                }
            }
            else if (obj is SubtreeNode subtree && subtree.bindings != null)
            {
                Undo.RecordObject(subtree, "Rename Blackboard Variable");
                for (int i = 0; i < subtree.bindings.Count; i++)
                {
                    SubtreeBinding binding = subtree.bindings[i];
                    if (binding.parentVariableName == oldName)
                    {
                        binding.parentVariableName = newName;
                        subtree.bindings[i] = binding;
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                EditorUtility.SetDirty(obj);
            }
        }
    }

    private void PropagateTypeChange(string variableName, string newTypeName)
    {
        string[] guids = AssetDatabase.FindAssets("t:BehaviourTreeAssetBase");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            BehaviourTreeAssetBase tree = AssetDatabase.LoadAssetAtPath<BehaviourTreeAssetBase>(path);
            if (tree == null || tree.BlackboardDefinition != cachedDefinition) continue;

            UpdateTreeNodesType(tree, variableName, newTypeName);
        }
    }

    private static void UpdateTreeNodesType(BehaviourTreeAssetBase treeAsset, string variableName, string newTypeName)
    {
        string assetPath = AssetDatabase.GetAssetPath(treeAsset);
        if (string.IsNullOrEmpty(assetPath)) return;

        UnityEngine.Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);

        foreach (UnityEngine.Object obj in subAssets)
        {
            bool changed = false;

            if (obj is LeafNode leaf && leaf.fieldEntries != null)
            {
                Undo.RecordObject(leaf, "Update Variable Type");
                for (int i = 0; i < leaf.fieldEntries.Count; i++)
                {
                    NodeFieldEntry entry = leaf.fieldEntries[i];
                    if (entry.variableName == variableName && entry.isVariable)
                    {
                        entry.fieldTypeName = newTypeName;
                        leaf.fieldEntries[i] = entry;
                        changed = true;
                    }
                }
            }
            else if (obj is DecoratorNode decorator && decorator.fieldEntries != null)
            {
                Undo.RecordObject(decorator, "Update Variable Type");
                for (int i = 0; i < decorator.fieldEntries.Count; i++)
                {
                    NodeFieldEntry entry = decorator.fieldEntries[i];
                    if (entry.variableName == variableName && entry.isVariable)
                    {
                        entry.fieldTypeName = newTypeName;
                        decorator.fieldEntries[i] = entry;
                        changed = true;
                    }
                }
            }
            else if (obj is CompositeNode composite && composite.fieldEntries != null)
            {
                Undo.RecordObject(composite, "Update Variable Type");
                for (int i = 0; i < composite.fieldEntries.Count; i++)
                {
                    NodeFieldEntry entry = composite.fieldEntries[i];
                    if (entry.variableName == variableName && entry.isVariable)
                    {
                        entry.fieldTypeName = newTypeName;
                        composite.fieldEntries[i] = entry;
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                EditorUtility.SetDirty(obj);
            }
        }
    }
}
