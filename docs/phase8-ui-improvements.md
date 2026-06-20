# Phase 8 — Commander Module UI Improvements

## Overview

This document details the current state of the commander/squad UI implementation and the improvements needed for Phase 8. The goal is to polish the squad editor, add a commander tab to the main editor, visually distinguish commander trees from agent trees, and limit commander-specific nodes to commander trees only.

---

## 1. SquadDefinitionEditor — Make Nicer

### Current State

The SquadDefinitionEditor at [SquadDefinitionEditor.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Editor/SquadDefinitionEditor.cs) is functional but visually sparse. It loads from [SquadDefinitionEditor.uxml](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Editor/UIDocuments/SquadDefinitionEditor.uxml) and provides:

- **Toolbar** with a `SquadBarMenu` (Create New Squad, Open Squad, Save Squad)
- **Schema section:** `CreateNewSquadButton` + `BrowseSquadButton` above a `BlackBoardView` (reuses the existing BB variable editor component)
- **Roles section:** `NewRoleField` + `AddRoleButton` + `RolesList` (simple X-deletable rows)
- **Bindings section:** `AddBindingGroupButton` (opens `TreeAssetSearchProvider`) + `BindingGroupsScroll` containing per-tree `Foldout`s with:
  - Tree variable dropdown (`PopupField<string>`)
  - Direction arrow (→ ← ↔)
  - Squad variable dropdown (`PopupField<string>`)
  - Direction `EnumField`
  - X remove button per binding
  - + Add Binding button per group
  - Remove Group button per group

**Layout:** Everything is in a single scrollable `ContentArea`. The `BlackBoardView` has a fixed height of 200px.

### Issues

1. **No visual hierarchy** — Schema, Roles, and Bindings sections run together. Headers exist but there's no section grouping or card styling.
2. **Schema section is confusing** — Two buttons ("Create New Squad" and "Browse...") appear while a squad is already loaded. These should only show in empty state.
3. **Roles section is rudimentary** — plain rows with X buttons, no drag-to-reorder, no role count indicator.
4. **Binding direction arrow** — `→` means TreeVar → SquadVar (ToSquad: tree writes to squad). But visually it reads left-to-right, which means "tree variable goes to squad variable." This is correct for `ToSquad`. For `FromSquad` it shows `←` meaning "squad variable goes to tree variable." This is correct. For `Both` it shows `↔`. The arrow direction is correct.
5. **No save indicator** — users don't know if changes are dirty.
6. **No "New Squad" empty state** — when no squad is loaded, the window is blank except for the toolbar.
7. **No preview of connected trees** — can't see which trees are using this squad.

### Changes Needed

#### 1.1 Empty State

When `currentSquad == null`, show:
```
┌─ Create New Squad ───────────────────────────┐
│                                               │
│    [Create New Squad]   [Browse...]           │
│                                               │
└───────────────────────────────────────────────┘
```

Hide the Schema/Roles/Bindings sections entirely. Move the `SquadNavRow` buttons into this empty state area and hide them when a squad is loaded.

#### 1.2 Section Card Styling

Wrap each section (Schema, Roles, Bindings) in a `VisualElement` with:
- Dark background `Color(0.18f, 0.18f, 0.18f, 1f)`
- Rounded corners `border-radius: 6px`
- Internal padding `8px`
- Margin-bottom between cards `8px`

Headers get slightly larger font and bold styling.

#### 1.3 Schema Section

- Remove `CreateNewSquadButton` and `BrowseSquadButton` from the nav row (move to empty state)
- Remove the fixed 200px height from `SquadBlackBoardView` — let it grow to fit
- Add a label showing the BB definition name: "Schema: CombatSquad_Schema" with an "Open" button next to it that opens the BB asset in the inspector
- Add an `isSquadData` toggle column in the BlackBoardView when used in squad context (see section 4)

#### 1.4 Roles Section

- Add role count badge: "Roles (3)"
- Style each role row with consistent background, padding, and hover highlight
- Add drag handles for reorder (or at minimum up/down arrow buttons)
- Add role validation — no duplicates, no empty strings

#### 1.5 Bindings Section

- Add tree count badge: "Tree Bindings (2)"
- Style binding rows consistently
- Group foldout header shows tree type icon (agent/commander) and tree name
- Add "Open Tree" button next to each group header
- Add "Remove All Bindings" button inside each group
- Add search/filter bar when there are many bindings (>10)

#### 1.6 Dirty State Awareness

