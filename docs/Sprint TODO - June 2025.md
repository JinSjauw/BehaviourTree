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
| 4 | Commander Module UX Improvements | ⬜ Pending |
| 5 | Commander Nodes (Hardcoded for Speed) | ⬜ Pending |
| 6 | NodeView Visual Overhaul (UI Toolkit) | ⬜ Pending |
| 7 | Smooth Blackboard Add-Variable UI | ✅ Done |
| 8 | Rewrite Node Palette | ⬜ Pending |
| 9 | Bugs & Polish Before Playtest | ⬜ Pending |
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

- [ ] Editor menu item or component context menu: "Add BehaviourTree Runner"
- [ ] Creates/assigns a `TreeRunner` component
- [ ] Optionally creates a new `BehaviourTreeAsset` if none exists
- [ ] Auto-assigns asset to runner
- [ ] Works on selected GameObject(s)

---

## 3. Overhaul Graph Inspector UI ✓

### 3.1 Blackboard Section (upper half)

- [x] Add tab system: **Blackboard** | **Tracked Variables** | **Commander**
- [x] **Blackboard tab** — current variable list (read/write)
- [ ] **Tracked Variables tab** — shows `[BlackboardTrack]` annotated fields from agent components, mapping to blackboard variable names
- [ ] **Commander tab** — commander tree related variables (shared variables, commander→agent mappings)

### 3.2 Node Inspector Section (lower half)

- [x] Selected node's properties (method name, field entries, abort type, etc.)
- [x] Replaces current separate inspector window
- [ ] **Rename node** — editable name field in inspector. Default = method/type name. Custom name overrides display.
- [ ] **Type subtitle** — when node is renamed from its default, show actual type as a smaller subtitle below the name (e.g. "WaitSeconds" under "Retreat Delay")

### 3.3 Layout

- [x] Vertical split: upper = blackboard tabs, lower = node inspector
- [x] Resizable splitter between sections

---

## 4. Commander Module UX Improvements

- [ ] Clean up commander setup flow (create, assign, link)
- [ ] Better error messages and validation (missing bindings, unlinked agents)
- [ ] Visual indicators in tree view for commander-connected nodes
- [ ] Streamline agent registration / deregistration

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

- [ ] Migrate `BehaviourNodeView` from Unity IMGUI (`VisualElement` wrappers) to native UI Toolkit rendering
- [ ] Redesign node visuals:
  - Title bar with colored type indicator (Composite = blue, Decorator = orange, Action = green, Condition = yellow)
  - Abort type badge on composite nodes (e.g. "LP" for LowerPriority, "S" for Self)
  - Runtime status icon (idle/running/success/failure) with distinct colors
  - Port circles with hover highlights
- [ ] Replace `GraphView` node rendering with `USS`-styled `VisualElement` classes
- [ ] Add `.uss` stylesheet for consistent theming (dark theme, spacing, fonts)
- [ ] Node selection state: border highlight, subtle background change
- [ ] Animated transitions: running state pulse, abort flash, connection highlight
- [ ] Ensure existing node interaction (drag, select, context menu, double-click) works on new visual elements
- [ ] Backward compat: fallback to current visuals if UI Toolkit runtime not available

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
