using System;
using System.Collections.Generic;
using System.Linq;
using BehaviourTree.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace BehaviourTree.Editor
{
    /// <summary>
    /// Searchable popup for selecting an existing blackboard variable.
    /// Reuses VariableTypeSearchPopup.uxml for layout. Shows variables
    /// from the blackboard definition with Singular/Array radio filter.
    /// </summary>
    public class VariableSearchPopup : PopupWindowContent
    {
        private const float WindowWidth = 300f;
        private const float WindowHeight = 320f;

        private readonly BlackboardDefinition definition;
        private readonly Type filterType;
        private readonly bool isSquadContext;
        private readonly Action<BlackboardVariableBase, bool> onVariableSelected;
        private readonly bool defaultToArray;

        private TextField searchField;
        private RadioButton radioSingular;
        private RadioButton radioArray;
        private RadioButton radioSquadData;
        private VisualElement sizeRow;
        private ListView variableListView;
        private Label emptyStateLabel;

        private List<BlackboardVariableBase> allVariables;
        private List<BlackboardVariableBase> filteredVariables;

        public VariableSearchPopup(BlackboardDefinition definition, Type filterType, Action<BlackboardVariableBase, bool> onVariableSelected, bool defaultToArray = false, bool isSquadContext = false)
        {
            this.definition = definition;
            this.filterType = filterType;
            this.isSquadContext = isSquadContext;
            this.onVariableSelected = onVariableSelected;
            this.defaultToArray = defaultToArray;
        }

        public override VisualElement CreateGUI()
        {
            IReadOnlyList<BlackboardVariableBase> vars = definition?.GetAllVariables();
            allVariables = vars != null ? new List<BlackboardVariableBase>(vars) : new List<BlackboardVariableBase>();
            filteredVariables = new List<BlackboardVariableBase>();

            // Load UXML (reuse VariableTypeSearchPopup layout)
            string uxmlPath = BehaviourTreeEditorPaths.VariableTypeSearchPopupUxml;
            VisualTreeAsset treeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
            VisualElement root = treeAsset.CloneTree();

            // Load USS
            string ussPath = BehaviourTreeEditorPaths.VariableTypeSearchPopupUss;
            StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(ussPath);
            if (styleSheet != null)
                root.styleSheets.Add(styleSheet);

            // Query elements
            searchField = root.Q<TextField>("search-field");
            radioSingular = root.Q<RadioButton>("radio-singular");
            radioArray = root.Q<RadioButton>("radio-array");
            radioSquadData = root.Q<RadioButton>("radio-squad-data");
            sizeRow = root.Q<VisualElement>("size-row");
            variableListView = root.Q<ListView>("type-list");
            emptyStateLabel = root.Q<Label>("empty-state-label");

            // Update header
            Label header = root.Q<Label>();
            if (header != null) header.text = "Select Variable";

            // Singular selected by default; hide size row (we're picking existing variables)
            radioSingular.value = !defaultToArray;
            radioArray.value = defaultToArray;
            sizeRow.visible = false;

            // Show squad data radio when in squad context
            if (isSquadContext)
                radioSquadData.style.display = DisplayStyle.Flex;

            // Radio filter triggers re-filter
            radioSingular.RegisterValueChangedCallback(evt => { if (evt.newValue) ApplyFilters(); });
            radioArray.RegisterValueChangedCallback(evt => { if (evt.newValue) ApplyFilters(); });
            radioSquadData.RegisterValueChangedCallback(evt => { if (evt.newValue) ApplyFilters(); });

            // Configure ListView
            variableListView.itemsSource = filteredVariables;
            variableListView.makeItem = MakeListItem;
            variableListView.bindItem = BindListItem;
            variableListView.itemsChosen += OnItemChosen;
            variableListView.selectionChanged += OnSelectionChanged;

            // Wire callbacks
            searchField.RegisterValueChangedCallback(OnSearchChanged);
            searchField.RegisterCallback<KeyDownEvent>(OnSearchKeyDown);
            variableListView.RegisterCallback<KeyDownEvent>(OnListKeyDown);

            // Focus search field on open
            root.schedule.Execute(() => searchField.Focus()).StartingIn(0);

            // Apply filters immediately so the list is filtered on first render
            ApplyFilters();

            return root;
        }

        private VisualElement MakeListItem()
        {
            return new Label { style = { paddingLeft = 6, unityTextAlign = TextAnchor.MiddleLeft } };
        }

        private void BindListItem(VisualElement element, int index)
        {
            Label label = element as Label;
            if (label == null || index < 0 || index >= filteredVariables.Count) return;

            BlackboardVariableBase variable = filteredVariables[index];
            Type varType = variable.GetValueType();
            string typeDisplay = varType != null
                ? FieldTypeHelper.GetDisplayName(varType, variable.Stride)
                : variable.TypeName ?? "?";

            label.text = $"{variable.Name}  ({typeDisplay})";
        }

        public override Vector2 GetWindowSize()
        {
            return new Vector2(WindowWidth, WindowHeight);
        }

        public override void OnOpen() { }
        public override void OnClose() { }

        private void ApplyFilters()
        {
            string query = (searchField?.value ?? string.Empty).Trim();
            bool showArrays = radioArray?.value ?? false;
            bool showSquad = radioSquadData?.value ?? false;

            filteredVariables.Clear();

            for (int i = 0; i < allVariables.Count; i++)
            {
                BlackboardVariableBase variable = allVariables[i];

                // Skip system variables
                if (variable.isSystemVariable) continue;

                // Squad-data / Singular / Array radio filter
                if (showSquad)
                {
                    if (!variable.isSquadData) continue;
                }
                else
                {
                    if (variable.isSquadData) continue;
                    bool isArray = variable.Stride > 1;
                    if (showArrays != isArray) continue;
                }

                // Type filter — only show variables whose value type matches the member type
                if (filterType != null)
                {
                    Type varType = variable.GetValueType();
                    if (varType != filterType)
                        continue;
                }

                // Text search
                if (!string.IsNullOrEmpty(query))
                {
                    if (variable.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                }

                filteredVariables.Add(variable);
            }

            variableListView.Rebuild();

            // Toggle empty state
            bool isEmpty = filteredVariables.Count == 0;
            variableListView.style.display = isEmpty ? DisplayStyle.None : DisplayStyle.Flex;
            if (emptyStateLabel != null)
                emptyStateLabel.style.display = isEmpty ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void OnSearchChanged(ChangeEvent<string> evt)
        {
            ApplyFilters();
        }

        private void OnSearchKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.DownArrow)
            {
                variableListView.Focus();
                if (filteredVariables.Count > 0)
                    variableListView.SetSelection(0);
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.Escape)
            {
                editorWindow.Close();
                evt.StopPropagation();
            }
        }

        private void OnListKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            {
                CommitSelection();
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.Escape)
            {
                editorWindow.Close();
                evt.StopPropagation();
            }
        }

        private void OnSelectionChanged(IEnumerable<object> items)
        {
            if (variableListView.selectedIndex >= 0 && variableListView.selectedIndex < filteredVariables.Count)
                CommitSelection();
        }

        private void OnItemChosen(IEnumerable<object> items)
        {
            CommitSelection();
        }

        private void CommitSelection()
        {
            if (variableListView.selectedIndex < 0 || variableListView.selectedIndex >= filteredVariables.Count)
                return;

            BlackboardVariableBase selectedVariable = filteredVariables[variableListView.selectedIndex];
            editorWindow.Close();
            onVariableSelected?.Invoke(selectedVariable, radioArray?.value ?? false);
        }
    }
}