- Call `EditorUtility.SetDirty(currentSquad)` on every edit (already done)
- Show `*` in the window title when there are unsaved changes
- Auto-save on domain reload via `OnDisable`

---

## 2. SquadTabView — Make Nicer + Editable Bindings

### Current State

The `SquadTabView` at [SquadTabView.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Editor/SquadTabView.cs) is a `[UxmlElement]` VisualElement loaded from [SquadTabView.uxml](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Editor/UIDocuments/SquadTabView.uxml). It lives in the Squads tab of the main BehaviourTreeEditor. It shows:

- **Empty state** — "Select a tree asset to view squad connections."
- **No-connections state** — "No squad connections. Add a squad below."
- **Per-connection foldouts** with:
  - Squad ObjectField picker + "Open" button
  - Role picker (`PopupField<string>`) — only for agent trees
  - **Read-only bindings table** — shows treeVariableName → squadVariableName labels from the squad's `SquadBindingGroup`
  - Remove Connection button
- **+ Add Squad Connection** button at bottom

The tab is refreshed whenever `BehaviourTreeEditor.OnSelectionChange()` fires, via `squadTabView.Refresh(currentTree)`.

### Issues

1. **Bindings are read-only** — users must open the SquadDefinitionEditor separately to edit bindings. This creates a disjointed workflow.
2. **Direction arrow correctness** — currently uses the same logic as SquadDefinitionEditor: `ToSquad` → `→`, `FromSquad` → `←`, `Both` → `↔`. This is correct.
3. **No inline binding creation** — can't add new bindings from the squads tab.
4. **Connection creation is bare** — clicking "+ Add Squad Connection" creates an empty connection with no squad. User must then pick one from the ObjectField. Should open a Squad search provider.
5. **No visual indication of binding status** — are bindings valid? Do variables still exist in the definitions?
6. **Role picker always visible** — should only appear for agent trees (already correct, but logic could be clearer).

### Changes Needed

#### 2.1 Editable Bindings in Squads Tab

The squads tab already has access to both `currentTree` and `connection.squad`. It can directly edit the `SquadBindingGroup` — it's the same shared data that the SquadDefinitionEditor edits. Changes:

- Replace the read-only label rows with the same editable widgets from SquadDefinitionEditor:
  - `PopupField<string>` for tree variable (filtered to `currentTree.BlackboardDefinition`)
  - Direction arrow (read-only text label)
  - `PopupField<string>` for squad variable (filtered to `connection.squad.blackboardDefinition`)
  - `EnumField` for `BindingDirection`
  - `X` remove button per binding
- Add "+ Add Binding" button inside each connection foldout
- Call `EditorUtility.SetDirty(connection.squad)` on changes (not `currentTree` — the bindings live on the squad)

#### 2.2 SearchWindow for Squad Connection

Replace the empty `AddConnectionClicked` behavior:
- Open a `SquadSearchProvider` instead of adding an empty connection
- On selection: validate the tree isn't already connected to this squad, then add a `SquadConnection` with the selected squad
- If the squad has no binding group for this tree, auto-create one

#### 2.3 Binding Validation

- When a binding's variable name doesn't exist in the target definition, show the dropdown selection as blank and color the row's background red/orange
- Add a tooltip explaining the issue: "Variable 'Health' not found in (tree/squad) definition"
- Re-validate on refresh

#### 2.4 Visual Improvements

- Card-style foldouts matching SquadDefinitionEditor
- Squad type badge (showing BlackboardDefinition asset name)
- Role picker only when `connection.squad.availableRoles.Count > 0` (already happens, but add a label when no roles defined: "No roles defined in squad")
- "Open Squad" button more prominent (icon?)

#### 2.5 Role Picker Scope

The role picker should ONLY appear for agent tree connections, never for commander tree connections in the squads tab. This is already the behavior — `connection.assignedRole` is set but only displayed if squad has roles. Make this more explicit:
- Check `currentTree is CommanderTreeAsset` → hide role row entirely
- Check `currentTree is AgentTreeAsset` → show role row

---

## 3. Commander Tab — Implement

### Current State

The main editor's [BehaviourTreeEditor.uxml](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Editor/UIDocuments/BehaviourTreeEditor.uxml) has a `CommanderTab` defined in the `TabView`:

```xml
<ui:Tab label="Commander" name="CommanderTab" style="flex-grow: 1;"/>
```

The tab is completely empty — no content inside. `BehaviourTreeEditor.ConfigureTabsForTreeType()` shows/hides it based on whether `currentRunner is CommanderTreeRunner`. But it has no content to show.

