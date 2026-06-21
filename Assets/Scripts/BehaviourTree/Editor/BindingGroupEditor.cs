using System;
using System.Collections.Generic;
using BehaviourTree.Core;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace BehaviourTree.Editor
{
    /// <summary>
    /// Reusable editor for a SquadBindingGroup's variable bindings.
    /// Used by both SquadDefinitionEditor (per-tree binding groups) and
    /// SquadTabView (per-connection bindings between a tree and squad).
    /// Filters variable dropdowns by type so only compatible variables can be bound.
    /// </summary>
    public class BindingGroupEditor : VisualElement
    {
        /// <summary>
        /// Fired when bindings change in any BindingGroupEditor.
        /// Parameter: the SquadDefinition that was modified, and the source
        /// object (SquadDefinitionEditor or SquadTabView) that initiated the change.
        /// Listeners should skip refreshes where source == self to avoid flicker.
        /// </summary>
        public static event Action<SquadDefinition, object> BindingsChangedForSquad;

        public static void NotifyBindingsChanged(SquadDefinition squad, object source)
        {
            BindingsChangedForSquad?.Invoke(squad, source);
        }
        private const string PlaceholderText = "Select a variable...";

        private SquadBindingGroup bindingGroup;
        private BlackboardDefinition treeDef;
        private BlackboardDefinition squadDef;
        private Action onChanged;
        private VisualTreeAsset bindingRowTemplate;
        private VisualTreeAsset bindingGroupFoldoutTemplate;
        private string foldoutTitle;
        private Action onRemoveGroup;

        private Foldout foldout;
        private VisualElement rowsContainer;
        private bool showAddButton = true;

        private List<string> treeVarNames;
        private List<string> squadVarNames;
        private Dictionary<string, Type> treeVarTypes;
        private Dictionary<string, Type> squadVarTypes;

        public BindingGroupEditor(
            SquadBindingGroup bindingGroup,
            BlackboardDefinition treeDef,
            BlackboardDefinition squadDef,
            Action onChanged,
            VisualTreeAsset bindingRowTemplate = null,
            VisualTreeAsset bindingGroupFoldoutTemplate = null,
            string foldoutTitle = null,
            Action onRemoveGroup = null,
            bool showAddButton = true)
        {
            this.bindingGroup = bindingGroup;
            this.treeDef = treeDef;
            this.squadDef = squadDef;
            this.onChanged = onChanged;
            this.bindingRowTemplate = bindingRowTemplate;
            this.bindingGroupFoldoutTemplate = bindingGroupFoldoutTemplate;
            this.foldoutTitle = foldoutTitle;
            this.onRemoveGroup = onRemoveGroup;
            this.showAddButton = showAddButton;

            BuildVariableMaps();

            BuildUI();
        }

        private void BuildVariableMaps()
        {
            treeVarNames = new List<string>();
            treeVarTypes = new Dictionary<string, Type>();
            BuildVarList(treeDef, treeVarNames, treeVarTypes);

            squadVarNames = new List<string>();
            squadVarTypes = new Dictionary<string, Type>();
            BuildVarList(squadDef, squadVarNames, squadVarTypes);
        }

        private static void BuildVarList(BlackboardDefinition def, List<string> names, Dictionary<string, Type> types)
        {
            names.Clear();
            types.Clear();
            if (def == null) return;

            IReadOnlyList<BlackboardVariableBase> vars = def.GetAllVariables();
            for (int i = 0; i < vars.Count; i++)
            {
                BlackboardVariableBase v = vars[i];
                if (v == null || string.IsNullOrEmpty(v.Name))
                    continue;

                names.Add(v.Name);
                types[v.Name] = v.GetValueType();
            }
        }

        private void BuildUI()
        {
            Clear();
            foldout = null;

            VisualElement root;

            if (bindingGroupFoldoutTemplate != null && !string.IsNullOrEmpty(foldoutTitle))
            {
                root = bindingGroupFoldoutTemplate.CloneTree();
                foldout = root.Q<Foldout>("binding-group-foldout");
                if (foldout != null)
                    foldout.text = foldoutTitle;

                rowsContainer = root.Q<VisualElement>("binding-rows-container");
                if (rowsContainer == null)
                    rowsContainer = foldout ?? root;

                Button addBindingButton = root.Q<Button>("add-binding-to-group-button");
                if (addBindingButton != null)
                    addBindingButton.clicked += OnAddBindingClicked;

                Button removeGroupButton = root.Q<Button>("remove-binding-group-button");
                if (removeGroupButton != null)
                {
                    if (onRemoveGroup != null)
                        removeGroupButton.clicked += () => onRemoveGroup();
                    else
                        removeGroupButton.RemoveFromHierarchy();
                }
            }
            else
            {
                root = new VisualElement();
                root.AddToClassList("binding-group-editor");
                rowsContainer = root;

                if (showAddButton)
                {
                    Button addBindingButton = new Button(OnAddBindingClicked) { text = "+ Add Binding" };
                    addBindingButton.AddToClassList("binding-add-button");
                    root.Add(addBindingButton);
                }

                if (onRemoveGroup != null)
                {
                    Button removeGroupButton = new Button(() => onRemoveGroup()) { text = "Remove Group" };
                    removeGroupButton.AddToClassList("binding-remove-group-button");
                    root.Add(removeGroupButton);
                }
            }

            BuildBindingRows();
            Add(root);
        }

        private void BuildBindingRows()
        {
            rowsContainer.Clear();

            if (bindingGroup.bindings == null || bindingGroup.bindings.Count == 0)
                return;

            for (int bindingIndex = 0; bindingIndex < bindingGroup.bindings.Count; bindingIndex++)
            {
                VariableBinding binding = bindingGroup.bindings[bindingIndex];
                int capturedBindingIndex = bindingIndex;

                VisualElement bindingRow;
                if (bindingRowTemplate != null)
                    bindingRow = bindingRowTemplate.CloneTree();
                else
                {
                    bindingRow = new VisualElement();
                    bindingRow.AddToClassList("binding-row");
                }

                // Determine selected types for cross-filtering
                Type selectedTreeType = GetVarType(treeVarTypes, binding.treeVariableName);
                Type selectedSquadType = GetVarType(squadVarTypes, binding.squadVariableName);

                // ── Tree variable dropdown (filtered by squad type) ──
                List<string> treeChoices = BuildFilteredChoices(treeVarNames, treeVarTypes, selectedSquadType);
                int treeIndex = GetSelectedIndex(treeChoices, binding.treeVariableName);
                PopupField<string> treeVarPopup = new PopupField<string>(treeChoices, treeIndex);
                treeVarPopup.AddToClassList("binding-tree-var");
                treeVarPopup.RegisterValueChangedCallback(evt =>
                {
                    string selected = evt.newValue;
                    binding.treeVariableName = IsPlaceholder(selected) ? null : selected;

                    // If squad var type no longer matches, clear it
                    if (binding.treeVariableName != null)
                    {
                        Type newTreeType = GetVarType(treeVarTypes, binding.treeVariableName);
                        if (newTreeType != null && binding.squadVariableName != null)
                        {
                            Type squadType = GetVarType(squadVarTypes, binding.squadVariableName);
                            if (squadType != newTreeType)
                                binding.squadVariableName = null;
                        }
                    }
                    onChanged?.Invoke();
                    BuildBindingRows();
                });
                ReplacePlaceholder(bindingRow, "tree-var-placeholder", treeVarPopup);
                if (treeVarPopup.parent == null)
                    bindingRow.Add(treeVarPopup);

                // ── Arrow label ──
                Label arrowLabel = bindingRow.Q<Label>("binding-arrow");
                if (arrowLabel == null)
                {
                    arrowLabel = new Label();
                    arrowLabel.AddToClassList("binding-arrow");
                    bindingRow.Add(arrowLabel);
                }
                arrowLabel.text = binding.direction == BindingDirection.ToSquad ? "→" :
                    binding.direction == BindingDirection.FromSquad ? "←" : "↔";

                // ── Squad variable dropdown (filtered by tree type) ──
                List<string> squadChoices = BuildFilteredChoices(squadVarNames, squadVarTypes, selectedTreeType);
                int squadIndex = GetSelectedIndex(squadChoices, binding.squadVariableName);
                PopupField<string> squadVarPopup = new PopupField<string>(squadChoices, squadIndex);
                squadVarPopup.AddToClassList("binding-squad-var");
                squadVarPopup.RegisterValueChangedCallback(evt =>
                {
                    string selected = evt.newValue;
                    binding.squadVariableName = IsPlaceholder(selected) ? null : selected;

                    // If tree var type no longer matches, clear it
                    if (binding.squadVariableName != null)
                    {
                        Type newSquadType = GetVarType(squadVarTypes, binding.squadVariableName);
                        if (newSquadType != null && binding.treeVariableName != null)
                        {
                            Type treeType = GetVarType(treeVarTypes, binding.treeVariableName);
                            if (treeType != newSquadType)
                                binding.treeVariableName = null;
                        }
                    }
                    onChanged?.Invoke();
                    BuildBindingRows();
                });
                ReplacePlaceholder(bindingRow, "squad-var-placeholder", squadVarPopup);
                if (squadVarPopup.parent == null)
                    bindingRow.Add(squadVarPopup);

                // ── Direction enum ──
                EnumField directionField = new EnumField(binding.direction);
                directionField.AddToClassList("binding-direction-field");
                directionField.RegisterValueChangedCallback(evt =>
                {
                    binding.direction = (BindingDirection)evt.newValue;
                    arrowLabel.text = binding.direction == BindingDirection.ToSquad ? "→" :
                        binding.direction == BindingDirection.FromSquad ? "←" : "↔";
                    onChanged?.Invoke();
                });
                ReplacePlaceholder(bindingRow, "direction-placeholder", directionField);
                if (directionField.parent == null)
                    bindingRow.Add(directionField);

                // ── Remove binding button ──
                Button removeBindingButton = bindingRow.Q<Button>("binding-remove-button");
                if (removeBindingButton == null)
                {
                    removeBindingButton = new Button { text = "X" };
                    removeBindingButton.AddToClassList("binding-remove-button");
                    bindingRow.Add(removeBindingButton);
                }
                removeBindingButton.clicked += () =>
                {
                    bindingGroup.bindings.RemoveAt(capturedBindingIndex);
                    onChanged?.Invoke();
                    BuildBindingRows();
                };

                rowsContainer.Add(bindingRow);
            }
        }

        private void OnAddBindingClicked()
        {
            if (bindingGroup.bindings == null)
                bindingGroup.bindings = new List<VariableBinding>();

            bindingGroup.bindings.Add(new VariableBinding
            {
                treeVariableName = null,
                squadVariableName = null,
                direction = BindingDirection.Both
            });
            onChanged?.Invoke();
            BuildBindingRows();
        }

        /// <summary>
        /// Builds a dropdown choices list prepended with the placeholder, optionally
        /// filtered to only variables whose type matches <paramref name="matchType"/>.
        /// </summary>
        private static List<string> BuildFilteredChoices(List<string> allNames, Dictionary<string, Type> types, Type matchType)
        {
            List<string> choices = new List<string> { PlaceholderText };

            if (matchType == null)
            {
                choices.AddRange(allNames);
            }
            else
            {
                for (int i = 0; i < allNames.Count; i++)
                {
                    string name = allNames[i];
                    if (types.TryGetValue(name, out Type t) && t == matchType)
                        choices.Add(name);
                }
            }

            return choices;
        }

        /// <summary>
        /// Returns the index of <paramref name="variableName"/> in the choices list,
        /// or 0 (the placeholder) if the name is null, empty, or not found.
        /// </summary>
        private static int GetSelectedIndex(List<string> choices, string variableName)
        {
            if (string.IsNullOrEmpty(variableName))
                return 0;
            int index = choices.IndexOf(variableName);
            return index > 0 ? index : 0;
        }

        private static bool IsPlaceholder(string value)
        {
            return string.IsNullOrEmpty(value) || value == PlaceholderText;
        }

        private static Type GetVarType(Dictionary<string, Type> types, string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            types.TryGetValue(name, out Type t);
            return t;
        }

        private static void ReplacePlaceholder(VisualElement parent, string placeholderName, VisualElement replacement)
        {
            VisualElement placeholder = parent.Q<VisualElement>(placeholderName);
            if (placeholder == null) return;
            int index = placeholder.parent.IndexOf(placeholder);
            placeholder.parent.Insert(index, replacement);
            placeholder.RemoveFromHierarchy();
        }

        /// <summary>
        /// Rebuilds the entire UI, preserving foldout expanded state.
        /// </summary>
        public void Rebuild()
        {
            bool wasExpanded = foldout?.value ?? true;
            BuildVariableMaps();
            BuildUI();
            if (foldout != null)
                foldout.value = wasExpanded;
        }
    }
}
