# File Split Analysis — Behaviour Tree Editor

## Summary

Scan of all `.cs` files under `Assets/Scripts/` ranked by size and split potential.
22 source files + 3 test files exceed 300 lines.
Threshold for consideration: files with clear independent subsystems that can be extracted without breaking cohesion.

---

## Tier 1 — High-Value Split Candidates

Files with 500+ lines containing clearly identifiable independent subsystems.

### 1. [BlackBoard.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Core/BlackBoard.cs) — 528 lines, 16 methods

| Subsystem | Lines (approx) | Description |
|---|---|---|
| Init & core access | ~100 | `Initialize`, `GetTotalSlotCount`, `FindVariableIndex`, `GetBoxed`/`SetBoxed` |
| Serialized ref layout | ~180 | `BuildSerializedReferences`, `HasSameVariableLayout`, `SaveReferencesByName`, `SaveReferencesByNameFromSnapshot`, `RestoreReferencesByName`, `RemapRenamedReferences` |
| Value overrides | ~60 | `GetValueOverride`, `SetValueOverride`, `ClearValueOverride` |
| Reference slot tracking | ~30 | `IsReferenceSlotOverridden`, `SetReferenceSlotOverridden`, `ClearReferenceSlotOverridden` |
| Enum + fields | ~20 | `BlackBoardType` enum, serialized fields |

**Recommendation**: Extract `BlackBoard.Layout.cs` for the serialized-reference layout management subsystem (~180 lines). Optionally extract `BlackBoard.Overrides.cs` for the override subsystem (~90 lines). The layout subsystem is the strongest candidate — it handles snapshot comparison, name-based remapping, and reference restoration with no dependency on other parts of the class except `storage` and `serializedReferences`.

**Risk**: Low. `BuildSerializedReferences` is already called externally from `BlackBoardEditor` but the internal helpers are private. Extracting to a partial class is zero-risk.

---

### 2. [SquadDefinitionEditor.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Editor/SquadDefinitionEditor.cs) — 517 lines, 23 methods

| Subsystem | Lines (approx) | Description |
|---|---|---|
| Window lifecycle | ~80 | `CreateGUI`, `OnEnable`, `OnDisable`, `OnSelectionChange`, `OnProjectChanged` |
| Squad bar menu | ~90 | `BuildSquadBarMenu`, `CreateNewSquad`, `BrowseOpenSquad` |
| Scroll nesting utils | ~70 | `RegisterNestedScrollHandling`, `TryForwardScrollTo`, `IsDescendantOf` |
| Roles UI | ~100 | `BuildRolesUI`, `OnAddRoleClicked` |
| Binding groups UI | ~80 | `BuildBindingGroupsUI`, `OnAddBindingGroupClicked` |
| Misc | ~100 | `LoadSquad`, `RefreshUI`, `ClearUI`, `OnBindingsExternallyChanged` |

**Recommendation**: Extract `SquadDefinitionEditor.Roles.cs` (~100 lines) and/or `SquadDefinitionEditor.Menus.cs` (~90 lines) as partial class files. The scroll nesting utilities could also move to a shared `ScrollUtils.cs` helper.

**Risk**: Low. All extracted sections access `currentSquad`, `expandedRoleFoldouts`, and UI element references — partial class access is seamless.

---

### 3. [TreeBaker.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Runtime/TreeBaker.cs) — 569 lines, 17 methods

| Subsystem | Lines (approx) | Description |
|---|---|---|
| Main orchestration | ~90 | `BakeTree` — entry point that drives all subsystems |
| Instance management | ~120 | `GetEffectiveRoot`, `EnsureInstance`, `ProcessChildren` |
| Scope mapping | ~80 | `EnsureScopeMapping`, `GetScopeMap`, `ResolveSubtreeVarIndex` |
| Node data fill | ~90 | `FillNodeData`, `CopyGenericVariables` |
| Field packing | ~170 | `CountFieldDataForNode`, `IsArrayFieldEntry`, `PackFieldEntryWithArray`, `PackFieldEntry`, `GetConstantValue`, `ResolveFieldType` |
| Slot resolution | ~30 | `ResolveSlotOffset` |