### Purpose

The Commander tab allows configuring commander-specific settings:
1. **Select a SquadDefinition** — which squad this commander orchestrates
2. **View/edit squad bindings** — same binding data as the SquadDefinitionEditor but focused on the commander's perspective
3. **Set `CommanderBlackboardDefinition.maxSize`** — the maximum agent count for stride allocation
4. **View isSquadData variables** — per-agent array variables in the commander BB

### Implementation

#### 3.1 Create CommanderTabView

New file: `Editor/CommanderTabView.cs` — `[UxmlElement("CommanderTabView")]` VisualElement, parallel to `SquadTabView`.

```csharp
[UxmlElement("CommanderTabView")]
public partial class CommanderTabView : VisualElement
{
    // UI elements
    private Label emptyStateLabel;
    private VisualElement squadSection;
    private ObjectField squadField;
    private ScrollView bindingsScroll;
    private IntegerField maxSizeField;
    
    private BaseEditorTreeAsset currentTree;
    private SquadDefinition currentSquad;
    
    public CommanderTabView() { ... }
    public void Refresh(BaseEditorTreeAsset treeAsset) { ... }
    
    // Builds editable bindings between commander and squad,
    // same dropdown-based rows as SquadDefinitionEditor
}
```

UXML: `Editor/UIDocuments/CommanderTabView.uxml`:
```xml
<ui:UXML>
  <ui:VisualElement style="flex-grow: 1; flex-direction: column; padding: 8;">
    <ui:Label name="empty-state-label" text="Select a commander tree asset." 
              style="color: grey; -unity-text-align: middle-center; flex-grow: 1;"/>

    <ui:VisualElement name="content-area" style="flex-grow: 1; flex-direction: column; display: none;">
      <!-- Squad selection -->
      <ui:Label text="Squad" style="font-size: 13px; -unity-font-style: bold; margin-bottom: 4px;"/>
      <ui:VisualElement style="flex-direction: row; align-items: center; margin-bottom: 8px;">
        <uie:ObjectField name="squad-field" object-type="BehaviourTree.Core.SquadDefinition" 
                          style="flex-grow: 1; margin-right: 4px;"/>
        <ui:Button text="Browse..." name="browse-squad-button" style="padding: 0 8px;"/>
      </ui:VisualElement>

      <!-- Max size -->
      <ui:Label text="Max Agents" style="font-size: 13px; -unity-font-style: bold; margin-bottom: 4px;"/>
      <uie:IntegerField name="max-size-field" style="margin-bottom: 12px;"/>

      <!-- Bindings -->
      <ui:Label text="Squad Bindings" style="font-size: 13px; -unity-font-style: bold; margin-bottom: 4px;"/>
      <ui:VisualElement style="flex-direction: row; margin-bottom: 4px;">
        <ui:Button text="+ Add Binding" name="add-binding-button"/>
      </ui:VisualElement>
      <ui:ScrollView name="bindings-scroll" style="flex-grow: 1;"/>
    </ui:VisualElement>
  </ui:VisualElement>
</ui:UXML>
```

#### 3.2 Wire into BehaviourTreeEditor

In [BehaviourTreeEditor.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Editor/BehaviourTreeEditor.cs):

- Query `CommanderTabView commanderTabView` from UXML
- In `OnSelectionChange()`, call `commanderTabView?.Refresh(currentTree)` alongside `squadTabView?.Refresh(currentTree)`
- `ConfigureTabsForTreeType()` already shows/hides the CommanderTab — add content visibility toggle

#### 3.3 Data Flow

CommanderTabView edits the same data as SquadTabView — `SquadConnection` entries on the tree asset, and `SquadBindingGroup` entries on the squad definition. Changes:
- `connection.squad` → stored in `currentTree.squadConnections` (set via ObjectField)
- `connection.assignedRole` → N/A for commander (no role assignment)
- `SquadBindingGroup.bindings` → stored in `currentSquad.bindingGroups` (edited inline)
- `CommanderBlackboardDefinition.maxSize` → stored on the commander's BB def

#### 3.4 Auto-Create Binding Group

When a squad is selected, auto-create a `SquadBindingGroup` for the commander tree if one doesn't exist:
```csharp
SquadBindingGroup group = squad.GetOrCreateBindingGroup(currentTree);
```

This mirrors the SquadDefinitionEditor behavior.

---

## 4. isSquadData Flag

### Current State

`BlackboardVariableBase` at [BlackboardVariableBase.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Core/BlackboardVariableBase.cs) already has:

```csharp
/// <summary>
/// If true, stride is managed dynamically at runtime by CommanderTreeRunner
/// when agents register/unregister. Used for per-agent squad data arrays.
/// </summary>
public bool isSquadData;
```

This flag was added in Phase 6. It is **not exposed in the editor**. There is no checkbox, no toggle, no visual indicator in `BlackBoardView`.

### Runtime Behavior

When `isSquadData == true`, `CommanderTreeRunner.ResizeSquadDataStrides()` sets the variable's stride to `Mathf.Max(1, registeredAgents.Count)`. The storage is resized via `ManagedBlackboardStorage.ResizeFromVariables()`. This is already implemented and tested in Phase 6.

### What Needs to Change

#### 4.1 Expose isSquadData in BlackBoardView

Add a checkbox column to the variable list when the BlackBoardView is showing a commander BB def:

```
[Name]        [Type]     [is Array]   [Stride]   [is Squad Data]   [Value]   [X]
Health        float      [x]          8          [x]               [0..7]    [X]
AgentRole     TacticalRole [ ]       1          [x]               [0..7]    [X]
TargetID      int        [ ]          1          [ ]               0         [X]
```

- The `[is Squad Data]` column is a `Toggle`
- Visible ONLY when editing a commander's `BlackboardDefinition` or a squad's `BlackboardDefinition` (since both support `isSquadData`)
- Hidden when editing an agent's `BlackboardDefinition` (agents don't have squad data)

#### 4.2 BlackBoardView Context Awareness

`BlackBoardView.BuildBlackboardView(BlackboardDefinition)` needs to know the **context**:
- Agent tree BB def → hide isSquadData toggle
- Commander tree BB def → show isSquadData toggle
- Squad BB def → show isSquadData toggle

Pass a `bool showSquadDataToggle` parameter or a `BlackBoardContext` enum:
```csharp
public enum BlackBoardContext
{
    Agent,
    Commander,
    Squad
}

public void BuildBlackboardView(BlackboardDefinition definition, BlackBoardContext context = BlackBoardContext.Agent)
```

#### 4.3 isSquadData + IsArray Interaction

When `isSquadData` is toggled on:
- `IsArray` is automatically set to `true`
- The `Stride` field shows the `maxSize` from `CommanderBlackboardDefinition` (or agent count hint for squad) as read-only/hint text
- If `isSquadData` is toggled off, `IsArray` remains `true` but `Stride` becomes editable

#### 4.4 Dynamic Resize on Agent Count Change

When agents register/unregister at runtime:
- `CommanderTreeRunner` already calls `ResizeSquadDataStrides(registeredAgents.Count)` 
- This sets `variable.Stride = newAgentCount` for all `isSquadData` variables
- Then calls `ManagedBlackboardStorage.ResizeFromVariables()` which rebuilds the `values[]` array

This is already working. No changes needed to the runtime path. The editor just needs to expose the flag.

---

## 5. Commander vs Agent Tree Visual Distinction

### Current State

- `AgentTreeAsset` and `CommanderTreeAsset` both inherit from `BaseEditorTreeAsset`
- Both are drawn identically in `BehaviourTreeEditorGraphView`
- The editor window has no visual differentiation
- `ConfigureTabsForTreeType()` hides the CommanderTab for agent runners

### Purpose

Make it immediately obvious whether the user is editing a commander tree or an agent tree. This helps prevent:
- Adding commander-only nodes (ForEachAgent, etc.) to an agent tree
- Expecting squad data in a non-commander context
- Confusion when switching between tree types

### Implementation

#### 5.1 Graph View Background Tint

Modify `BehaviourTreeEditorGraphView` to change the grid background color based on tree type:

```csharp
public void PopulateView(BaseEditorTreeAsset treeAsset)
{
    // ... existing population logic ...
    
    // Tint the grid background
    GridBackground gridBackground = this.Q<GridBackground>();
    if (gridBackground != null)
    {
        if (treeAsset is CommanderTreeAsset)
            gridBackground.style.backgroundColor = new Color(0.12f, 0.08f, 0.15f, 1f); // subtle purple tint
        else
            gridBackground.style.backgroundColor = new Color(0.12f, 0.12f, 0.12f, 1f); // neutral dark
    }
}
```

This gives the commander tree graph a subtle purple/warm tint while agent trees stay neutral dark.

#### 5.2 Title Bar Indicator

Add a tree type indicator to the graph's title area:

```
┌─ [C] CombatCommander (Commander) ───────────────────────┐
│                                                           │
│   ⋮ (tree nodes)                                          │
```

