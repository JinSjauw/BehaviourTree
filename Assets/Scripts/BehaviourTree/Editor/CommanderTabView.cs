using System.Collections.Generic;
using BehaviourTree.Core;
using BehaviourTree.Editor;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// VisualElement shown in the "Commander" tab of the BehaviourTreeEditor.
/// Same pattern as SquadTabView but manages a single squad connection —
/// a commander tree commands exactly one squad.
/// </summary>
[UxmlElement("CommanderTabView")]
public partial class CommanderTabView : VisualElement
{
    private Label emptyStateLabel;
    private VisualElement commanderContent;
    private BaseEditorTreeAsset currentTree;
    private VisualTreeAsset connectionRowTemplate;
    private VisualTreeAsset bindingRowTemplate;

    public CommanderTabView()
    {
        style.flexGrow = 1;

        VisualTreeAsset treeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
            BehaviourTreeEditorPaths.CommanderTabViewUxml);
        if (treeAsset != null)
        {
            VisualElement ui = treeAsset.CloneTree();
            Add(ui);
        }

        emptyStateLabel = this.Q<Label>("empty-state-label");
        commanderContent = this.Q<VisualElement>("commander-content");

        connectionRowTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
            BehaviourTreeEditorPaths.SquadConnectionRowUxml);
        bindingRowTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
            BehaviourTreeEditorPaths.BindingRowUxml);

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

    private SquadConnection GetOrCreateConnection()
    {
        if (currentTree.squadConnections == null)
            currentTree.squadConnections = new List<SquadConnection>();

        if (currentTree.squadConnections.Count == 0)
        {
            currentTree.squadConnections.Add(new SquadConnection());
            EditorUtility.SetDirty(currentTree);
        }

        return currentTree.squadConnections[0];
    }

    private void RebuildUI()
    {
        commanderContent.Clear();

        if (currentTree == null)
        {
            commanderContent.style.display = DisplayStyle.None;
            emptyStateLabel.text = "Select a tree asset to configure the commander squad.";
            emptyStateLabel.style.display = DisplayStyle.Flex;
            return;
        }

        SquadConnection connection = GetOrCreateConnection();
        commanderContent.style.display = DisplayStyle.Flex;
        emptyStateLabel.style.display = DisplayStyle.None;

        VisualElement rowContent = connectionRowTemplate != null
            ? connectionRowTemplate.CloneTree()
            : new VisualElement();

        // ── Squad picker row ──
        Button selectSquadButton = rowContent.Q<Button>("select-squad-button");
        Button openSquadButton = rowContent.Q<Button>("open-squad-button");

        if (selectSquadButton != null)
        {
            selectSquadButton.text = connection.squad != null ? connection.squad.name : "Select Squad...";
            selectSquadButton.clicked += () =>
            {
                SquadSearchProvider provider = ScriptableObject.CreateInstance<SquadSearchProvider>();
                provider.onSquadSelected = squad =>
                {
                    connection.squad = squad;
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

        // ── Bindings section ──
        VisualElement bindingsSection = rowContent.Q<VisualElement>("bindings-section");
        VisualElement bindingsPlaceholder = rowContent.Q<VisualElement>("bindings-editor-placeholder");
        Button addBindingButton = rowContent.Q<Button>("add-binding-button");

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

        // Commander has exactly one connection — hide the remove button
        Button removeConnectionButton = rowContent.Q<Button>("remove-connection-button");
        removeConnectionButton?.RemoveFromHierarchy();

        // Commander doesn't manage roles — hide the roles section from the shared template
        VisualElement rolesSection = rowContent.Q<VisualElement>("roles-section");
        rolesSection?.RemoveFromHierarchy();

        commanderContent.Add(rowContent);
    }

    private void OnBindingsExternallyChanged(SquadDefinition squad, object source)
    {
        if (source == this) return;
        if (currentTree?.squadConnections == null || currentTree.squadConnections.Count == 0) return;
        if ((Object)currentTree.squadConnections[0].squad == (Object)squad)
            RebuildUI();
    }
}
