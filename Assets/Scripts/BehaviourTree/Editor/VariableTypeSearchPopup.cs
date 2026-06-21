using System;
using System.Collections.Generic;
using System.Linq;
using BehaviourTree.Core;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace BehaviourTree.Editor
{
    /// <summary>
    /// Searchable popup for selecting a variable type, with Singular/Array radio buttons
    /// and a Size field that appears when Array is selected.
    /// When isSquadContext is true, a SquadData toggle is visible that forces Array
    /// and marks the variable as dynamically resized at runtime.
    /// Clicking a type in the list commits the selection immediately.
    /// </summary>
    public class VariableTypeSearchPopup : PopupWindowContent
    {
        private const float WindowWidth = 300f;
        private const float WindowHeight = 320f;

        private readonly Action<Type, bool, int, bool> onTypeSelected;
        private readonly bool isSquadContext;

        private TextField searchField;
        private RadioButton radioSingular;
        private RadioButton radioArray;
        private RadioButton radioSquadData;
        private VisualElement sizeRow;
        private IntegerField sizeField;
        private ListView typeListView;

        private List<Type> allTypes;
        private List<Type> filteredTypes;

        public VariableTypeSearchPopup(Action<Type, bool, int> onTypeSelected)
            : this((type, isArray, stride, isSquad) => onTypeSelected(type, isArray, stride), false)
        {
        }

        public VariableTypeSearchPopup(Action<Type, bool, int, bool> onTypeSelected, bool isSquadContext = false)
        {
            this.onTypeSelected = onTypeSelected;
            this.isSquadContext = isSquadContext;
        }

        public override VisualElement CreateGUI()
        {
            allTypes = VariableTypeRegistry.Types.ToList();
            filteredTypes = new List<Type>(allTypes);

            // Load UXML layout
            string uxmlPath = BehaviourTreeEditorPaths.VariableTypeSearchPopupUxml;
            VisualTreeAsset treeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
            VisualElement root = treeAsset.CloneTree();

            // Load USS stylesheet
            string ussPath = BehaviourTreeEditorPaths.VariableTypeSearchPopupUss;
            StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(ussPath);
            if (styleSheet != null)
                root.styleSheets.Add(styleSheet);

            // Query named elements
            searchField = root.Q<TextField>("search-field");
            radioSingular = root.Q<RadioButton>("radio-singular");
            radioArray = root.Q<RadioButton>("radio-array");
            radioSquadData = root.Q<RadioButton>("radio-squad-data");
            sizeRow = root.Q<VisualElement>("size-row");
            sizeField = root.Q<IntegerField>("size-field");
            typeListView = root.Q<ListView>("type-list");

            // Singular selected by default, size row hidden
            radioSingular.value = true;
            sizeRow.visible = false;

            if (isSquadContext)
                radioSquadData.style.display = DisplayStyle.Flex;

            // Show/hide size row based on radio selection
            radioSingular.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue)
                {
                    sizeRow.visible = false;
                }
            });
            radioArray.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue)
                {
                    sizeRow.visible = true;
                }
            });
            radioSquadData.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue)
                {
                    sizeRow.visible = false;
                }
            });

            // Configure ListView data & bindings
            typeListView.itemsSource = filteredTypes;
            typeListView.makeItem = MakeListItem;
            typeListView.bindItem = BindListItem;
            typeListView.itemsChosen += OnItemChosen;
            typeListView.selectionChanged += OnSelectionChanged;

            // Wire callbacks
            searchField.RegisterValueChangedCallback(OnSearchChanged);
            searchField.RegisterCallback<KeyDownEvent>(OnSearchKeyDown);
            typeListView.RegisterCallback<KeyDownEvent>(OnListKeyDown);

            // Focus the search field on open
            root.schedule.Execute(() => searchField.Focus()).StartingIn(0);

            return root;
        }

        private VisualElement MakeListItem()
        {
            return new Label { style = { paddingLeft = 6, unityTextAlign = TextAnchor.MiddleLeft } };
        }

        private void BindListItem(VisualElement element, int index)
        {
            Label label = element as Label;
            if (label == null || index < 0 || index >= filteredTypes.Count) return;
            label.text = FieldTypeHelper.GetDisplayName(filteredTypes[index]);
        }

        public override Vector2 GetWindowSize()
        {
            return new Vector2(WindowWidth, WindowHeight);
        }

        public override void OnOpen() { }
        public override void OnClose() { }

        private void OnSearchChanged(ChangeEvent<string> evt)
        {
            string query = (evt.newValue ?? string.Empty).Trim();
            filteredTypes.Clear();

            if (string.IsNullOrEmpty(query))
            {
                filteredTypes.AddRange(allTypes);
            }
            else
            {
                foreach (Type type in allTypes)
                {
                    string displayName = FieldTypeHelper.GetDisplayName(type);
                    if (displayName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                        filteredTypes.Add(type);
                }
            }

            typeListView.Rebuild();
        }

        private void OnSearchKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.DownArrow)
            {
                typeListView.Focus();
                if (filteredTypes.Count > 0)
                    typeListView.SetSelection(0);
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
            // Click-to-commit: selecting a type in the list commits immediately
            if (typeListView.selectedIndex >= 0 && typeListView.selectedIndex < filteredTypes.Count)
                CommitSelection();
        }

        private void OnItemChosen(IEnumerable<object> items)
        {
            CommitSelection();
        }

        private void CommitSelection()
        {
            if (typeListView.selectedIndex < 0 || typeListView.selectedIndex >= filteredTypes.Count)
                return;

            Type selectedType = filteredTypes[typeListView.selectedIndex];
            bool isArray = radioArray.value;
            bool isSquad = radioSquadData.value;
            int stride = isArray ? Mathf.Max(1, sizeField.value) : 1;

            editorWindow.Close();
            onTypeSelected?.Invoke(selectedType, isArray, stride, isSquad);
        }
    }
}