Agent trees show `[A]` prefix. Commander trees show `[C]` prefix in a small badge. The badge color matches the tint.

#### 5.3 Tab Visibility

All three tabs in the TabView adjust based on tree type:

| Tab | Agent Tree | Commander Tree |
|---|---|---|
| Shared (BlackBoardView) | Visible | Visible |
| Tracked | Visible | Visible |
| Squads | Visible | Visible |
| Commander | **Hidden** | **Visible** |

This is already partially implemented via `ConfigureTabsForTreeType()`.

#### 5.4 USS Style Variant

Add commander-specific USS rules:

```css
.commander-graph-background {
    background-color: rgb(30, 20, 38);
    --unity-background-image-scale-mode: stretch-and-scale;
}

.agent-graph-background {
    background-color: rgb(30, 30, 30);
}
```

Apply the appropriate class when populating the graph view.

---

## 6. Limit Commander-Specific Nodes to Commander Trees

### Current State

The node creation menu (right-click in graph or via the "+" button in node headers) lists all registered node types. There is no filtering based on tree type. A user can add a ForEachAgent node to an agent tree, and it would appear to work in the editor — only to fail at bake time or runtime.

### Node Categories

Commander-specific composite nodes (Phase 8 composites):

| Node | Commander Only? | Reason |
|---|---|---|
| ForEachAgent | Yes | Requires per-agent stride + agentOffset |
| ForEachRole | Yes | Requires AgentRole squad data |
| SelectAgent | Yes | Requires agentOffset + _targetAgentID |
| GetLowestAgent | Yes | Internally uses ForEachAgent + SelectAgent |
| GetHighestAgent | Yes | Internally uses ForEachAgent + SelectAgent |
| GetNearestAgent | Yes | Internally uses ForEachAgent + SelectAgent |

These should **only** appear in the node creation menu when editing a `CommanderTreeAsset`.

### Implementation

#### 6.1 NodeMethod Attribute Extension

Add an optional `treeType` filter to `[NodeMethod]`:

```csharp
// Core/NodeMethodAttribute.cs
public enum AllowedTreeType
{
    Any = 0,
    Agent = 1,
    Commander = 2
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class NodeMethodAttribute : Attribute
{
    public string methodName;
    public AllowedTreeType allowedTreeType = AllowedTreeType.Any;
    
    public NodeMethodAttribute(string methodName) { this.methodName = methodName; }
}
```

Commander-only nodes use:
```csharp
[NodeMethod("ForEachAgent", allowedTreeType = AllowedTreeType.Commander)]
```

#### 6.2 MethodRegistry Filtering

`MethodRegistry` already builds a list of available methods. Add a filter method:

```csharp
public static List<string> GetAvailableMethodNames(AllowedTreeType treeType)
{
    return registeredMethods
        .Where(m => m.allowedTreeType == AllowedTreeType.Any || m.allowedTreeType == treeType)
        .Select(m => m.methodName)
        .ToList();
}
```

#### 6.3 Editor Node Creation Menu

In `BehaviourTreeEditorGraphView` or the node search provider:

```csharp
AllowedTreeType treeType = currentTree is CommanderTreeAsset 
    ? AllowedTreeType.Commander 
    : AllowedTreeType.Agent;

List<string> availableMethods = MethodRegistry.GetAvailableMethodNames(treeType);
```

Filter the node creation dropdown to only show compatible methods.

#### 6.4 Validation on Existing Trees

If an agent tree somehow already has a commander-only node (e.g. via script, or because the attribute was changed post-creation):

- **Bake-time:** warn and skip the node
- **Editor:** show a red border on the node with a tooltip: "This node requires a Commander Tree"
- **Runtime:** `TreeEvaluator` constructor logs an error and skips unsupported nodes

---

## Implementation TODO

### SquadDefinitionEditor

- [ ] **Empty state** — show Create/Browse buttons only when `currentSquad == null`; hide schema/roles/bindings sections
- [ ] **Section card styling** — dark background, rounded corners, padding for each section
- [ ] **Schema section** — remove nav buttons from loaded state, add BB def name label + Open button
- [ ] **Roles section** — add count badge, consistent row styling, drag-to-reorder handles
- [ ] **Bindings section** — add count badge, "Open Tree" button per group, tree type icon
- [ ] **Dirty state** — `*` in title bar, auto-save on disable

### SquadTabView

