using System.Collections.Generic;
using BehaviourTree.Core;
using BehaviourTree.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// VisualElement shown in the "Squads" tab of the BehaviourTreeEditor.
/// Displays the current tree's SquadConnections — which squads it's connected to,
/// the assigned role, and the per-tree binding table from each squad.
/// </summary>
[UxmlElement("SquadTabView")]
public partial class SquadTabView : VisualElement
{
    // ── UI elements queried from UXML ────────────────────────
    private Label emptyStateLabel;
    private ScrollView connectionsScroll;
    private Button addConnectionButton;

    // ── State ────────────────────────────────────────────────
    private BaseEditorTreeAsset currentTree;

    public SquadTabView()
    {
        style.flexGrow = 1;

        // Load UXML
        string uxmlPath = BehaviourTreeEditorPaths.SquadTabViewUxml;
        VisualTreeAsset treeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
        if (treeAsset != null)
        {
            VisualElement ui = treeAsset.CloneTree();
            Add(ui);
        }

        // Query elements
        emptyStateLabel = this.Q<Label>("empty-state-label");
        connectionsScroll = this.Q<ScrollView>("connections-scroll");
        addConnectionButton = this.Q<Button>("add-connection-button");

        if (addConnectionButton != null)
            addConnectionButton.clicked += OnAddConnectionClicked;
    }

    /// <summary>
    /// Refreshes the tab with squad data from the given tree asset.
    /// </summary>
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
        Foldout foldout = new Foldout
        {
            text = connection.squad != null ? connection.squad.name : "No Squad Selected",
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

        // ── Row 1: Squad picker + Open button ──
        VisualElement headerRow = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                alignItems = Align.Center,
                marginBottom = 4
            }
        };

        ObjectField squadField = new ObjectField("Squad")
        {
            objectType = typeof(SquadDefinition),
            value = connection.squad,
            style = { flexGrow = 1, marginRight = 4 }
        };
        squadField.RegisterValueChangedCallback(evt =>
        {
            connection.squad = evt.newValue as SquadDefinition;
            connection.assignedRole = string.Empty;
            EditorUtility.SetDirty(currentTree);
            foldout.text = connection.squad != null ? connection.squad.name : "No Squad Selected";
            RebuildUI();
        });
        headerRow.Add(squadField);

        Button openSquadButton = new Button(() =>
        {
            if (connection.squad != null)
            {
                SquadDefinitionEditor.OpenWindow();
                SquadDefinitionEditor wnd = EditorWindow.GetWindow<SquadDefinitionEditor>();
                wnd.LoadSquad(connection.squad);
            }
        })
        {
            text = "Open",
            style = { width = 50, paddingLeft = 4, paddingRight = 4 }
        };
        openSquadButton.SetEnabled(connection.squad != null);
        headerRow.Add(openSquadButton);

        foldout.Add(headerRow);

        // ── Row 2: Role picker ──
        if (connection.squad != null && connection.squad.availableRoles != null && connection.squad.availableRoles.Count > 0)
        {
            VisualElement roleRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginBottom = 8
                }
            };

            Label roleLabel = new Label("Role")
            {
                style = { width = 60, color = new Color(0.7f, 0.7f, 0.7f, 1f) }
            };
            roleRow.Add(roleLabel);

            PopupField<string> rolePopup = new PopupField<string>(
                connection.squad.availableRoles,
                string.IsNullOrEmpty(connection.assignedRole) ? 0
                    : Mathf.Max(0, connection.squad.availableRoles.IndexOf(connection.assignedRole)))
            {
                style = { flexGrow = 1 }
            };
            rolePopup.RegisterValueChangedCallback(evt =>
            {
                connection.assignedRole = evt.newValue;
                EditorUtility.SetDirty(currentTree);
            });
            roleRow.Add(rolePopup);

            foldout.Add(roleRow);
        }

        // ── Bindings table (read-only summary from squad's binding group for this tree) ──
        if (connection.squad != null && connection.squad.bindingGroups != null)
        {
            SquadBindingGroup matchingGroup = connection.squad.GetBindingGroup(currentTree);
            if (matchingGroup != null && matchingGroup.bindings != null && matchingGroup.bindings.Count > 0)
            {
                Label bindingsHeader = new Label("Bindings")
                {
                    style =
                    {
                        fontSize = 11,
                        color = new Color(0.6f, 0.6f, 0.6f, 1f),
                        unityFontStyleAndWeight = FontStyle.Bold,
                        marginBottom = 2
                    }
                };
                foldout.Add(bindingsHeader);

                for (int i = 0; i < matchingGroup.bindings.Count; i++)
                {
                    VariableBinding binding = matchingGroup.bindings[i];
                    VisualElement bindingRow = new VisualElement
                    {
                        style =
                        {
                            flexDirection = FlexDirection.Row,
                            alignItems = Align.Center,
                            marginBottom = 1,
                            paddingLeft = 8,
                            paddingTop = 1,
                            paddingBottom = 1,
                            backgroundColor = new Color(0.16f, 0.16f, 0.16f, 1f)
                        }
                    };

                    Label treeVarLabel = new Label(binding.treeVariableName ?? "(none)")
                    {
                        style = { flexGrow = 1, color = new Color(0.7f, 0.7f, 0.9f, 1f), fontSize = 11 }
                    };
                    bindingRow.Add(treeVarLabel);

                    Label arrowLabel = new Label(
                        binding.direction == BindingDirection.ToSquad ? "→" :
                        binding.direction == BindingDirection.FromSquad ? "←" : "↔")
                    {
                        style =
                        {
                            width = 20,
                            unityTextAlign = TextAnchor.MiddleCenter,
                            color = new Color(0.5f, 0.5f, 0.5f, 1f),
                            fontSize = 12,
                            marginLeft = 2,
                            marginRight = 2
                        }
                    };
                    bindingRow.Add(arrowLabel);

                    Label squadVarLabel = new Label(binding.squadVariableName ?? "(none)")
                    {
                        style = { flexGrow = 1, color = new Color(0.9f, 0.7f, 0.7f, 1f), fontSize = 11 }
                    };
                    bindingRow.Add(squadVarLabel);

                    foldout.Add(bindingRow);
                }
            }
            else
            {
                Label noBindingsLabel = new Label("No bindings defined. Open the squad editor to add bindings for this tree.")
                {
                    style =
                    {
                        color = new Color(0.5f, 0.5f, 0.5f, 1f),
                        fontSize = 11,
                        whiteSpace = WhiteSpace.Normal,
                        paddingLeft = 8
                    }
                };
                foldout.Add(noBindingsLabel);
            }
        }

        // ── Remove button ──
        Button removeConnectionButton = new Button(() =>
        {
            currentTree.squadConnections.RemoveAt(connectionIndex);
            EditorUtility.SetDirty(currentTree);
            RebuildUI();
        })
        {
            text = "Remove Connection",
            style = { marginTop = 4, paddingLeft = 8, paddingRight = 8 }
        };
        foldout.Add(removeConnectionButton);

        return foldout;
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
}
