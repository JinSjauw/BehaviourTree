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

            squadBlackBoardView = rootVisual.Q<BlackBoardView>("squad-blackboard-view");
            createNewSquadButton = rootVisual.Q<Button>("create-new-squad-button");
            browseSquadButton = rootVisual.Q<Button>("browse-squad-button");
            rolesList = rootVisual.Q<VisualElement>("roles-list");
            newRoleField = rootVisual.Q<TextField>("new-role-field");
            addRoleButton = rootVisual.Q<Button>("add-role-button");
            addBindingGroupButton = rootVisual.Q<Button>("add-binding-group-button");
            bindingGroupsScroll = rootVisual.Q<ScrollView>("binding-groups-scroll");
            squadBarMenu = rootVisual.Q<ToolbarMenu>("squad-bar-menu");

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
        /// inner scrollable area (blackboard ListView, roles list, or bindings list), the
        /// delta is forwarded to that inner ScrollView and propagation is stopped.
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
                    blackboardInnerScroll.scrollOffset += delta;
                    evt.StopPropagation();
                    return;
                }

                // Check if the mouse is over the roles ScrollView
                if (rolesScroll != null && IsDescendantOf(target, rolesScroll))
                {
                    rolesScroll.scrollOffset += delta;
                    evt.StopPropagation();
                    return;
                }

                // Check if the mouse is over the bindings ScrollView
                if (bindingGroupsScroll != null && IsDescendantOf(target, bindingGroupsScroll))
                {
                    bindingGroupsScroll.scrollOffset += delta;
                    evt.StopPropagation();
                }
            }, TrickleDown.TrickleDown);
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

            if (!currentSquad.availableRoles.Contains(roleName))
            {
                currentSquad.availableRoles.Add(roleName);
                EditorUtility.SetDirty(currentSquad);
                BuildRolesUI();
            }

            newRoleField.value = string.Empty;
            newRoleField.Focus();
        }

        private void BuildRolesUI()
        {
            rolesList.Clear();

            List<string> roles = currentSquad.availableRoles;
            for (int i = 0; i < roles.Count; i++)
            {
                int capturedIndex = i;
                string role = roles[i];

                VisualElement row = new VisualElement();
                row.AddToClassList("role-row");

                Label roleLabel = new Label(role);
                roleLabel.AddToClassList("role-label");
                row.Add(roleLabel);

                Button removeButton = new Button(() =>
                {
                    currentSquad.availableRoles.RemoveAt(capturedIndex);
                    EditorUtility.SetDirty(currentSquad);
                    BuildRolesUI();
                })
                {
                    text = "X"
                };
                removeButton.AddToClassList("role-remove-button");
                row.Add(removeButton);

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

                string treeName = group.treeAsset != null ? group.treeAsset.name : "Unknown Tree";
                Foldout groupFoldout = new Foldout
                {
                    text = "Bindings for: " + treeName
                };
                groupFoldout.AddToClassList("binding-group-foldout");

                // Resolve variable name dropdowns from BB definitions
                List<string> treeVarNames = GetVariableNames(group.treeAsset?.BlackboardDefinition);
                List<string> squadVarNames = GetVariableNames(currentSquad.blackboardDefinition);

                // Bindings list
                if (group.bindings != null && group.bindings.Count > 0)
                {
                    for (int bindingIndex = 0; bindingIndex < group.bindings.Count; bindingIndex++)
                    {
                        VariableBinding binding = group.bindings[bindingIndex];
                        int capturedBindingIndex = bindingIndex;

                        VisualElement bindingRow = new VisualElement();
                        bindingRow.AddToClassList("binding-row");

                        // Tree variable dropdown
                        int treeIndex = treeVarNames.IndexOf(binding.treeVariableName ?? "");
                        PopupField<string> treeVarPopup = new PopupField<string>(treeVarNames, Mathf.Max(0, treeIndex));
                        treeVarPopup.AddToClassList("binding-tree-var");
                        treeVarPopup.RegisterValueChangedCallback(evt =>
                        {
                            binding.treeVariableName = evt.newValue;
                            EditorUtility.SetDirty(currentSquad);
                        });
                        bindingRow.Add(treeVarPopup);

                        Label arrowLabel = new Label(binding.direction == BindingDirection.ToSquad ? "→" :
                            binding.direction == BindingDirection.FromSquad ? "←" : "↔");
                        arrowLabel.AddToClassList("binding-arrow");
                        bindingRow.Add(arrowLabel);

                        // Squad variable dropdown
                        int squadIndex = squadVarNames.IndexOf(binding.squadVariableName ?? "");
                        PopupField<string> squadVarPopup = new PopupField<string>(squadVarNames, Mathf.Max(0, squadIndex));
                        squadVarPopup.AddToClassList("binding-squad-var");
                        squadVarPopup.RegisterValueChangedCallback(evt =>
                        {
                            binding.squadVariableName = evt.newValue;
                            EditorUtility.SetDirty(currentSquad);
                        });
                        bindingRow.Add(squadVarPopup);

                        EnumField directionField = new EnumField(binding.direction);
                        directionField.AddToClassList("binding-direction-field");
                        directionField.RegisterValueChangedCallback(evt =>
                        {
                            binding.direction = (BindingDirection)evt.newValue;
                            arrowLabel.text = binding.direction == BindingDirection.ToSquad ? "→" :
                                binding.direction == BindingDirection.FromSquad ? "←" : "↔";
                            EditorUtility.SetDirty(currentSquad);
                        });
                        bindingRow.Add(directionField);

                        Button removeBindingButton = new Button(() =>
                        {
                            group.bindings.RemoveAt(capturedBindingIndex);
                            EditorUtility.SetDirty(currentSquad);
                            BuildBindingGroupsUI();
                        })
                        {
                            text = "X"
                        };
                        removeBindingButton.AddToClassList("binding-remove-button");
                        bindingRow.Add(removeBindingButton);

                        groupFoldout.Add(bindingRow);
                    }
                }

                // Add binding button (inside group)
                Button addBindingButton = new Button(() =>
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
                })
                {
                    text = "+ Add Binding"
                };
                addBindingButton.AddToClassList("binding-add-button");
                groupFoldout.Add(addBindingButton);

                // Remove group button
                Button removeGroupButton = new Button(() =>
                {
                    currentSquad.bindingGroups.RemoveAt(capturedGroupIndex);
                    EditorUtility.SetDirty(currentSquad);
                    BuildBindingGroupsUI();
                })
                {
                    text = "Remove Group"
                };
                removeGroupButton.AddToClassList("binding-remove-group-button");
                groupFoldout.Add(removeGroupButton);

                bindingGroupsScroll.Add(groupFoldout);
            }
        }
    }
}
