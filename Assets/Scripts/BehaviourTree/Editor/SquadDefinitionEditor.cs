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

            VisualElement root = visualTree.CloneTree();
            root.style.flexGrow = 1;
            rootVisualElement.Add(root);

            squadBlackBoardView = root.Q<BlackBoardView>("SquadBlackBoardView");
            createNewSquadButton = root.Q<Button>("CreateNewSquadButton");
            browseSquadButton = root.Q<Button>("BrowseSquadButton");
            rolesList = root.Q<VisualElement>("RolesList");
            newRoleField = root.Q<TextField>("NewRoleField");
            addRoleButton = root.Q<Button>("AddRoleButton");
            addBindingGroupButton = root.Q<Button>("AddBindingGroupButton");
            bindingGroupsScroll = root.Q<ScrollView>("BindingGroupsScroll");
            squadBarMenu = root.Q<ToolbarMenu>("SquadBarMenu");

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
            bbDef.name = squad.name + "_Schema";
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

                VisualElement row = new VisualElement
                {
                    style =
                    {
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.Center,
                        marginBottom = 2,
                        paddingLeft = 4,
                        paddingRight = 4,
                        paddingTop = 2,
                        paddingBottom = 2,
                        backgroundColor = new Color(0.22f, 0.22f, 0.22f, 1f)
                    }
                };

                Label roleLabel = new Label(role)
                {
                    style =
                    {
                        flexGrow = 1,
                        color = new Color(0.85f, 0.85f, 0.85f, 1f),
                        unityTextAlign = TextAnchor.MiddleLeft
                    }
                };
                row.Add(roleLabel);

                Button removeButton = new Button(() =>
                {
                    currentSquad.availableRoles.RemoveAt(capturedIndex);
                    EditorUtility.SetDirty(currentSquad);
                    BuildRolesUI();
                })
                {
                    text = "X",
                    style =
                    {
                        width = 24,
                        height = 20,
                        marginLeft = 4,
                        paddingLeft = 0,
                        paddingRight = 0
                    }
                };
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
                Label placeholder = new Label("No tree bindings. Use the Add button above to select a tree asset.")
                {
                    style =
                    {
                        color = Color.grey,
                        unityTextAlign = TextAnchor.MiddleCenter,
                        marginTop = 20,
                        fontSize = 12,
                        whiteSpace = WhiteSpace.Normal
                    }
                };
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
                    text = "Bindings for: " + treeName,
                    style =
                    {
                        marginBottom = 4,
                        paddingLeft = 4,
                        paddingRight = 4,
                        paddingTop = 2,
                        paddingBottom = 2
                    }
                };

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

                        VisualElement bindingRow = new VisualElement
                        {
                            style =
                            {
                                flexDirection = FlexDirection.Row,
                                alignItems = Align.Center,
                                marginBottom = 2,
                                paddingLeft = 8,
                                paddingRight = 4,
                                paddingTop = 2,
                                paddingBottom = 2,
                                backgroundColor = new Color(0.18f, 0.18f, 0.18f, 1f)
                            }
                        };

                        // Tree variable dropdown
                        int treeIndex = treeVarNames.IndexOf(binding.treeVariableName ?? "");
                        PopupField<string> treeVarPopup = new PopupField<string>(treeVarNames, Mathf.Max(0, treeIndex))
                        {
                            style = { flexGrow = 1, marginRight = 2 }
                        };
                        treeVarPopup.RegisterValueChangedCallback(evt =>
                        {
                            binding.treeVariableName = evt.newValue;
                            EditorUtility.SetDirty(currentSquad);
                        });
                        bindingRow.Add(treeVarPopup);

                        Label arrowLabel = new Label(binding.direction == BindingDirection.ToSquad ? "→" :
                            binding.direction == BindingDirection.FromSquad ? "←" : "↔")
                        {
                            style =
                            {
                                width = 20,
                                unityTextAlign = TextAnchor.MiddleCenter,
                                color = new Color(0.6f, 0.6f, 0.6f, 1f),
                                marginLeft = 2,
                                marginRight = 2
                            }
                        };
                        bindingRow.Add(arrowLabel);

                        // Squad variable dropdown
                        int squadIndex = squadVarNames.IndexOf(binding.squadVariableName ?? "");
                        PopupField<string> squadVarPopup = new PopupField<string>(squadVarNames, Mathf.Max(0, squadIndex))
                        {
                            style = { flexGrow = 1, marginRight = 2 }
                        };
                        squadVarPopup.RegisterValueChangedCallback(evt =>
                        {
                            binding.squadVariableName = evt.newValue;
                            EditorUtility.SetDirty(currentSquad);
                        });
                        bindingRow.Add(squadVarPopup);

                        EnumField directionField = new EnumField(binding.direction)
                        {
                            style = { width = 80, marginRight = 2 }
                        };
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
                            text = "X",
                            style = { width = 24, height = 20, paddingLeft = 0, paddingRight = 0 }
                        };
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
                    text = "+ Add Binding",
                    style = { marginLeft = 8, marginTop = 4, paddingLeft = 8, paddingRight = 8 }
                };
                groupFoldout.Add(addBindingButton);

                // Remove group button
                Button removeGroupButton = new Button(() =>
                {
                    currentSquad.bindingGroups.RemoveAt(capturedGroupIndex);
                    EditorUtility.SetDirty(currentSquad);
                    BuildBindingGroupsUI();
                })
                {
                    text = "Remove Group",
                    style = { marginLeft = 8, marginTop = 8, paddingLeft = 8, paddingRight = 8 }
                };
                groupFoldout.Add(removeGroupButton);

                bindingGroupsScroll.Add(groupFoldout);
            }
        }
    }
}