**Recommendation**: Extract `TreeBaker.FieldPacking.cs` (~170 lines). The field packing subsystem compiles `NodeFieldEntry` lists into flat `FieldData[]` arrays and `boxedConstants`. It has a clear interface (takes `runtimeBbDef`, `scopeMap`, produces `FieldData[]` and `boxedConstants`) with minimal coupling to the rest of the baker.

**Risk**: Low. `PackFieldEntryWithArray` and `PackFieldEntry` are internal helpers called only from `FillNodeData`. Passing the necessary context as parameters is straightforward.

---

### 4. [BlackBoardView.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Editor/BlackBoardView.cs) — 515 lines, 17 methods

| Subsystem | Lines (approx) | Description |
|---|---|---|
| Top-level view | ~60 | Constructor, `BuildBlackboardView` orchestration |
| Creator UI | ~80 | `BuildCreatorUI`, type creation, new variable entry |
| ListView builder | ~200 | `BuildListView`, variable rendering, array element handling |
| Edit callbacks | ~100 | Rename, type change, reorder, removal handlers |
| Type sync | ~50 | `SyncVariableTypesToEnum`, `HandleRenames` |

**Recommendation**: Extract `BlackBoardView.ListBuilder.cs` (~200 lines). The ListView construction with all its `bindItem`/`unbindItem`/`makeItem` callbacks is self-contained UI-building code that doesn't interact with the creator or type-sync logic.

**Risk**: Medium. The list builder references `cachedDefinition`, `entryTemplate`, `arrayElementTemplate`, and `IsSquadContext` — all accessible via partial class.

---

## Tier 2 — Moderate Split Potential

Files with 300-500 lines. Cohesive but contain sections that could be separated.

### 5. [BehaviourTreeEditorGraphView.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Runtime/Tests/CommanderCompositeTests.cs) — 747 lines, ~48 methods

The single largest non-test file. A Unity `GraphView` subclass.

| Notable sections | Lines |
|---|---|
| Setup (grid, title, zoom) | ~100 |
| Node/edge creation + compatibility | ~120 |
| Graph change handling (undo, removal, move) | ~80 |
| View population + rebuild | ~120 |
| Debug visualization | ~80 |
| Search window | ~40 |
| Copy/paste bridge | ~30 |
| Subtree handling | ~40 |

**Why lower rank**: GraphView subclasses are inherently monolithic — most methods operate on the same `nodeViewDict`, `tree`, and `graphViewChanged` callback. Extracting partials would scatter highly coupled logic across files. The class has 48 methods but most are small (5-15 lines).

**Recommendation**: Only worth splitting if it crosses 1000+ lines. Currently acceptable as-is.

---

### 6. [BehaviourNodeView.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Editor/BehaviourNodeView.cs) — 417 lines, 29 methods

| Notable sections | Lines |
|---|---|
| Core setup (ports, title, color) | ~140 |
| Debug rendering (state badges) | ~60 |
| Abort icons | ~70 |
| Positioning + sorting | ~30 |
| Tooltips | ~40 |

**Why lower rank**: Most methods are short UI helpers on a single visual element. Splitting would not reduce cognitive load meaningfully.

**Recommendation**: Extract `BehaviourNodeView.Debug.cs` (~130 lines) if the debug/abort icon rendering grows beyond 200 lines.

---

### 7. [BehaviourTreeEditor.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Editor/BehaviourTreeEditor.cs) — 418 lines, 22 methods

Main editor window. Contains tab configuration, asset bar menu building, selection handling, and tree baker invocation.

**Why lower rank**: The largest chunk (~120 lines) is `BuildAssetBarMenu` — menu construction with many `AppendAction` calls. The rest is lifecycle/coordination. Menu building could be extracted but the benefit is marginal.

---

### 8. [RuntimeDebugManager.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Editor/RuntimeDebugManager.cs) — 404 lines, 14 methods

