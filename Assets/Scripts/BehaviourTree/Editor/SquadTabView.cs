using System.Collections.Generic;
using BehaviourTree.Core;
using BehaviourTree.Editor;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// VisualElement shown in the "Squads" tab of the BehaviourTreeEditor.
/// Displays the current tree's squad connections — squad picker, multi-role
/// assignment, and editable per-tree variable bindings shared with SquadDefinitionEditor.
/// </summary>
[UxmlElement("SquadTabView")]
public partial class SquadTabView : VisualElement
{
    private Label emptyStateLabel;
    private ScrollView connectionsScroll;
    private Button addConnectionButton;
    private BaseEditorTreeAsset currentTree;
    private VisualTreeAsset connectionRowTemplate;
    private VisualTreeAsset assignedRoleRowTemplate;
    private VisualTreeAsset bindingRowTemplate;
    private VisualTreeAsset bindingGroupFoldoutTemplate;

    public SquadTabView()
    {
        style.flexGrow = 1;

        VisualTreeAsset treeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
            BehaviourTreeEditorPaths.SquadTabViewUxml);
        if (treeAsset != null)
        {
            VisualElement ui = treeAsset.CloneTree();
            Add(ui);
        }

        emptyStateLabel = this.Q<Label>("empty-state-label");
        connectionsScroll = this.Q<ScrollView>("connections-scroll");
        addConnectionButton = this.Q<Button>("add-connection-button");

        if (addConnectionButton != null)
            addConnectionButton.clicked += OnAddConnectionClicked;

        connectionRowTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
            BehaviourTreeEditorPaths.SquadConnectionRowUxml);
        assignedRoleRowTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
            BehaviourTreeEditorPaths.SquadAssignedRoleRowUxml);
        bindingRowTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
            BehaviourTreeEditorPaths.BindingRowUxml);
        bindingGroupFoldoutTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
            BehaviourTreeEditorPaths.BindingGroupFoldoutUxml);

        BindingGroupEditor.BindingsChangedForSquad += OnBindingsExternallyChanged;
        RegisterCallback<DetachFromPanelEvent>(evt =>
        {
            BindingGroupEditor.BindingsChangedForSquad -= OnBindingsExternallyChanged;
        });
    }

    public void Refresh(BaseEditorTreeAsset treeAsset)
    {
        currentTree = treeAsset;
        RebuildUI();
    }

    private void RebuildUI()
    {
        connectionsScroll.Clear();

        if (currentTree == null)
        {
            connectionsScroll.style.display = DisplayStyle.None;
            emptyStateLabel.text = "Select a tree asset to view squad connections.";
            emptyStateLabel.style.display = DisplayStyle.Flex;
            return;
        }

        List<SquadConnection> connections = currentTree.squadConnections;

        if (connections == null || connections.Count == 0)
        {
            connectionsScroll.style.display = DisplayStyle.None;
            emptyStateLabel.text = "No squad connections. Add a squad below.";
            emptyStateLabel.style.display = DisplayStyle.Flex;
        }
        else
        {
            connectionsScroll.style.display = DisplayStyle.Flex;
            emptyStateLabel.style.display = DisplayStyle.None;

            for (int i = 0; i < connections.Count; i++)
                connectionsScroll.Add(BuildConnectionElement(connections[i], i));
        }
    }

    private VisualElement BuildConnectionElement(SquadConnection connection, int connectionIndex)
    {
        string squadName = connection.squad != null ? connection.squad.name : "No Squad Selected";
        Foldout foldout = new Foldout
        {
            text = squadName,
            style =
            {
                marginBottom = 4,
                marginTop = 4,
                paddingLeft = 4,
                paddingRight = 4,
                paddingTop = 2,
                paddingBottom = 2,
                backgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f)
            }
        };

        VisualElement content = connectionRowTemplate != null
            ? connectionRowTemplate.CloneTree()
            : new VisualElement();

        // ── Squad picker row ──
        Button selectSquadButton = content.Q<Button>("select-squad-button");
        Button openSquadButton = content.Q<Button>("open-squad-button");
        if (selectSquadButton != null)
        {
            selectSquadButton.text = connection.squad != null ? connection.squad.name : "Select Squad...";
            selectSquadButton.clicked += () =>
            {
                SquadSearchProvider provider = ScriptableObject.CreateInstance<SquadSearchProvider>();
                provider.onSquadSelected = squad =>
                {
                    connection.squad = squad;
                    connection.assignedRoles.Clear();
                    EditorUtility.SetDirty(currentTree);
                    RebuildUI();
                };
                SearchWindow.Open(new SearchWindowContext(
                    GUIUtility.GUIToScreenPoint(selectSquadButton.worldBound.position)),
                    provider);
            };
        }
        if (openSquadButton != null)
        {
            openSquadButton.SetEnabled(connection.squad != null);
            openSquadButton.clicked += () =>
            {
                if (connection.squad != null)
                {
                    SquadDefinitionEditor wnd = EditorWindow.GetWindow<SquadDefinitionEditor>();
                    wnd.titleContent = new GUIContent("Squad Editor");
                    wnd.LoadSquad(connection.squad);
                }
            };
        }

        // ── Roles section ──
        VisualElement rolesSection = content.Q<VisualElement>("roles-section");
        VisualElement rolesContainer = content.Q<VisualElement>("roles-container");
        Button addRoleButton = content.Q<Button>("add-role-button");
        bool hasRoles = connection.squad != null
            && connection.squad.availableRoles != null
            && connection.squad.availableRoles.Count > 0;

        if (rolesSection != null)
        {
            if (hasRoles)
            {
                BuildRoleRows(rolesContainer, connection);
                if (addRoleButton != null)
                {
                    addRoleButton.clicked += () =>
                    {
                        RoleSearchProvider provider = ScriptableObject.CreateInstance<RoleSearchProvider>();
                        provider.availableRoles = connection.squad.availableRoles;
                        provider.excludeRoles = new HashSet<string>(connection.assignedRoles);
                        provider.onRoleSelected = roleName =>
                        {
                            connection.assignedRoles.Add(roleName);
                            EditorUtility.SetDirty(currentTree);
                            BuildRoleRows(rolesContainer, connection);
                        };
                        SearchWindow.Open(new SearchWindowContext(
                            GUIUtility.GUIToScreenPoint(addRoleButton.worldBound.position)),
                            provider);
                    };
                }
            }
            else
            {
                rolesSection.RemoveFromHierarchy();
            }
        }

        // ── Bindings section ──
        VisualElement bindingsSection = content.Q<VisualElement>("bindings-section");
        VisualElement bindingsPlaceholder = content.Q<VisualElement>("bindings-editor-placeholder");
        Button addBindingButton = content.Q<Button>("add-binding-button");
        if (bindingsSection != null && bindingsPlaceholder != null)
        {
            if (connection.squad != null)
            {
                SquadBindingGroup bindingGroup = connection.squad.GetOrCreateBindingGroup(currentTree);
                if (bindingGroup.bindings == null)
                    bindingGroup.bindings = new List<VariableBinding>();

                SquadDefinition capturedSquad = connection.squad;
                BindingGroupEditor bindingsEditor = new BindingGroupEditor(
                    bindingGroup,
                    currentTree?.BlackboardDefinition,
                    capturedSquad.blackboardDefinition,
                    () =>
                    {
                        EditorUtility.SetDirty(currentTree);
                        EditorUtility.SetDirty(capturedSquad);
                        BindingGroupEditor.NotifyBindingsChanged(capturedSquad, this);
                    },
                    bindingRowTemplate,
                    null,
                    null,
                    null,
                    showAddButton: false);

                int placeholderIndex = bindingsPlaceholder.parent.IndexOf(bindingsPlaceholder);
                bindingsPlaceholder.parent.Insert(placeholderIndex, bindingsEditor);
                bindingsPlaceholder.RemoveFromHierarchy();

                if (addBindingButton != null)
                {
                    addBindingButton.clicked += () =>
                    {
                        if (bindingGroup.bindings == null)
                            bindingGroup.bindings = new List<VariableBinding>();

                        bindingGroup.bindings.Add(new VariableBinding
                        {
                            treeVariableName = null,
                            squadVariableName = null,
                            direction = BindingDirection.Both
                        });
                        EditorUtility.SetDirty(currentTree);
                        EditorUtility.SetDirty(capturedSquad);
                        bindingsEditor.Rebuild();
                    };
                }
            }
            else
            {
                bindingsSection.RemoveFromHierarchy();
            }
        }

        // ── Remove connection button ──
        Button removeConnectionButton = content.Q<Button>("remove-connection-button");
        if (removeConnectionButton != null)
        {
            removeConnectionButton.clicked += () =>
            {
                currentTree.squadConnections.RemoveAt(connectionIndex);
                EditorUtility.SetDirty(currentTree);
                RebuildUI();
            };
        }

        foldout.Add(content);
        return foldout;
    }

    private void BuildRoleRows(VisualElement container, SquadConnection connection)
    {
        container.Clear();

        if (connection.assignedRoles == null || connection.assignedRoles.Count == 0)
        {
            Label noRolesLabel = new Label("No roles assigned.")
            {
                style =
                {
                    color = new Color(0.5f, 0.5f, 0.5f, 1f),
                    fontSize = 11,
                    paddingLeft = 8
                }
            };
            container.Add(noRolesLabel);
            return;
        }

        for (int i = 0; i < connection.assignedRoles.Count; i++)
        {
            int capturedIndex = i;
            string roleName = connection.assignedRoles[i];

            VisualElement roleRow = assignedRoleRowTemplate != null
                ? assignedRoleRowTemplate.CloneTree()
                : new VisualElement();

            VisualElement colourBanner = roleRow.Q<VisualElement>("role-colour-banner");
            if (colourBanner != null)
            {
                Color roleColour = Color.gray;
                if (connection.squad != null && connection.squad.availableRoles != null)
                {
                    for (int j = 0; j < connection.squad.availableRoles.Count; j++)
                    {
                        if (connection.squad.availableRoles[j].name == roleName)
                        {
                            roleColour = connection.squad.availableRoles[j].colour;
                            break;
                        }
                    }
                }
                colourBanner.style.backgroundColor = roleColour;
            }

            Label roleNameLabel = roleRow.Q<Label>("role-name-label");
            if (roleNameLabel != null)
                roleNameLabel.text = roleName;

            Button removeButton = roleRow.Q<Button>("role-remove-button");
            if (removeButton != null)
            {
                removeButton.clicked += () =>
                {
                    connection.assignedRoles.RemoveAt(capturedIndex);
                    EditorUtility.SetDirty(currentTree);
                    BuildRoleRows(container, connection);
                };
            }

            container.Add(roleRow);
        }
    }

    private void OnAddConnectionClicked()
    {
        if (currentTree == null) return;

        if (currentTree.squadConnections == null)
            currentTree.squadConnections = new List<SquadConnection>();

        currentTree.squadConnections.Add(new SquadConnection());
        EditorUtility.SetDirty(currentTree);
        RebuildUI();
    }

    private void OnBindingsExternallyChanged(SquadDefinition squad, object source)
    {
        if (source == this) return;
        if (currentTree?.squadConnections == null) return;
        for (int i = 0; i < currentTree.squadConnections.Count; i++)
        {
            if ((Object)currentTree.squadConnections[i].squad == (Object)squad)
            {
                RebuildUI();
                return;
            }
        }
    }
}
