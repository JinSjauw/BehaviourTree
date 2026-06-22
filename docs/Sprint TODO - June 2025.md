# Sprint TODO — June 2025

**Playtest with designers:** 19th–21st June  
**Commander testing:** Exposure only  

---

## Summary

| # | Section | Status |
|---|---|---|
| 1 | Conditional Aborts | ✅ Done |
| 2 | GameObject → Add TreeRunner + BehaviourTree Button | ⬜ Pending |
| 3 | Overhaul Graph Inspector UI | ✅ Done |
| 4 | Commander Module UX Improvements | ✅ Done |
| 5 | Commander Nodes (Hardcoded for Speed) | 🔄 In Progress |
| 6 | NodeView Visual Overhaul (UI Toolkit) | ✅ Done |
| 7 | Smooth Blackboard Add-Variable UI | ✅ Done |
| 8 | Rewrite Node Palette | 🔄 In Progress |
| 9 | Bugs & Polish Before Playtest | 🔄 In Progress |
| 10 | Graph Editor Sticky Notes | ✅ Done |

---

## 1. Conditional Aborts ✓

- [x] Add `AbortType` enum (None, Self, LowerPriority, Both)
- [x] Add `abortType` field to `CompositeNode` (editor asset)
- [x] Add `abortType` field to `NodeData` (runtime struct)
- [x] Add `lastConditionResult[]` to `TickContext`
- [x] Implement `EvaluateLeafCondition` — evaluates a single method-bearing node
- [x] Implement `EvaluateCompositeCondition` / `FindFirstCondition` — recursive with abort-type constraint
- [x] Implement `CheckConditionalAbort` — Self pass (direct leaves) + LowerPriority pass (child composites)
- [x] Implement `AbortSubtree` — recursive state reset to INACTIVE
- [x] Implement `HasValidConditionForAbort` — editor validation pass
- [x] Wire into `SequenceMethod`, `SelectorMethod`, `PriorityMethod`, `ParallelMethod`
- [x] Allocate `lastConditionResult[]` in `TreeEvaluator`
- [x] Copy `abortType` from `CompositeNode` to `NodeData` in `TreeBaker`
- [x] Add `AbortType` dropdown in composite node inspector
- [x] Show editor warning when abort type is set but no reachable condition exists
- [x] Test: Self abort (condition fails → action aborted within same composite)
- [x] Test: LowerPriority abort (condition becomes true → sibling aborted under same parent)

---

## 2. GameObject → Add TreeRunner + BehaviourTree Button

**Target:** Before 21st June

### 2.1 Asset Menu — Tree Selection & Navigation

- [ ] **Recent trees menu** — cycle through the 5 most recently opened trees (MRU list persisted per-user)
- [ ] **Scene TreeRunner dropdown** — lists all active `TreeRunner` components in the current scene, select to focus that runner's tree in the editor
- [ ] **Selected GameObject info** — asset menu bar displays the currently selected GameObject and its assigned tree asset
  - If the GameObject has a `TreeRunner` with an assigned tree: show tree name, click to open
  - If the GameObject has no tree component: show a **Create Tree** button and an extra context menu item to create + assign a new tree
  - Creating a tree auto-adds a `TreeRunner` component if needed, then assigns the tree asset

### 2.2 Lock Toggle

- [ ] **Lock toggle** in the asset menu bar — when enabled, blocks `PopulateView` / `RepopulateView` calls so the editor keeps the current tree asset ref even when selection changes
- [ ] Lock still allows tree reassignment during authoring transitions (editor → Play Mode, Play Mode → editor) so the runtime-debugged tree instance replaces the authored one correctly

### 2.3 Editor Menu / Component Context Menu

- [ ] Editor menu item or component context menu: **"Add BehaviourTree Runner"**
- [ ] Creates/assigns a `TreeRunner` component
- [ ] Optionally creates a new `BehaviourTreeAsset` if none exists
- [ ] Auto-assigns asset to runner
- [ ] Works on selected GameObject(s)

---

## 3. Overhaul Graph Inspector UI ✓

### 3.1 Blackboard Section (upper half)

