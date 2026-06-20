using System.Collections.Generic;
using System.Linq;
using BehaviourTree.Core;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace BehaviourTree.Editor
{
    /// <summary>
    /// Editor window for SquadDefinition assets.
    /// Shows the squad's BlackboardDefinition, available roles, and per-tree binding groups.
    /// Opened via the BehaviourTree editor toolbar, double-click, or BehaviourTree menu.
    /// </summary>
    public class SquadDefinitionEditor : EditorWindow
    {
        private SquadDefinition currentSquad;
        private ToolbarMenu squadBarMenu;
        private BlackBoardView squadBlackBoardView;
        private Button createNewSquadButton;
        private Button browseSquadButton;
        private VisualElement rolesList;
        private TextField newRoleField;
        private Button addRoleButton;
        private Button addBindingGroupButton;
        private ScrollView bindingGroupsScroll;
        private Label squadDefinitionNameLabel;
        private VisualTreeAsset roleRowTemplate;
        private VisualTreeAsset bindingRowTemplate;
        private VisualTreeAsset bindingGroupFoldoutTemplate;

        // Preserve foldout expanded state across UI rebuilds
        private HashSet<string> expandedRoleFoldouts = new HashSet<string>();
        private HashSet<string> expandedBindingGroupFoldouts = new HashSet<string>();

        [MenuItem("BehaviourTree/Open Squad Editor", priority = 30)]
        public static void OpenWindow()
        {
            SquadDefinitionEditor wnd = GetWindow<SquadDefinitionEditor>();
            wnd.titleContent = new GUIContent("Squad Editor");
        }

        [OnOpenAsset]
        public static bool OnOpenAsset(int instanceID, int line)
        {
            if (Selection.activeObject is SquadDefinition squad)
            {
                SquadDefinitionEditor wnd = GetWindow<SquadDefinitionEditor>();
                wnd.titleContent = new GUIContent("Squad Editor");
                wnd.LoadSquad(squad);
                return true;
            }
            return false;
        }

        public void CreateGUI()
        {
            VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                BehaviourTreeEditorPaths.SquadDefinitionEditorUxml);

            if (visualTree == null)
            {
                Debug.LogError("Failed to load SquadDefinitionEditor.uxml");
                return;
            }

            VisualElement rootVisual = visualTree.CloneTree();
            rootVisual.style.flexGrow = 1;
            rootVisualElement.Add(rootVisual);

            StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                BehaviourTreeEditorPaths.SquadDefinitionEditorUss);
            if (styleSheet != null)
                rootVisual.styleSheets.Add(styleSheet);

            // Load entry-row stylesheets so class selectors cascade to cloned templates
            StyleSheet roleRowUss = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                BehaviourTreeEditorPaths.RoleRowUss);
            if (roleRowUss != null)
                rootVisual.styleSheets.Add(roleRowUss);

            // Cache entry-row templates for BuildRolesUI / BuildBindingGroupsUI
            roleRowTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                BehaviourTreeEditorPaths.RoleRowUxml);
            bindingRowTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                BehaviourTreeEditorPaths.BindingRowUxml);
            bindingGroupFoldoutTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                BehaviourTreeEditorPaths.BindingGroupFoldoutUxml);
            

            squadBlackBoardView = rootVisual.Q<BlackBoardView>("squad-blackboard-view");
            createNewSquadButton = rootVisual.Q<Button>("create-new-squad-button");
            browseSquadButton = rootVisual.Q<Button>("browse-squad-button");
            rolesList = rootVisual.Q<VisualElement>("roles-list");
            newRoleField = rootVisual.Q<TextField>("new-role-field");
            addRoleButton = rootVisual.Q<Button>("add-role-button");
            addBindingGroupButton = rootVisual.Q<Button>("add-binding-group-button");
            bindingGroupsScroll = rootVisual.Q<ScrollView>("binding-groups-scroll");
            squadBarMenu = rootVisual.Q<ToolbarMenu>("squad-bar-menu");
            squadDefinitionNameLabel = rootVisual.Q<Label>("squad-definition-name");

            if (squadBlackBoardView != null)
                RegisterNestedScrollHandling(rootVisual);

            if (addRoleButton != null)
                addRoleButton.clicked += OnAddRoleClicked;

            if (createNewSquadButton != null)
                createNewSquadButton.clicked += OnCreateNewSquadNavClicked;

            if (browseSquadButton != null)
                browseSquadButton.clicked += OnBrowseSquadClicked;

            if (addBindingGroupButton != null)
                addBindingGroupButton.clicked += OnAddBindingGroupClicked;

            if (newRoleField != null)
                newRoleField.RegisterCallback<KeyDownEvent>(evt =>
                {
                    if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                        OnAddRoleClicked();
                });

            BuildSquadBarMenu();
        }

        /// <summary>
        /// Intercepts wheel events on the content-area (parent of #sections-scroll) during
        /// TrickleDown, before the outer ScrollView receives them. If the mouse is over an
        /// inner scrollable area (blackboard ListView, roles list, or bindings list) and that
        /// area has overflow in the scroll direction, the delta is forwarded to it and
        /// propagation is stopped. Otherwise the event falls through to the outer ScrollView.
        /// </summary>
        private float scrollSpeed = 10;
        private void RegisterNestedScrollHandling(VisualElement rootVisual)
        {
            VisualElement contentArea = rootVisual.Q<VisualElement>("content-area");
            ScrollView rolesScroll = rootVisual.Q<ScrollView>("roles-scroll");
            if (contentArea == null)
                return;

            contentArea.RegisterCallback<WheelEvent>(evt =>
            {
                VisualElement target = evt.target as VisualElement;
                if (target == null)
                    return;

                Vector2 delta = (Vector2)evt.delta * scrollSpeed;

                // Check if the mouse is over the blackboard's inner ScrollView
                ScrollView blackboardInnerScroll = squadBlackBoardView?.Q<ScrollView>();
                if (blackboardInnerScroll != null && IsDescendantOf(target, squadBlackBoardView))
                {
                    if (TryForwardScrollTo(blackboardInnerScroll, delta))
                        evt.StopPropagation();
                    return;
                }

                // Check if the mouse is over the roles ScrollView
                if (rolesScroll != null && IsDescendantOf(target, rolesScroll))
                {
                    if (TryForwardScrollTo(rolesScroll, delta))
                        evt.StopPropagation();
                    return;
                }

                // Check if the mouse is over the bindings ScrollView
                if (bindingGroupsScroll != null && IsDescendantOf(target, bindingGroupsScroll))
                {
                    if (TryForwardScrollTo(bindingGroupsScroll, delta))
                        evt.StopPropagation();
                }
            }, TrickleDown.TrickleDown);
        }

        /// <summary>
        /// Forwards <paramref name="delta"/> to <paramref name="scrollView"/> if the
        /// content overflows the viewport. If there is overflow the event is always consumed
        /// (even when already at the scroll limit) to prevent the outer ScrollView from
        /// taking over.
        /// Returns true if the scroll was consumed, false if it should bubble up.
        /// </summary>
        private static bool TryForwardScrollTo(ScrollView scrollView, Vector2 delta)
        {
            if (scrollView?.contentContainer == null || scrollView.contentViewport == null)
                return false;

            float contentHeight = scrollView.contentContainer.layout.height;
            float viewportHeight = scrollView.contentViewport.layout.height;

            // No overflow — let the outer ScrollView handle it
            if (contentHeight <= viewportHeight + 1f)
                return false;

            scrollView.scrollOffset += delta;
            return true;
        }

        private static bool IsDescendantOf(VisualElement element, VisualElement ancestor)
        {
            while (element != null)
            {
                if (element == ancestor)
                    return true;
                element = element.parent;
            }
            return false;
        }

        private void OnEnable()
        {
            EditorApplication.projectChanged += OnProjectChanged;
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= OnProjectChanged;
        }

        private void OnProjectChanged()
        {
            // Refresh if the current squad was deleted or modified externally
            if (currentSquad == null)
            {
                ClearUI();
                BuildSquadBarMenu();
            }
        }

        private void OnSelectionChange()
        {
            if (Selection.activeObject is SquadDefinition squad && squad != currentSquad)
                LoadSquad(squad);
        }

        /// <summary>
        /// Loads a SquadDefinition into the editor window.
        /// </summary>
        public void LoadSquad(SquadDefinition squad)
        {
            currentSquad = squad;
            RefreshUI();
            BuildSquadBarMenu();
        }

        private void RefreshUI()
        {
            if (currentSquad == null)
            {
                ClearUI();
                return;
            }

            // Schema
            if (squadBlackBoardView != null)
                squadBlackBoardView.BuildBlackboardView(currentSquad.blackboardDefinition);

            // Name label
            if (squadDefinitionNameLabel != null)
                squadDefinitionNameLabel.text = currentSquad.name;

            // Roles
            if (rolesList != null)
                BuildRolesUI();

            // Binding groups
            if (bindingGroupsScroll != null)
                BuildBindingGroupsUI();
        }

        private void ClearUI()
        {
            squadBlackBoardView?.BuildBlackboardView(null);
            if (squadDefinitionNameLabel != null)
                squadDefinitionNameLabel.text = string.Empty;
            rolesList?.Clear();
            bindingGroupsScroll?.Clear();
        }

        // ═══════════════════════════════════════════════════════════════
        // Squad bar menu
        // ═══════════════════════════════════════════════════════════════

        private void BuildSquadBarMenu()
        {
            if (squadBarMenu == null) return;

            DropdownMenu menu = squadBarMenu.menu;
            menu.ClearItems();

            menu.AppendAction("Create New Squad", CreateNewSquad);
            menu.AppendSeparator();

            // Recent squads
            const int maxRecent = 5;
            string[] guids = AssetDatabase.FindAssets("t:SquadDefinition");
            List<SquadDefinition> recentSquads = guids
                .Select(g => AssetDatabase.LoadAssetAtPath<SquadDefinition>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(s => s != null)
                .OrderByDescending(s => System.IO.File.GetLastWriteTime(AssetDatabase.GetAssetPath(s)))
                .Take(maxRecent)
                .ToList();

            foreach (SquadDefinition squad in recentSquads)
            {
                SquadDefinition captured = squad;
                menu.AppendAction("Open Squad/" + captured.name, _ => LoadSquad(captured));
            }

            menu.AppendSeparator("Open Squad/");
            menu.AppendAction("Open Squad/Browse...", BrowseOpenSquad);

            if (currentSquad != null)
            {
                menu.AppendSeparator();
                menu.AppendAction("Save Squad", _ =>
                {
                    EditorUtility.SetDirty(currentSquad);
                    AssetDatabase.SaveAssets();
                });
            }
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

            // Auto-create embedded BlackboardDefinition as a sub-asset
            BlackboardDefinition bbDef = CreateInstance<BlackboardDefinition>();
            bbDef.name = squad.name + "_BB_Definition";
            AssetDatabase.AddObjectToAsset(bbDef, path);
            squad.blackboardDefinition = bbDef;
            EditorUtility.SetDirty(squad);
            EditorUtility.SetDirty(bbDef);
            AssetDatabase.SaveAssets();

            LoadSquad(squad);
            Selection.activeObject = squad;
        }

        private void BrowseOpenSquad(DropdownMenuAction action)
        {
            if (currentSquad != null)
                EditorGUIUtility.PingObject(currentSquad);

            string path = EditorUtility.OpenFilePanel("Open Squad Definition", "Assets", "asset");
            if (string.IsNullOrEmpty(path)) return;

            // Convert absolute path to project-relative
            string projectRelative = "Assets" + path.Replace("\\", "/")
                .Replace(Application.dataPath.Replace("\\", "/"), "");

            SquadDefinition squad = AssetDatabase.LoadAssetAtPath<SquadDefinition>(projectRelative);
            if (squad != null)
                LoadSquad(squad);
        }

        // ═══════════════════════════════════════════════════════════════
        // Roles
        // ═══════════════════════════════════════════════════════════════

        private void OnCreateNewSquadNavClicked()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Squad Definition", "NewSquad", "asset",
                "Create a new SquadDefinition");

            if (string.IsNullOrEmpty(path)) return;

            SquadDefinition squad = CreateInstance<SquadDefinition>();
            squad.name = System.IO.Path.GetFileNameWithoutExtension(path);

            AssetDatabase.CreateAsset(squad, path);

            BlackboardDefinition bbDef = CreateInstance<BlackboardDefinition>();
            bbDef.name = squad.name + "_Schema";
            AssetDatabase.AddObjectToAsset(bbDef, path);
            squad.blackboardDefinition = bbDef;

            EditorUtility.SetDirty(squad);
            EditorUtility.SetDirty(bbDef);
            AssetDatabase.SaveAssets();

            LoadSquad(squad);
            Selection.activeObject = squad;
        }

        private void OnBrowseSquadClicked()
        {
            SquadSearchProvider provider = CreateInstance<SquadSearchProvider>();
            provider.onSquadSelected = LoadSquad;
            SearchWindow.Open(new SearchWindowContext(
                GUIUtility.GUIToScreenPoint(browseSquadButton.worldBound.position)),
                provider);
        }

        private void OnAddRoleClicked()
        {
            if (currentSquad == null || newRoleField == null) return;

            string roleName = newRoleField.value?.Trim();
            if (string.IsNullOrEmpty(roleName)) return;

            bool alreadyExists = false;
            for (int i = 0; i < currentSquad.availableRoles.Count; i++)
            {
                if (currentSquad.availableRoles[i].name == roleName)
                {
                    alreadyExists = true;
                    break;
                }
            }
            if (!alreadyExists)
            {
                currentSquad.availableRoles.Add(new SquadRole { name = roleName });
                EditorUtility.SetDirty(currentSquad);
                BuildRolesUI();
            }

            newRoleField.value = string.Empty;
            newRoleField.Focus();
        }

        private static void ReplacePlaceholder(VisualElement parent, string placeholderName, VisualElement replacement)
        {
            VisualElement placeholder = parent.Q<VisualElement>(placeholderName);
            if (placeholder == null) return;
            int index = placeholder.parent.IndexOf(placeholder);
            placeholder.parent.Insert(index, replacement);
            placeholder.RemoveFromHierarchy();
        }

        private void BuildRolesUI()
        {
            // Save expanded state of existing role foldouts before clearing
            expandedRoleFoldouts.Clear();
            for (int i = 0; i < rolesList.childCount; i++)
            {
                Foldout foldout = rolesList[i].Q<Foldout>("role-foldout");
                if (foldout != null && foldout.value)
                    expandedRoleFoldouts.Add(foldout.text);
            }

            rolesList.Clear();

            if (roleRowTemplate == null || currentSquad == null) return;

            List<SquadRole> roles = currentSquad.availableRoles;
            for (int i = 0; i < roles.Count; i++)
            {
                int capturedIndex = i;
                SquadRole role = roles[i];

                VisualElement row = roleRowTemplate.CloneTree();

                // Foldout text = role name
                Foldout foldout = row.Q<Foldout>("role-foldout");
                if (foldout != null)
                {
                    foldout.text = role.name;
                    foldout.value = expandedRoleFoldouts.Contains(role.name);
                }

                // Colour picker + banner
                ColorField colourPicker = row.Q<ColorField>("role-color-picker");
                VisualElement colourBanner = row.Q<VisualElement>("role-color-banner");
                if (colourPicker != null)
                {
                    colourPicker.value = role.colour;
                    colourPicker.RegisterValueChangedCallback(evt =>
                    {
                        currentSquad.availableRoles[capturedIndex].colour = evt.newValue;
                        if (colourBanner != null)
                            colourBanner.style.backgroundColor = evt.newValue;
                        EditorUtility.SetDirty(currentSquad);
                    });
                }
                if (colourBanner != null)
                    colourBanner.style.backgroundColor = role.colour;

                // Max amount (inside foldout)
                IntegerField maxAmountField = foldout?.Q<IntegerField>();
                if (maxAmountField != null)
                {
                    maxAmountField.value = role.maxAmount;
                    maxAmountField.RegisterValueChangedCallback(evt =>
                    {
                        currentSquad.availableRoles[capturedIndex].maxAmount = evt.newValue;
                        EditorUtility.SetDirty(currentSquad);
                    });
                }

                // Is fallback (inside foldout)
                Toggle fallbackToggle = foldout?.Q<Toggle>();
                if (fallbackToggle != null)
                {
                    fallbackToggle.value = role.isFallback;
                    fallbackToggle.RegisterValueChangedCallback(evt =>
                    {
                        currentSquad.availableRoles[capturedIndex].isFallback = evt.newValue;
                        EditorUtility.SetDirty(currentSquad);
                    });
                }

                // Remove button
                row.Q<Button>("role-remove-button").clicked += () =>
                {
                    currentSquad.availableRoles.RemoveAt(capturedIndex);
                    EditorUtility.SetDirty(currentSquad);
                    BuildRolesUI();
                };

                rolesList.Add(row);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // Binding groups
        // ═══════════════════════════════════════════════════════════════

        private void OnAddBindingGroupClicked()
        {
            if (currentSquad == null) return;

            TreeAssetSearchProvider provider = CreateInstance<TreeAssetSearchProvider>();
            provider.excludeSquad = currentSquad;
            provider.onTreeSelected = tree =>
            {
                SquadBindingGroup group = currentSquad.GetOrCreateBindingGroup(tree);
                if (group.bindings == null)
                    group.bindings = new List<VariableBinding>();

                EditorUtility.SetDirty(currentSquad);
                BuildBindingGroupsUI();
            };
            SearchWindow.Open(new SearchWindowContext(
                GUIUtility.GUIToScreenPoint(addBindingGroupButton.worldBound.position)),
                provider);
        }

        private static List<string> GetVariableNames(BlackboardDefinition def)
        {
            List<string> names = new List<string>();
            if (def == null) return names;

            IReadOnlyList<BlackboardVariableBase> vars = def.GetAllVariables();
            for (int i = 0; i < vars.Count; i++)
            {
                if (vars[i] != null && !string.IsNullOrEmpty(vars[i].Name))
                    names.Add(vars[i].Name);
            }
            return names;
        }

        private void BuildBindingGroupsUI()
        {
            // Save expanded state of existing binding group foldouts before clearing
            expandedBindingGroupFoldouts.Clear();
            for (int i = 0; i < bindingGroupsScroll.childCount; i++)
            {
                Foldout foldout = bindingGroupsScroll[i].Q<Foldout>("binding-group-foldout");
                if (foldout != null && foldout.value)
                    expandedBindingGroupFoldouts.Add(foldout.text);
            }

            bindingGroupsScroll.Clear();

            if (currentSquad.bindingGroups == null || currentSquad.bindingGroups.Count == 0)
            {
                Label placeholder = new Label("No tree bindings. Use the Add button above to select a tree asset.");
                placeholder.AddToClassList("binding-placeholder");
                bindingGroupsScroll.Add(placeholder);
                return;
            }

            for (int groupIndex = 0; groupIndex < currentSquad.bindingGroups.Count; groupIndex++)
            {
                SquadBindingGroup group = currentSquad.bindingGroups[groupIndex];
                int capturedGroupIndex = groupIndex;

                // Clone the foldout template (or create bare Foldout as fallback)
                VisualElement foldoutRoot;
                if (bindingGroupFoldoutTemplate != null)
                    foldoutRoot = bindingGroupFoldoutTemplate.CloneTree();
                else
                {
                    foldoutRoot = new Foldout();
                    foldoutRoot.AddToClassList("binding-group-foldout");
                }

                string treeName = group.treeAsset != null ? group.treeAsset.name : "Unknown Tree";
                string foldoutText = "Bindings for: " + treeName;
                Foldout groupFoldout = foldoutRoot.Q<Foldout>("binding-group-foldout");
                if (groupFoldout != null)
                {
                    groupFoldout.text = foldoutText;
                    groupFoldout.value = expandedBindingGroupFoldouts.Contains(foldoutText);
                }

                // Resolve variable name dropdowns from BB definitions
                List<string> treeVarNames = GetVariableNames(group.treeAsset?.BlackboardDefinition);
                List<string> squadVarNames = GetVariableNames(currentSquad.blackboardDefinition);

                // Container for binding rows (from template or use the Foldout content area)
                VisualElement rowsContainer = foldoutRoot.Q<VisualElement>("binding-rows-container") ?? groupFoldout ?? foldoutRoot;

                // Bindings list
                if (group.bindings != null && group.bindings.Count > 0)
                {
                    for (int bindingIndex = 0; bindingIndex < group.bindings.Count; bindingIndex++)
                    {
                        VariableBinding binding = group.bindings[bindingIndex];
                        int capturedBindingIndex = bindingIndex;

                        VisualElement bindingRow;
                        if (bindingRowTemplate != null)
                            bindingRow = bindingRowTemplate.CloneTree();
                        else
                        {
                            bindingRow = new VisualElement();
                            bindingRow.AddToClassList("binding-row");
                        }

                        // Tree variable dropdown (replaces tree-var-placeholder)
                        int treeIndex = treeVarNames.IndexOf(binding.treeVariableName ?? "");
                        PopupField<string> treeVarPopup = new PopupField<string>(treeVarNames, Mathf.Max(0, treeIndex));
                        treeVarPopup.AddToClassList("binding-tree-var");
                        treeVarPopup.RegisterValueChangedCallback(evt =>
                        {
                            binding.treeVariableName = evt.newValue;
                            EditorUtility.SetDirty(currentSquad);
                        });
                        ReplacePlaceholder(bindingRow, "tree-var-placeholder", treeVarPopup);
                        if (treeVarPopup.parent == null)
                            bindingRow.Add(treeVarPopup);

                        // Arrow label
                        Label arrowLabel = bindingRow.Q<Label>("binding-arrow");
                        if (arrowLabel == null)
                        {
                            arrowLabel = new Label();
                            arrowLabel.AddToClassList("binding-arrow");
                            bindingRow.Add(arrowLabel);
                        }
                        arrowLabel.text = binding.direction == BindingDirection.ToSquad ? "←" :
                            binding.direction == BindingDirection.FromSquad ? "→" : "↔";

                        // Squad variable dropdown (replaces squad-var-placeholder)
                        int squadIndex = squadVarNames.IndexOf(binding.squadVariableName ?? "");
                        PopupField<string> squadVarPopup = new PopupField<string>(squadVarNames, Mathf.Max(0, squadIndex));
                        squadVarPopup.AddToClassList("binding-squad-var");
                        squadVarPopup.RegisterValueChangedCallback(evt =>
                        {
                            binding.squadVariableName = evt.newValue;
                            EditorUtility.SetDirty(currentSquad);
                        });
                        ReplacePlaceholder(bindingRow, "squad-var-placeholder", squadVarPopup);
                        if (squadVarPopup.parent == null)
                            bindingRow.Add(squadVarPopup);

                        // Direction enum (replaces direction-placeholder)
                        EnumField directionField = new EnumField(binding.direction);
                        directionField.AddToClassList("binding-direction-field");
                        directionField.RegisterValueChangedCallback(evt =>
                        {
                            binding.direction = (BindingDirection)evt.newValue;
                            arrowLabel.text = binding.direction == BindingDirection.ToSquad ? "→" :
                                binding.direction == BindingDirection.FromSquad ? "←" : "↔";
                            EditorUtility.SetDirty(currentSquad);
                        });
                        ReplacePlaceholder(bindingRow, "direction-placeholder", directionField);
                        if (directionField.parent == null)
                            bindingRow.Add(directionField);

                        // Remove binding button
                        Button removeBindingButton = bindingRow.Q<Button>("binding-remove-button");
                        if (removeBindingButton == null)
                        {
                            removeBindingButton = new Button { text = "X" };
                            removeBindingButton.AddToClassList("binding-remove-button");
                            bindingRow.Add(removeBindingButton);
                        }
                        removeBindingButton.clicked += () =>
                        {
                            group.bindings.RemoveAt(capturedBindingIndex);
                            EditorUtility.SetDirty(currentSquad);
                            BuildBindingGroupsUI();
                        };

                        rowsContainer.Add(bindingRow);
                    }
                }

                // Add binding button (from template or create fallback)
                Button addBindingButton = foldoutRoot.Q<Button>("add-binding-to-group-button");
                if (addBindingButton == null)
                {
                    addBindingButton = new Button { text = "+ Add Binding" };
                    addBindingButton.AddToClassList("binding-add-button");
                    foldoutRoot.Add(addBindingButton);
                }
                addBindingButton.clicked += () =>
                {
                    if (group.bindings == null)
                        group.bindings = new List<VariableBinding>();

                    group.bindings.Add(new VariableBinding
                    {
                        treeVariableName = "",
                        squadVariableName = "",
                        direction = BindingDirection.Both
                    });
                    EditorUtility.SetDirty(currentSquad);
                    BuildBindingGroupsUI();
                };

                // Remove group button (from template or create fallback)
                Button removeGroupButton = foldoutRoot.Q<Button>("remove-binding-group-button");
                if (removeGroupButton == null)
                {
                    removeGroupButton = new Button { text = "Remove Group" };
                    removeGroupButton.AddToClassList("binding-remove-group-button");
                    foldoutRoot.Add(removeGroupButton);
                }
                removeGroupButton.clicked += () =>
                {
                    currentSquad.bindingGroups.RemoveAt(capturedGroupIndex);
                    EditorUtility.SetDirty(currentSquad);
                    BuildBindingGroupsUI();
                };

                bindingGroupsScroll.Add(foldoutRoot);
            }
        }
    }
}