Cohesive class that manages runtime debug proxy nodes and edges in the graph view.

**Why lower rank**: All methods work together to build and maintain the debug overlay. No natural split boundary. Extracting the `EnsureProxyEdges`/`TryAddProxyEdge` section (~60 lines) would be artificial.

---

### 9. [BlackBoardEditor.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Editor/BlackBoardEditor.cs) — 345 lines, 5 methods

Custom `Editor` inspector with two large methods:

| Method | Lines |
|---|---|
| `OnInspectorGUI` | ~200 (orchestration + iteration) |
| `DrawReferenceSlotEditor` | ~50 |
| `DrawValueSlotEditor` | ~50 |
| `DrawTypedField` | ~40 |

**Why lower rank**: Only 5 methods. `OnInspectorGUI` is the bulk — it iterates over variables and delegates to `DrawReferenceSlotEditor`/`DrawValueSlotEditor`. The sub-editors are already private static methods. Not worth splitting 345 lines of inspector code.

---

### 10. [NodeMethod.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Core/NodeMethod.cs) — 334 lines, 14 methods

Abstract base class for all node methods. Contains:

- `CompileAccessors` — reflection-based IL generation
- `ReadFromBBGeneric` / `WriteToBBGeneric` — BB access dispatch
- `DeserializeFields` — field data parsing
- `ResolveInputsGeneric` / `WriteOutputsGeneric` — runtime BB read/write
- Abstract `Execute()` overloads

**Why lower rank**: All methods are tightly coupled to the abstract node method concept. Extracting any subsystem would require an additional abstract base or composition pattern — not a simple file split.

---

## Tier 3 — Test Files

Large test files that could be split by fixture.

### 11. CommanderCompositeTests.cs — 1087 lines

Runtime test file for commander composite nodes (ForEachAgent, SelectAgent).

**Recommendation**: Split into `CommanderForEachAgentTests.cs` and `CommanderSelectAgentTests.cs` by composite type.

---

### 12. BlackboardGameObjectIntegrationTests.cs — 726 lines

Integration tests for blackboard GameObject reference handling.

**Recommendation**: Split by test scenario group (e.g., `BlackboardSerializationTests.cs`, `BlackboardOverrideTests.cs`, `BlackboardLayoutChangeTests.cs`).

---

### 13. SquadInstanceTests.cs — 492 lines

Editor test file for squad instance behavior.

**Recommendation**: Acceptable at current size. Split only if it exceeds 600 lines.

---

## Quick Reference Table

| # | File | Lines | Tier | Est. Lines to Extract |
|---|---|---|---|---|
| 1 | `BlackBoard.cs` | 528 | 1 | ~180 (Layout) + ~90 (Overrides) |
| 2 | `SquadDefinitionEditor.cs` | 517 | 1 | ~100 (Roles) + ~90 (Menus) |
| 3 | `TreeBaker.cs` | 569 | 1 | ~170 (FieldPacking) |
| 4 | `BlackBoardView.cs` | 515 | 1 | ~200 (ListBuilder) |
| 5 | `BehaviourTreeEditorGraphView.cs` | 747 | 2 | — (acceptable as-is) |
| 6 | `BehaviourNodeView.cs` | 417 | 2 | — (acceptable as-is) |
| 7 | `BehaviourTreeEditor.cs` | 418 | 2 | — (acceptable as-is) |
| 8 | `RuntimeDebugManager.cs` | 404 | 2 | — (acceptable as-is) |
| 9 | `BlackBoardEditor.cs` | 345 | 2 | — (acceptable as-is) |
| 10 | `NodeMethod.cs` | 334 | 2 | — (too tightly coupled) |
| 11 | `CommanderCompositeTests.cs` | 1087 | 3 | ~500 (by composite type) |
| 12 | `BlackboardGameObjectIntegrationTests.cs` | 726 | 3 | ~350 (by scenario) |
| 13 | `SquadInstanceTests.cs` | 492 | 3 | — (acceptable as-is) |