- [x] Add tab system: **Blackboard** | **Tracked Variables** | **Commander**
- [x] **Blackboard tab** — current variable list (read/write)
- [x] **Tracked Variables tab** — shows `[BlackboardTrack]` annotated fields from agent components, mapping to blackboard variable names
- [x] **Commander tab** — commander tree related variables (shared variables, commander→agent mappings)

### 3.2 Node Inspector Section (lower half)

- [x] Selected node's properties (method name, field entries, abort type, etc.)
- [x] Replaces current separate inspector window
- [x] **Rename node** — editable name field in inspector. Default = method/type name. Custom name overrides display.
- [x] **Type subtitle** — when node is renamed from its default, show actual type as a smaller subtitle below the name (e.g. "WaitSeconds" under "Retreat Delay")

### 3.3 Layout

- [x] Vertical split: upper = blackboard tabs, lower = node inspector
- [x] Resizable splitter between sections

---

## 4. Commander Module UX Improvements

**Target:** Before 21st June

### 4.1 Type Hierarchy: AgentTreeAsset / CommanderTreeAsset

- [x] Split `BehaviourTreeAsset` into a base class with two subclasses:
  - `AgentTreeAsset` — identical to current tree behaviour, all existing nodes available
  - `CommanderTreeAsset` — largely the same structure but:
    - Commander-specific nodes accessible in addition to standard nodes (Suppress, Flank, Ambush, etc.)
    - Agent-only nodes that are coupled to a single agent are filtered out of the node palette
- [x] Keep existing `.asset` files backward-compatible (default to `AgentTreeAsset`)

### 4.2 Commander Blackboard Binding Tab

- [x] Add an extra tab to the commander tree editor (alongside Inspector / Blackboard)
- [x] Tab contains a dropdown listing every `BehaviourTreeAsset` in the project
- [x] Selecting an agent tree asset from the dropdown creates a **binding** between the two trees
- [x] In the binding, the user maps fields between the two blackboard schemas:
  - Commander blackboard variables ↔ Agent blackboard variables
  - Each binding row: commander var name → agent var name
- [x] Bindings are persisted on the `CommanderTreeAsset`

### 4.3 CommanderBindingBridge Component Integration

- [x] `CommanderBindingBridge` already exists on the same GameObject as the agent `TreeRunner`
- [x] Component holds a reference to a `CommanderTreeAsset`
- [x] On load / init:
  - Retrieves all existing bindings that this commander asset type has with this agent asset type
  - Loads those bindings into the runtime bridge
- [x] Any updates made on the component at runtime are reflected back into the commander asset's persisted bindings
- [x] Two-way sync: editing bindings in the commander editor tab updates the bridge; bridge runtime changes update the asset

### 4.4 Agent Registration & Per-Agent Array Access

- [x] Commander tree holds a **collection of registered agents** (dynamic at runtime, configured in editor)
- [x] When an agent registers (via `CommanderBindingBridge`), it is assigned an **agent ID** (index into the collection)
- [x] Commander blackboard variables with stride > 1 are backed by arrays: one slot per agent
  - Example: `AgentRole[8]` → stride=8 → 8 agents, each reads `AgentRole[agentID]`
  - Agent reads only its own slot via its assigned ID; commander reads/writes all slots
- [x] **Agents Data tab** in the commander editor:
  - Displays all per-agent array variables as a table (rows = agents, columns = variables)
  - Agent name/ID on the left, each variable column shows the value for that agent's slot
  - Row count matches the commander's `maxSize` (max agents)
  - Design-time defaults for each agent slot can be configured here
- [x] `CommanderBindingBridge` stores the assigned agent ID received on registration
- [x] Agent's `TreeRunner` uses the bridge's agent ID to offset into per-agent blackboard arrays

### 4.5 Polish

- [x] Clean up commander setup flow (create, assign, link)
- [x] Better error messages and validation (missing bindings, unlinked agents)
- [x] Visual indicators in tree view for commander-connected nodes
- [x] Streamline agent registration / deregistration

---

## 5. Commander Nodes (Hardcoded for Speed)

### 5.1 Suppress & Flank

- [ ] `SuppressTarget` — agent fires at target position without needing line of sight (keeps enemy pinned)
- [ ] `FlankTarget` — agent moves to a flanking position relative to target (offset calculated from leader position + angle)
- [ ] `SuppressAndFlank` composite — one agent suppresses while others flank