- [ ] **Editable bindings** — replace read-only labels with dropdowns + EnumField
- [ ] **+ Add Binding inside each connection** — creates VariableBinding, saved to squad
- [ ] **SearchWindow for SquadConnection** — replace empty add with SquadSearchProvider
- [ ] **Binding validation** — orange/red background for invalid bindings, tooltip explanation
- [ ] **Card-style foldouts** — match SquadDefinitionEditor styling
- [ ] **Role picker guard** — explicitly hide for CommanderTreeAsset
- [ ] **Bindings edit calls `EditorUtility.SetDirty(connection.squad)`** — not currentTree

### CommanderTabView

- [ ] **Create `CommanderTabView.cs`** — UxmlElement VisualElement
- [ ] **Create `CommanderTabView.uxml`** — layout with squad picker, maxSize field, bindings scroll
- [ ] **Wire into BehaviourTreeEditor** — query CommanderTabView, call Refresh on selection change
- [ ] **Squad ObjectField** — pick SquadDefinition, auto-create binding group
- [ ] **MaxSize IntegerField** — edit commander BB's maxSize
- [ ] **Editable bindings** — same dropdown-based rows
- [ ] **Update `BehaviourTreeEditorPaths.cs`** — add CommanderTabViewUxml constant
- [ ] **Update `BehaviourTreeEditor.uxml`** — add `<CommanderTabView>` inside CommanderTab

### isSquadData Flag

- [ ] **BlackBoardView context parameter** — `BlackBoardContext` enum: Agent/Commander/Squad
- [ ] **Toggle column in BB view** — show isSquadData checkbox for Commander/Squad contexts
- [ ] **Auto-set IsArray** — when isSquadData toggled on, set IsArray = true
- [ ] **Stride read-only** — when isSquadData, stride field shows maxSize as hint
- [ ] **Update callers** — all `BuildBlackboardView()` calls pass context
  - `BehaviourTreeEditor`: `AgentTreeAsset` → Agent, `CommanderTreeAsset` → Commander
  - `SquadDefinitionEditor`: Squad context

### Editor Tint

- [ ] **GraphView background tint** — `PopulateView()` checks tree type, adjusts background color
- [ ] **Title indicator** — [C] or [A] badge in graph title area
- [ ] **USS rules** — commander/agent background classes
- [ ] **Tab visibility** — `ConfigureTabsForTreeType()` already handles CommanderTab show/hide

### Node Limiting

- [ ] **NodeMethodAttribute extension** — add `AllowedTreeType` field
- [ ] **Annotate commander-only nodes** — ForEachAgent, ForEachRole, SelectAgent, GetLowestAgent, GetHighestAgent, GetNearestAgent
- [ ] **MethodRegistry.GetAvailableMethodNames(treeType)** — filter by tree type
- [ ] **Node creation menu filter** — use filtered method list in search provider and context menu
- [ ] **Bake-time validation** — warn if commander-only node found in agent tree
- [ ] **Editor node visual warning** — red border + tooltip on unsupported nodes

---

## Files to Create

| File | Assembly | Purpose |
|---|---|---|
| `Editor/CommanderTabView.cs` | Editor | Commander tab content |
| `Editor/UIDocuments/CommanderTabView.uxml` | — | Commander tab layout |

## Files to Modify

| File | Change |
|---|---|
| `Editor/SquadDefinitionEditor.cs` | Empty state, card styling, section improvements, dirty state |
| `Editor/UIDocuments/SquadDefinitionEditor.uxml` | Section card containers, empty state area |
| `Editor/SquadTabView.cs` | Editable bindings, SearchWindow for connections, validation |
| `Editor/SquadTabView.uxml` | Card-style foldouts, edit-friendly layout |
| `Editor/BehaviourTreeEditor.cs` | CommanderTabView integration, ConfigureTabsForTreeType |
| `Editor/UIDocuments/BehaviourTreeEditor.uxml` | CommanderTabView element in CommanderTab |
| `Editor/BehaviourTreeEditorPaths.cs` | CommanderTabViewUxml constant |
| `Editor/BlackBoardView.cs` | Context parameter, isSquadData toggle column |
| `Editor/BehaviourTreeEditorGraphView.cs` | Background tint, title indicator, node menu filtering |
| `Core/NodeMethodAttribute.cs` | AllowedTreeType field |
| `Runtime/MethodRegistry.cs` | GetAvailableMethodNames filter |
| `Runtime/TreeBaker.cs` | Bake-time validation for commander-only nodes in agent trees |
| `Runtime/TreeEvaluator.cs` | Constructor warning for unsupported node/method combinations |