### 5.2 Ambush (Fallback)

- [ ] `AmbushAtPosition` — agents move to ambush positions around a trigger zone
- [ ] Trigger condition: enemy enters zone → ambush triggers
- [ ] Fallback: if ambush fails (enemy detected, timer expires) → fall back to regroup

### 5.3 Formation (Leader Offsets)

- [ ] `FormationMove` — agents move to offset positions relative to leader
- [ ] Simple formation types: Line, Wedge, Column, Circle
- [ ] Offsets calculated from leader position + formation parameters
- [ ] `AssignFormationRoles` — distribute agents to formation slots

### 5.4 Leader Fallback

- [ ] `DetectLeaderDeath` condition — checks if current leader is dead/missing
- [ ] `SelectNewLeader` action — picks new leader by priority (role, health, distance)
- [ ] `ReassignFormation` — recalculates formation with new leader

---

## 6. NodeView Visual Overhaul (UI Toolkit)

**Target:** Before 26th June

- [x] Replace `GraphView` node rendering with `USS`-styled `VisualElement` classes — flat node-body layout with #input/#text-container/#output
- [x] Redesign node visuals:
  - Title bar with colored type indicator (Composite = blue, Decorator = orange, Action = green, Condition = yellow)
  - Editable node title with methodName subtitle — Enter/blur commit, Undo support, inspector synced
  - Runtime status border with colored states (running/success/failure) and CSS transitions
  - Custom port elements (`BehaviourPort`) with separated hit area and visual connector cap, hover highlights, connection state classes
- [x] Add `.uss` stylesheets for consistent theming (dark theme, spacing, fonts, port styling with `BehaviourPort.uss`)
- [x] Node selection/hover state: border highlight on `#node-body` with `:hover`, `:selected`, `:hover:selected` pseudo-classes
- [ ] Add `BehaviourPort.uxml` for UI Builder-editable port template layout
- [ ] Animated transitions: running state pulse, abort flash, connection highlight animation
- [ ] Ensure existing node interaction (drag, select, context menu, double-click) works on new visual elements
- [ ] Backward compat: fallback to current visuals if UI Toolkit runtime not available
- [x] Abort type badge on composite nodes (e.g. "LP" for LowerPriority, "S" for Self) — also shows on leftmost condition leaf reachable by parent's conditional abort
- [x] Warning icon on nodes with unassigned/missing variables or invalid abort config, with tooltip
- [ ] Runtime status icon (separate element from border — idle/running/success/failure icon)

### 6.1 Graph Editor Notes (Sticky Notes) ✓

- [x] Add right-click context menu option: **Add Note**
- [x] Notes render as resizable, draggable colored boxes with text content
- [x] Double-click note to edit text (inline or popup)
- [x] Configurable note color
- [x] Notes are serialized as part of the `BehaviourTreeAsset` (not runtime — editor only)
- [x] Notes are purely visual — they do not affect tree execution
- [x] Example content: "This branch handles retreat when HP < 30%", "TODO: add cooldown to Flank"

---

## 7. Smooth Blackboard Add-Variable UI ✓

- [x] Replace current type dropdown with search provider (like `NodeSearchProvider`)
- [x] Search filter for variable type name
- [x] Multi-choice toggle above search: **Single Value** / **Array (stride > 1)**
- [x] Selecting array mode prompts for stride count
- [x] Variable name field below type selection
- [x] Default value field (inline editor matching the selected type)

---

## 8. Rewrite Node Palette

### 8.1 Standard Offering (Generic)

- [ ] Decorators: Inverter, Repeater, Conditional (BB-based), Cooldown, Timeout
- [ ] Composites: Sequence, Selector, Priority, Parallel
- [ ] Actions: Wait, Log, SetVariable, MoveTo, RotateTowards
- [ ] Conditions: CompareInt, CompareFloat, CompareBool, IsNull, HasTarget
- [ ] Keep palette categories clean and flat — avoid deep nesting

### 8.2 Enemy Demo Palette (TreeCommanderTest)

- [ ] Commander-specific nodes: Suppress, Flank, Ambush, Formation, LeaderFallback
- [ ] Separate category or sub-category in search/palette
- [ ] Test tree setup for playtest scenario

---

## 9. Bugs & Polish Before Playtest

### 9.1 Subtree Fixes & Improvements

- [x] Subtree nodes must unfold and display their internal tree during runtime debugging — fixed `DebugProxiesAreSetup` flag gate + `currentRunner` null on direct asset selection
- [x] Verify conditional aborts work correctly inside subtrees — fixed Self pass, LP pass, and `FindFirstCondition` to traverse through SUBTREE descendants
- [x] Subtrees automatically copy necessary blackboard variables from the parent tree on creation — `CopyVariable` on `BlackboardDefinition` + `CopyReferencedBlackboardVariables` in `SubtreeExtractor`, auto-dedups shared variables
- [ ] Blackboard view refreshes when a subtree is created or opened (avoids stale display)
- [x] Add right-click option **Copy Selection Into Subtree** — creates a subtree from selected nodes without removing them from the parent tree (in addition to existing **Extract** which moves them)

### 9.2 General
- [x] Fix child sorting on composite nodes — children now sorted by X position after edge creation, paste, and search window connections
- [x] Fix BehaviourTreeEditor not wiring PollDebugState when window opens during already-running Play Mode
- [x] Fix status-border z-ordering — moved behind node-body in UXML so debug colors don't cover port connectors
- [ ] Run through all open console warnings/errors, fix any that surfaced during sprint
- [ ] Validate tree bake with conditional abort enabled (no regressions)
- [ ] Validate commander multi-agent scenarios
- [ ] Quick smoke-test: create new tree, add nodes, bake, run, abort

---

## 10. Graph Editor Sticky Notes ✓

- [x] Add right-click context menu option: **Add Note**
- [x] Notes render as resizable, draggable colored boxes with text content
- [x] Double-click note to edit text (inline or popup)
- [x] Configurable note color
- [x] Notes are serialized as part of the `BehaviourTreeAsset` (editor only — not runtime)
- [x] Notes are purely visual — do not affect tree execution

---

## Timeline (Target)

| Date | Focus |
|---|---|
| Now → 16 Jun | ~~Conditional aborts~~, GameObject button, node palette rewrite |
| 16–18 Jun | ~~Inspector overhaul~~, ~~blackboard add-variable UI~~, commander nodes (hardcoded) |
| 18–21 Jun | Commander nodes, GameObject button, node palette rewrite, playtest prep |
| 19–21 Jun | **Playtest with designers** — fixes only |
| 21–26 Jun | NodeView UI Toolkit visual overhaul, commander module UX polish, remaining nodes |

---

## Progress Update (17 Jun)

**Completed since last update:**
- Expanded section 2 (TreeRunner menu, recent trees, lock toggle)
- Expanded section 4 (Commander UX: type hierarchy, binding tab, bridge integration, agent registration)
- Added section 9.1 (Subtree fixes & improvements)
- Node rename + type subtitle (section 3.2)
- Custom port elements with USS styling, hover/connection states (section 6)
- Node view UXML redesign: flat layout, editable TextField, status-border z-order fix
- Inspector-to-graph title sync via CustomNodeEditor flag
- Child sorting fixes on edge creation, paste, search window
- PlayMode init debug polling fix
- Context menu order fix

**Completed (18 Jun):**
- **Section 9.1:** Subtree runtime debug unfolding (`DebugProxiesAreSetup` flag), conditional aborts traversing SUBTREE descendants (Self pass, LP pass recursive helper, `FindFirstCondition`), auto-copy blackboard variables on subtree creation (`CopyVariable` + `CopyReferencedBlackboardVariables`), Copy Selection Into Subtree context menu option
- Fixed `abortType` not persisting through subtree extraction (missing field in `CloneNodeIntoAsset`)
- Fixed `abortType` not persisting through copy/paste (missing in `SerializedNodeData` + `CopyPasteHandler`)
- Fixed new nodes showing type name ("CompositeNode") instead of methodName — `nodeName` now cleared in all `Create*Node` methods
- Fixed context menu `NullReferenceException` from stale `Event.current` in node creation menu
- Fixed stale screen position in context menu search window — extracted shared `OpenNodeSearchAtScreenPosition` with `setCreationPosition` flag
