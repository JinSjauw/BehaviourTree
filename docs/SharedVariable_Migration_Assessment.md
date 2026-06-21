# SharedVariable Migration Assessment

## Behaviour Designer SharedVariable Architecture

### Core Design

Behaviour Designer replaces the traditional blackboard with a **distributed typed-variable** model:

```
SharedVariable<T>            -- generic base class with .Value property
  ├── SharedFloat            -- concrete: SharedVariable<float>
  ├── SharedInt              -- concrete: SharedVariable<int>
  ├── SharedBool             -- concrete: SharedVariable<bool>
  ├── SharedTransform        -- concrete: SharedVariable<Transform>
  ├── SharedGameObject       -- concrete: SharedVariable<GameObject>
  ├── SharedVector3          -- concrete: SharedVariable<Vector3>
  ├── SharedString           -- etc.
  └── (user-defined)         -- public class SharedMyType : SharedVariable<MyType> { ... }
```

### How Variables Are Used

**In task code**, variables are declared as public fields:

```csharp
public class IsTargetInRange : Conditional
{
    public SharedTransform target;    // typed, self-contained object
    public SharedFloat range;

    public override TaskStatus OnUpdate()
    {
        float dist = Vector3.Distance(transform.position, target.Value.position);
        return dist <= range.Value ? TaskStatus.Success : TaskStatus.Failure;
    }
}
```

**In the editor**, the inspector shows these fields. The user creates a named variable ("Target" of type Transform) in the Variables panel, then assigns it to the `target` field of both `IsTargetInRange` and `MoveToTarget` tasks. Both tasks hold a reference to the **same** `SharedTransform` instance. When `IsTargetInRange` writes `target.Value = someTransform`, `MoveToTarget` sees the change.

### Key Architectural Properties

| Property | BD SharedVariable | Current System |
|---|---|---|
| Storage model | Distributed: each var is an independent object | Centralized: flat slot array |
| Variable identity | By object reference (same instance = same variable) | By slot index (integer) |
| Name resolution | None at runtime; resolved at edit time via inspector | Bake-time conversion: name → slot offset |
| Type system | Concrete subclass per type (SharedFloat, etc.) | Generic BlackboardVariable<T> + [SerializeReference] |
| Node coupling | Node field IS the variable reference | Node field holds a slot index; BB is separate |
| Multi-agent | No native stride; each agent gets its own tree instance | Stride-based per-agent array in shared BB |
| Property binding | Variable Mappings (var → Component.property) | Tracked Bindings (Component.field → BB) |
| Cross-tree sharing | Global Variables (BehaviorManager singleton) | Commander BB merging + SquadInstance copy |
| Serialization | Unity-native [SerializeField] on concrete types | [SerializeReference] polymorphism |

---

## Current System Architecture (for contrast)

### Storage Path

```
BlackboardDefinition (ScriptableObject, authoring)
  └── List<BlackboardVariableBase> sharedVariables  [SerializeReference]
        └── BlackboardVariable<T> :  singleValue, arrayValues

TreeBaker.BakeTree()
  └── Merge self + commander defs → runtimeBbDef
  └── ResolveSlotOffset(varIndex) → slot offset (sum of preceding strides)
  └── Pack FieldData.value = slot offset

Runtime:
  ManagedBlackboardStorage:  object[] values (flat, indexed by slot)
  BlackBoard (MonoBehaviour): owns storage + overrides + currentAgentOffset
  FieldReader / FieldBinding:  blackboard.Get<T>(slotOffset)
```

### Key Design Decisions That Differ From BD

1. **Integer slot access**: All runtime BB reads/writes use integer slot indices, never names. Names are resolved at bake time.
2. **Stride**: Per-agent array variables allocate `stride` consecutive slots. Agent N reads `baseSlot + N`.
3. **BB merging**: Commander BB is appended to agent BB at bake time. `SquadInstance` copies bidirectionally via slot pair caching.
4. **Per-component overrides**: `BlackBoard` persists per-component overrides separately from the shared definition.
5. **[SerializeReference] polymorphism**: One list stores all variable types via Unity's polymorphic serialization.

---

## Disruption Assessment

### Tier 1: Foundational (Would Require Rewriting)

These are the core systems that the entire codebase is built on. Replacing them means essentially rebuilding the editor from scratch.

#### 1.1 `FieldBinding` / `NodeMethod` -- The Field-to-Variable Bridge

**Files**: [NodeMethod.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Core/NodeMethod.cs), all node method subclasses (~30+ files)

**Current**: Each `FieldBinding` stores a `bbSlotIndex` (integer). Runtime reads/writes via `bb.Get<T>(bbSlotIndex)`. Compiled expression trees for performance.

**BD model**: The node field IS the variable reference -- `public SharedTransform target`. There is no slot index. The value is accessed via `target.Value`.

**Impact**: Every `NodeMethod` subclass (~30+) has fields designed to hold `int` slot indices. All of these would need to be rewritten to hold `SharedVariable<T>` fields instead. The entire `FieldBinding` compile/reflect mechanism would be replaced by direct property access. This is roughly a **complete rewrite of the node method system**.

```
Current:   [SharedVar] int healthIndex;    →  bb.Get<int>(healthIndex)
BD-style:  public SharedInt health;        →  health.Value
```

This change cascades into:
- `FieldReader` (entire ref struct becomes obsolete)
- `FieldBinding` (entire class becomes obsolete)
- `FieldData` (packed union becomes obsolete)
- Expression-tree compilation code
- All method classes that read/write BB fields

#### 1.2 `TreeBaker` -- Bake-Time Slot Resolution

**File**: [TreeBaker.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Runtime/TreeBaker.cs) (642 lines)

**Current**: The central bake pass that merges BB definitions, resolves variable names to slot offsets, and packs `FieldData` entries.

**BD model**: No bake-time slot resolution is needed. Variables are assigned by reference in the editor. The tree asset directly serializes `SharedVariable` references.

**Impact**: TreeBaker's BB-related code (~400 lines: `CopyGenericVariables`, `PackFieldEntry`, `ResolveSlotOffset`, subtree scope mapping, commander BB merging) all becomes dead code. The bake pipeline fundamentally changes from "resolve names to integers" to "validate variable references exist."

#### 1.3 `ManagedBlackboardStorage` / `BlackBoard` -- Runtime Storage

**Files**: [ManagedBlackboardStorage.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Core/ManagedBlackboardStorage.cs) (285 lines), [BlackBoard.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Core/BlackBoard.cs) (598 lines)

**Current**: Flat `object[]` array indexed by slot. `GetVariableSlotRange()` computes (baseSlot, stride) by summing preceding variable strides.

**BD model**: No centralized storage. Each `SharedVariable` holds its own `.Value`. The tree/BehaviorSource holds a `List<SharedVariable>` for the variable catalog.

**Impact**: The entire `ManagedBlackboardStorage` class becomes obsolete. `BlackBoard` would shrink dramatically -- no storage management, no `BuildSerializedReferences`, no `Initialize()` with slot allocation. Its role reduces to holding a list of owned `SharedVariable` instances. The `.Value` property on each variable replaces all `Get<T>`/`Set<T>`/`GetBoxed`/`SetBoxed` methods.

#### 1.4 `BlackboardDefinition` / `BlackboardVariable<T>` -- Schema Definition

**Files**: [BlackboardVariableBase.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Core/BlackboardVariableBase.cs) (92 lines), [BlackboardVariable.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Core/BlackboardVariable.cs) (123 lines), [BlackBoardDefinition.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Core/BlackBoardDefinition.cs) (131 lines)

**Current**: `BlackboardDefinition` is a ScriptableObject with `[SerializeReference] List<BlackboardVariableBase>`. Variables store name, stride, typeName, default values.

**BD model**: `SharedVariable<T>` is the atomic unit. A list of concrete typed instances. No separate "definition" ScriptableObject needed -- variables are part of the tree asset.

**Impact**: These three files are completely replaced. The `[SerializeReference]` polymorphism approach (which required `Activator.CreateInstance` for unknown types, `variableTypeName` caching, and `BlackboardVariableJsonUtility` for custom types) is replaced by concrete subclass serialization (which Unity handles natively).

---

### Tier 2: Structural (Major Refactoring Required)

These systems are conceptually compatible with both models but would need significant structural rework.

#### 2.1 Commander System -- Per-Agent Arrays and BB Merging

**Files**: [CommanderTreeRunner.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Runtime/CommanderTreeRunner.cs) (272 lines), [SquadInstance.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Runtime/SquadInstance.cs) (173 lines), [CommanderTreeAsset.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Runtime/CommanderTreeAsset.cs), [ForEachAgentMethod.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Runtime/Methods/ForEachAgentMethod.cs)

**Current design**:
- Squad-data variables have `stride = agentCount`
- `ForEachAgent` sets `currentAgentOffset = agentIndex` → all slot accesses shift by agent ID
- `CommanderTreeRunner.ResizeSquadDataStrides()` dynamically resizes storage when agents join/leave
- `SquadInstance` copies data bidirectionally between squad BB and tree BB via slot pair caching

**BD model**: No native stride. Each agent would need its own tree instance (BD's approach) or a custom array extension.

**Impact**: This is the **hardest problem** in the migration. Options:

1. **Per-agent tree instances** (BD native approach): Each agent gets its own copy of the tree with its own variable instances. Commander would coordinate by writing to a shared "commander variable set." This is architecturally clean but a fundamental departure from the current shared-BB model. Memory cost scales with agent count × variable count.

2. **Array-valued SharedVariables**: Create `SharedVector3Array`, `SharedIntArray`, etc. with array backing. The commander tree reads/writes `variable.Value[agentIndex]`. This retains the stride-like behavior but requires custom SharedVariable subclasses and editor tooling for array display.

3. **Hybrid**: Keep a flat storage layer under the hood, with SharedVariables acting as typed views into it. Defeats much of the purpose of switching to BD-style variables.

**Disruption level**: Very high. This alone could be a dealbreaker if per-agent performance is critical.

#### 2.2 Per-Component Override System

**Files**: [BlackBoard.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Core/BlackBoard.cs) (override sections), [BlackboardValueOverride.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Core/BlackboardValueOverride.cs), [BlackBoardEditor.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Editor/BlackBoardEditor.cs)

**Current**: `BlackBoard` component has `valueOverrides` (name-based) and `overriddenReferenceSlots` (slot-index-based). Editor draws override/X buttons per slot.

**BD model**: Variable Mappings allow a `SharedVariable` to map to a `MonoBehaviour.property`. This is a GET/SET proxy -- reading `var.Value` returns the mapped property. This is more powerful (live property binding) but operates at the variable level, not the component level.

**Impact**: The entire override UI and persistence logic would need to be redesigned. BD's Variable Mappings operate on a per-variable basis (one binding per variable) rather than per-component-instance overrides. Supporting per-component value overrides on the same variable definition would require extending BD's model.

#### 2.3 Squad System

**Files**: [SquadDefinition.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Core/SquadDefinition.cs) (168 lines), [SquadInstance.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Runtime/SquadInstance.cs) (173 lines)

**Current**: Squads have their own `BlackboardDefinition`. `SquadInstance` resolves name-based bindings between squad BB and tree BB, caches `[squadSlot, treeSlot]` pairs, and copies by slot.

**BD model**: Squads would have their own set of `SharedVariable` instances. Cross-tree variable sharing is done through Global Variables (BehaviorManager singleton) or by having trees reference the same variable instance.

**Impact**: `SquadInstance`'s slot pair caching and boxed copy mechanism would be replaced by direct variable reference sharing. The `SquadBindingGroup` editor UI would need to change from "map squad variable to tree variable" to "assign squad variable instance to tree variable field." Conceptually simpler (no slot arithmetic) but a complete rewrite of the binding system.

#### 2.4 `IBlackBoardAccess` / Interface Contract

**File**: [IBlackBoardAccess.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Core/IBlackBoardAccess.cs) (17 lines)

**Current**: `Get<T>(int slot)`, `Set<T>(int slot, T value)`, `GetBoxed(int slot)`, `SetBoxed(int slot, object value)`.

**BD model**: No centralized interface. Variables are accessed directly by their typed `.Value` property.

**Impact**: This interface becomes obsolete. All code that depends on it (~81 files reference the blackboard system) would need to switch to direct variable access. The refactoring is mechanical but enormous in scope.

---

### Tier 3: Editorial (Rewrite Required)

#### 3.1 Blackboard Definition Editor

**File**: [BlackBoardView.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Editor/BlackBoardView.cs) (606 lines)

**Current**: UI Toolkit element showing the variable list in the graph editor. Supports add/delete/reorder, type selection via `VariableTypeSearchPopup`, stride editing.

**BD model**: A Variables panel showing a list of `SharedVariable` instances. Add via type picker. No stride.

**Impact**: Complete rewrite of the editor UI. The underlying data model changes from `BlackboardDefinition.sharedVariables` to `List<SharedVariable>` on the tree asset. The `VariableTypeSearchPopup` and type registry would need to map to concrete `SharedVariable<T>` subclasses.

#### 3.2 Component Inspector

**File**: [BlackBoardEditor.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Editor/BlackBoardEditor.cs) (390 lines)

**Current**: Custom inspector for `BlackBoard` component showing per-variable overrides with override/X buttons. Layout change detection and rebuild logic.

**BD model**: Component inspector shows Variable Mappings (variable → property bindings). No per-component value overrides in the same sense.

**Impact**: If per-component overrides are still needed, this must be rebuilt on top of a different model. If Variable Mappings replace overrides, the inspector changes completely.

#### 3.3 Node Field Editors

**Files**: `NodeInspectorViewEditors/*`, `ComponentMemberSearchProvider.cs`, `NodeSearchProvider.cs`

**Current**: Node inspectors show fields that can be toggled between "Constant" and "Variable" mode. Variable mode shows a dropdown of BB variable names, resolves to slot index.

**BD model**: Node inspectors show `SharedVariable` fields directly -- they display as object fields with a variable picker dropdown. No constant/variable toggle (the value IS the variable).

**Impact**: Complete rewrite of all node field editor UI. The styling, data binding, and interaction model change fundamentally.

#### 3.4 Blackboard Enum Generator

**File**: [BlackboardEnumGenerator.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Editor/BlackboardEnumGenerator.cs) (159 lines)

**Current**: Generates `BlackboardEnums.cs` with compile-time slot index constants per definition. Used by BB_Compare methods.

**BD model**: No slot indices to generate. Variables accessed by reference, not by index.

**Impact**: File becomes completely obsolete.

---

### Tier 4: Peripheral (Moderate Rework)

#### 4.1 BB Node Methods

**Files**: `BB_CompareMethods.cs`, `BB_CheckMethods.cs`, `BB_SetMethods.cs`, `BB_LogMethods.cs`

**Current**: These methods use `[SharedVar]` annotated fields that resolve to slot indices. `BB_CompareInt`, `BB_CompareFloat`, etc. read two slots and compare.

**BD model**: These would hold `SharedInt`, `SharedFloat` fields directly and compare `.Value`.

**Impact**: Each method class needs rewriting, but the logic structure (compare two values, set a value, log a value) stays the same. Mechanical but time-consuming.

#### 4.2 Tracked Bindings

**Files**: [BehaviourTreeRunnerBase.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Runtime/BehaviourTreeRunnerBase.cs) (tracked bindings section), `TrackedVariablesView.cs`

**Current**: `TrackedVariable` pushes Component.field values into BB before evaluation. Uses `IBlackboardDataProvider`.

**BD model**: Variable Mappings accomplish the same thing (pull/push between Component.property and SharedVariable.Value) but built-in.

**Impact**: The tracked bindings system is largely replaced by BD's Variable Mappings. Some custom binding logic may survive if mappings don't cover all use cases.

#### 4.3 Runtime Debugging

**Files**: `RuntimeDebugManager.cs`, `RuntimeDebugProvider.cs`

**Current**: Reads BB state (slot values, variable names) and displays in debug panel.

**BD model**: Iterates `SharedVariable` list, displays `variable.Name` and `variable.Value`.

**Impact**: The data source changes but the UI structure stays similar. Moderate rework.

#### 4.4 Serialization Utilities

**File**: [BlackboardVariableJsonUtility.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Core/BlackboardVariableJsonUtility.cs) (173 lines)

**Current**: Custom JSON serialization for `BlackboardVariable<T>` instances, needed because `[SerializeReference]` polymorphism complicates default serialization.

**BD model**: Concrete `SharedVariable<T>` subclasses serialize natively via Unity. No custom JSON needed.

**Impact**: File becomes completely obsolete.

#### 4.5 TickContext / TreeEvaluator

**Files**: [TickContext.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Runtime/TickContext.cs) (93 lines), [TreeEvaluator.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Runtime/TreeEvaluator.cs) (107 lines)

**Current**: `TickContext` carries a `BlackBoard` reference. `TreeEvaluator` holds pre-baked `nodeDatas` and `fieldDatas` arrays.

**BD model**: `TickContext` would carry a variable container (list of SharedVariable instances). `TreeEvaluator` would hold baked node data but no `fieldDatas` (since there are no slot offsets).

**Impact**: Structural changes but the evaluation loop stays the same. Moderate rework.

---

## File-by-File Impact Summary

| File | Disposition | Reason |
|---|---|---|
| `BlackboardVariableBase.cs` | **Delete** | Replaced by `SharedVariable<T>` |
| `BlackboardVariable.cs` | **Delete** | Replaced by concrete `Shared*` subclasses |
| `BlackBoardDefinition.cs` | **Delete** | Variables live on tree asset directly |
| `BlackBoard.cs` | **Major rewrite** | Shrinks to variable container + mapping host |
| `IBlackboardStorage.cs` | **Delete** | No centralized storage |
| `ManagedBlackboardStorage.cs` | **Delete** | No flat array |
| `IBlackBoardAccess.cs` | **Delete** | No integer slot API |
| `IBlackboardDataProvider.cs` | **Rewrite** | Variable Mappings replace it |
| `BlackboardValueOverride.cs` | **Delete/Redesign** | Variable Mappings or new override model |
| `BlackboardVariableJsonUtility.cs` | **Delete** | Native Unity serialization |
| `FieldTypeHelper.cs` | **Rewrite** | Maps to SharedVariable types |
| `NodeMethod.cs` | **Major rewrite** | Slot indices → SharedVariable fields |
| `TreeBaker.cs` | **Major rewrite** | Slot resolution → variable reference validation |
| `FieldReader.cs` | **Delete** | No FieldData to read |
| `TreeEvaluator.cs` | **Moderate** | Remove fieldData array |
| `TickContext.cs` | **Moderate** | BB ref → variable container ref |
| `TickDispatcher.cs` | **Minor** | Interface change |
| `BehaviourTreeRunnerBase.cs` | **Moderate** | Init flow, tracked bindings |
| `AgentTreeRunner.cs` | **Moderate** | Data provider → variable mapping |
| `CommanderTreeRunner.cs` | **Major rewrite** | Stride → per-agent instances or array vars |
| `SquadInstance.cs` | **Major rewrite** | Slot copy → variable sharing |
| `SquadDefinition.cs` | **Rewrite** | BB def → variable binding |
| `RuntimeBTreeAsset.cs` | **Moderate** | BB def → variable list |
| `RuntimeAssetHelper.cs` | **Moderate** | Bake orchestration |
| `CommanderTreeAsset.cs` | **Moderate** | CreateCommanderBlackboard → variable setup |
| `BlackBoardEditor.cs` | **Rewrite** | Override UI → mapping UI |
| `BlackBoardView.cs` | **Rewrite** | Definition editor → variable list editor |
| `BlackboardEnumGenerator.cs` | **Delete** | No slot indices |
| `VariableTypeRegistry.cs` | **Rewrite** | VariableBase types → SharedVariable types |
| `VariableTypeSearchPopup.cs` | **Rewrite** | Type picker → SharedVariable picker |
| `VariableSearchPopup.cs` | **Rewrite** | Slot resolution → variable reference |
| `NodeInspectorViewEditors/*` | **Rewrite** | Constant/variable toggle → direct var fields |
| `ComponentMemberSearchProvider.cs` | **Moderate** | Field binding → variable mapping |
| `TrackedVariablesView.cs` | **Rewrite** | Tracked bindings → variable mappings |
| `RuntimeDebugManager.cs` | **Moderate** | BB state → variable state |
| `RuntimeDebugProvider.cs` | **Moderate** | Same |
| `MethodMetadataCache.cs` | **Moderate** | Field type scanning |
| `NodeWarningEvaluator.cs` | **Moderate** | Variable existence checks |
| `SerializedNodeData.cs` | **Moderate** | Field serialization format |
| `CopyPasteHandler.cs` | **Minor** | Variable references in copy |
| `SubtreeExtractor.cs` | **Moderate** | Subtree variable scoping |
| `SubtreeCycleValidator.cs` | **Minor** | Unchanged |
| All `*Method.cs` (~30 files) | **Rewrite** | Slot fields → SharedVariable fields |
| `BehaviourTreeEditor.cs` | **Moderate** | BB routing |
| `BehaviourTreeEditorGraphView.cs` | **Moderate** | BlackBoardView host |
| `CommanderTabView.cs` | **Rewrite** | Commander BB editing |
| `SquadTabView.cs` | **Rewrite** | Squad BB editing |
| `BindingGroupEditor.cs` | **Rewrite** | BB def binding → variable binding |
| `TooltipRegistry.cs` | **Minor** | Documentation |
| `StandardMethods.Blackboard.cs` | **Rewrite** | BB_Compare* → variable access |

---

## Risk Assessment

### High-Risk Items

1. **Commander stride system**: No equivalent in BD. Requires either per-agent tree instances (memory cost) or custom array-valued SharedVariable extensions. This is the single biggest architectural incompatibility.

2. **Baking pipeline dependency**: The entire pipeline (TreeBaker → FieldData → FieldReader/FieldBinding → runtime) is built around slot offsets. Tearing it out means every node execution path changes.

3. **Per-component overrides**: BD's Variable Mappings are a different abstraction. If per-component value overrides are essential, BD's model must be extended.

4. **Editor tooling rewrite scope**: All four major editor subsystems (BlackBoardView, BlackBoardEditor, node field editors, variable pickers) need ground-up rewrites.

### Medium-Risk Items

5. **Serialization migration**: Existing `.asset` files use `[SerializeReference]` for `BlackboardVariable<T>`. Migrating to concrete `SharedVariable<T>` subclasses requires either a one-time migration tool or dual-path loading with deprecation.

6. **Node method API surface**: ~30+ method classes need field declarations changed. The logic within each method must switch from `bb.Get<int>(slotIndex)` to `variable.Value`. The mechanical volume is large but each file is simple.

7. **Squad binding model**: Switching from slot-pair copying to variable reference sharing changes how squads connect to trees.

### Low-Risk Items

8. **Runtime debugging**: Data source changes but display is analogous.
9. **Subtree scoping**: BD handles subtrees with variable inheritance; this may actually be simpler.
10. **Copy/paste**: Variable references survive copy naturally (same instance), unlike slot indices that need remapping.

---

## Estimated Scope

| Category | Files Affected | Complexity |
|---|---|---|
| Core storage (delete/replace) | 8 files | N/A (deletion) |
| Runtime BB access (major rewrite) | 6 files | Very High |
| Bake pipeline (major rewrite) | 3 files | High |
| Commander/Squad (major rewrite) | 5 files | Very High |
| Node methods (rewrite) | ~30 files | Medium (per file) |
| Editor UI (rewrite) | ~12 files | High |
| Peripheral (moderate) | ~10 files | Medium |
| **Total** | **~74 files** | |

This represents a near-total replacement of the blackboard subsystem. The migration is architecturally substantial enough that it should be evaluated as a new major version rather than an incremental change.

---

## Alternatives to Full Replacement

### Option A: Adapter Layer

Keep the existing `BlackBoard` / `ManagedBlackboardStorage` as the storage backend, but expose SharedVariable-like wrappers for the API surface. Node methods use `SharedInt health` but under the hood it proxies to `storage.Get<int>(slot)`.

**Pros**: Commander stride system preserved; incremental migration possible; lower risk.
**Cons**: Adds a layer of indirection; doesn't fully realize BD's simplicity; hybrid complexity.

### Option B: Selective Adoption

Use BD-style variables for the node method API only (developer-facing), keeping the centralized storage + stride system for the commander backend. The two systems bridge via an adapter.

**Pros**: Developer UX improvement without sacrificing multi-agent architecture.
**Cons**: Two systems to maintain; cognitive overhead.

### Option C: Fork BD's Model

Adopt SharedVariable<T> as the variable type but retain a centralized catalog (like the current BlackboardDefinition) and extend SharedVariable with a stride capability. This gives the API benefits without losing the multi-agent architecture.

**Pros**: Best of both worlds; retains stride; cleaner than adapter.
**Cons**: Forked implementation; diverges from BD standard; not compatible with BD assets.

---

## Performance Assessment

### Current System — Per-Operation Analysis

The current system uses a flat `object[] values` array in [ManagedBlackboardStorage.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Core/ManagedBlackboardStorage.cs). Every slot stores a boxed value.

| Operation | Value Types (int, float, Vector3, etc.) | Reference Types (GameObject, Transform) | Per-frame alloc? |
|---|---|---|---|
| `Get<T>(slot)` | Unbox via `val is T tVal` pattern match | Cast (identity check) | No |
| `Set<T>(slot, value)` | **Boxes onto heap** (`object[]` assignment) | Pointer store | **Yes — per value-type write** |
| `GetBoxed(slot)` | Returns existing boxed reference | Returns reference | No |
| `SetBoxed(slot, object)` | Stores pre-boxed reference (no new box) | Stores reference | No |
| `SquadInstance.CopyToBB/CopyFromBB` | Copies pre-boxed references between slots | Copies references | No (boxes exist from original writes) |

**Key insight**: The `SquadInstance` copy loop (which runs per frame for every squad-tree pair) does NOT allocate because it moves already-boxed references via `GetBoxed`/`SetBoxed`. The allocation happened during the original `Set<T>` call that wrote the value.

**Expression tree compilation** ([NodeMethod.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Core/NodeMethod.cs#L63-L101)):
- Runtime IL generation via `Expression.Compile()` — one-time cost at tree init
- Resulting delegates bypass `GetBoxed`/`SetBoxed` but still go through `ManagedBlackboardStorage.Get<T>/Set<T>`, so boxing still occurs
- On IL2CPP/AOT platforms: falls back silently to reflection path (`ReadFromBBGeneric`/`WriteToBBGeneric` — much slower, with `Convert.ChangeType` + `FieldInfo.SetValue`)

**Memory layout**:
- `object[] values` — scattered heap references, poor cache locality for value types
- No per-type sub-arrays — an `int` value sits as a boxed heap object next to a `GameObject` reference
- `Type[] slotTypes` and `BlackboardSlotKind[] slotKinds` — two parallel arrays, read on every `Set<T>` call for type validation

**Stride performance**:
- `currentAgentOffset` is a simple integer add at access time ([BlackBoard.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Core/BlackBoard.cs#L456)) — zero-cost for locality
- All N agents share one array — excellent cache behavior when iterating agents
- `ResizeSquadDataStrides` allocates three new arrays on every agent join/leave

### BD SharedVariable — Performance Characteristics

BD's marketing claims "zero runtime allocations after startup." This implies:
1. All `SharedVariable<T>` instances are pre-allocated at init time
2. Value-type SharedVariables box once at creation
3. No allocations occur during `variable.Value` get/set

**Likely architecture** (based on source code analysis and documentation):

| Aspect | Regular BD | BD Pro (DOTS) |
|---|---|---|
| Storage | `SharedVariable<T>` heap objects (one per variable per tree) | `DynamicBuffer<SharedVariableElement>` per Entity |
| Value-type storage | Boxed in the `SharedVariable<T>` object (heap) | Unmanaged value stored directly in the buffer |
| Access | `variable.Value` property (read: unbox; write: box or reuse) | `buffer.Get<T>(index)` / `buffer.Set(index, value)` (burstable) |
| Per-agent | Separate tree instances (separate variable copies) | Separate entities with their own `DynamicBuffer` |
| Global variables | `BehaviorManager` singleton with dictionary lookup | Shared `DynamicBuffer` or singleton entity |
| Variable Mappings | `GetComponent<T>().property` — reflection cost | Burst-compatible property access via bakers |

**BD Regular performance concerns:**
- Each `SharedVariable<T>` is a separate heap object — scattered memory, poor cache
- Accessing `variable.Value` for value types likely unboxes (if stored as `object` internally)
- Variable Mappings use `GetComponent` + reflection — non-trivial per-access cost
- Per-agent tree instances mean N × M variable objects for N agents with M vars — memory blowup
- Global variable access goes through `BehaviorManager` dictionary — O(1) but with hash overhead

**BD Pro performance:**
- `DynamicBuffer<SharedVariableElement>` per entity — contiguous memory, cache-friendly
- Values stored unboxed in buffer — no boxing, no GC
- Burst-compiled systems for all tree evaluation
- Entity baking pre-processes authoring data — no runtime reflection
- Variable index is a stable integer — same slot-index pattern as the current system

### Comparative Summary

| Metric | Current System | BD Regular (non-DOTS) | BD Pro (DOTS) |
|---|---|---|---|
| Cache locality (contiguous) | **Good** — flat `object[]` | Poor — scattered heap objects | **Excellent** — `DynamicBuffer` |
| Boxing for value types | **Every Set** | **Every Set** (if boxed internally) | **None** — unmanaged storage |
| GC allocations per frame | Proportional to value-type writes | Proportional to value-type writes | **Zero** (after init) |
| Multi-agent memory | **Single array** + stride | **N × tree memory** | **N × entity buffer** |
| Multi-agent cache behavior | **Excellent** — agents adjacent in array | Poor — separate tree instances | **Excellent** — separate entities, Burst batches |
| IL2CPP / AOT safety | **Partial** — delegates fail, falls back to reflection | Unknown (likely similar) | Full — Burst native |
| Burst compatibility | **No** — object[], reflection, Expression trees | **No** — heap objects, reflection | **Yes** — native code |
| Variable access cost | Slot offset + unbox (no name lookup) | Object field read + unbox (no name lookup) | Buffer index + unbox (no name lookup) |
| Squad/commander data sharing | **Zero-copy** via slot reference sharing | Per-instance copy required | Per-entity copy required |

### Performance Verdict

**For GameObject-based (non-DOTS) scenarios**, the current system is **performance-competitive with BD Regular** and has a structural advantage for multi-agent scenarios thanks to stride-based array sharing. However, the **boxing on every value-type Set** is a real cost both systems share. The current system could be meaningfully improved by replacing `object[] values` with typed arrays (e.g., `int[] intValues`, `float[] floatValues`) without changing the architecture.

**For DOTS scenarios**, the current system is **not usable** as-is. BD Pro wins outright because it was built for DOTS from the ground up.

---

## DOTS Conversion Analysis

### What Behaviour Designer Pro Does

BD Pro takes the following approach for its DOTS backend:

1. **Entity Baking**: The `BehaviorTree` MonoBehaviour is baked into an Entity. Each node becomes an `IBufferElementData` in a `DynamicBuffer`. Variables become `SharedVariableElement` entries in a separate `DynamicBuffer`.

2. **ECS Systems**: Tree evaluation runs entirely in ECS systems (not MonoBehaviour Update). Systems query entities with `DynamicBuffer<TaskComponent>`, `DynamicBuffer<BranchComponent>`, and `DynamicBuffer<SharedVariableElement>`.

3. **ECS Variable Registry**: During baking, each `SharedVariable<T>` field on a task is registered with an `ECSVariableRegistry`, which assigns a stable integer index into the `SharedVariableElement` buffer. The task's baked component stores this index.

4. **Burst-compiled variable access**: `buffer.Get<T>(index)` and `buffer.Set(index, value)` — typed, burstable, no boxing.

5. **Per-agent entities**: Each agent is a separate Entity with its own set of `DynamicBuffer` instances (its own "blackboard"). No stride concept — per-agent data isolation by entity boundary.

6. **Entity Tasks** vs **GameObject Tasks**: BD Pro supports both. GameObject tasks use `[SharedVariable<T>]` fields and run on the main thread. Entity tasks use `ECSSharedVariableIndex<T>` and run in Burst-compiled jobs.

### Current System DOTS Readiness Assessment

#### Already DOTS-Compatible

| Component | Why |
|---|---|
| `FieldData` (struct, blittable) | `[StructLayout(LayoutKind.Explicit)]` with `byte` + `int` — can become `IBufferElementData` |
| `NodeData` (presumed blittable struct) | Can become `IBufferElementData` |
| Slot offset pattern | Stable integer indexing is exactly what DOTS buffers need |
| Bake-time name → index resolution | Same principle as Entity Baking — pre-process authoring data into runtime indices |
| Stride-based per-agent array | Conceptually maps to per-agent entities or a shared buffer with offset metadata |

#### Must Be Replaced

| Component | Problem | Replacement |
|---|---|---|
| `object[] values` | Not blittable, not burstable | `DynamicBuffer<BlackboardValue>` (union type) or typed `DynamicBuffer<T>` per value type |
| `ManagedBlackboardStorage` (class) | Reference type, uses heap arrays | ECS buffer system — storage is entity data |
| `BlackBoard` (MonoBehaviour) | GameObject component | Baker + `IComponentData` + system |
| `IBlackBoardAccess` | Uses generics + boxing | Burst-compatible buffer access via `DynamicBuffer<T>` |
| `FieldBinding.CompileAccessors` | Expression trees — not AOT/burst safe | Baked index direct access — no runtime codegen |
| `FieldBinding` reflection fallback | `Convert.ChangeType`, `FieldInfo.SetValue` — not burstable | Removed — DOTS requires compile-time known types |
| `SquadInstance` boxed copy | `GetBoxed`/`SetBoxed` on `object[]` | ECS system with `DynamicBuffer` element copy |
| `CommanderTreeRunner` (MonoBehaviour) | GameObject component | ECS system that schedules jobs |
| `currentAgentOffset` integer offset | Works for one array but not ECS model | Per-agent entity with own buffer OR offset metadata in the buffer element |

#### Grey Area

| Component | Notes |
|---|---|
| Node method classes (~30 files) | Would need Entity-vs-GameObject dual implementation (like BD Pro's `Entity Task` pattern) or conversion to pure burstable functions |
| Commander stride | Per-agent entities eliminate stride but introduce buffer-per-entity overhead. A shared `DynamicBuffer` with entity index as offset is an alternative. |
| Per-component overrides | BD Pro uses Property Bindings via bakers. Same pattern would work. |
| Tracked bindings | BD Pro uses Property Bindings — fully replaced |
| `FieldTypeHelper` / `VariableTypeRegistry` | Type resolution must happen at bake time, not runtime |

### What a DOTS Conversion Would Look Like for the Current System

#### Phase 1: Replace Runtime Storage

Replace the `object[]` array with a burstable storage layer:

```csharp
// Hypothetical 8-byte union for common value types:
public struct BlackboardValue : IBufferElementData
{
    public BlackboardValueKind kind;    // which union field is active
    public int intValue;
    public float floatValue;
    public Entity entityValue;          // for GameObject/Transform refs (baked to Entity)
    // Vectors > 8 bytes would need separate typed buffers
}
```

Or use the BD Pro approach: typed `DynamicBuffer<T>` per variable type with a registry mapping variable index → (bufferType, elementIndex).

#### Phase 2: Bake the Tree into Entities

Replace the `TreeBaker` output (runtime `NodeData[]` + `FieldData[]` + `FieldBinding[]`) with ECS entity data:

```
Authoring (GameObject)           Bake                    Runtime (ECS)
────────────────────────  ──────────────────  ──────────────────────────
BehaviourTreeAsset          → Baker            → Entity with:
  ├── nodes[]               →                      DynamicBuffer<NodeElement>
  ├── blackboardDefinition  →                      DynamicBuffer<VariableDefinition>
  └── field data            →                      DynamicBuffer<FieldMapping>
BlackBoard (component)      → Baker            → IComponentData (variable storage info)
TreeRunner                  → Baker            → IComponentData + system reference
```

#### Phase 3: Replace Node Execution

Replace the tick loop ([TickDispatcher](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Runtime/TickDispatcher.cs)) with ECS systems:

```csharp
// Hypothetical burstable system
public partial struct SequenceTickSystem : ISystem
{
    private void OnUpdate(ref SystemState state)
    {
        foreach (var (nodeStates, taskBuffer, sharedVars) in
            SystemAPI.Query<DynamicBuffer<NodeState>, DynamicBuffer<TaskComponent>,
                            DynamicBuffer<SharedVariableElement>>()
                .WithAll<SequenceFlag, EvaluateFlag>())
        {
            // Burst-compiled tick loop — no boxing, no allocations
            for (int i = 0; i < taskBuffer.Length; i++)
            {
                // Evaluate child nodes, read/write sharedVars by index
            }
        }
    }
}
```

#### Phase 4: Handle Stride in DOTS

Three options for per-agent data:

| Option | How | Pros | Cons |
|---|---|---|---|
| **A: Per-agent entities** (BD Pro approach) | Each agent is a separate Entity with its own `DynamicBuffer` | Clean entity isolation, burstable, BD Pro proven | Memory scales with agent count × variable count, commander must write to all agents |
| **B: Shared buffer + entity index** | One `DynamicBuffer` per tree, agent index as offset (like current stride) | Low memory, contiguous | Requires offset-aware access patterns, harder to burst |
| **C: Ordered agent registry** | Commander writes to a single buffer; agents read via `EntityCommandBuffer` copies per frame | Decouples write from read | Copy overhead per frame per agent |

**Option B** is the most natural mapping of the current system's stride to DOTS. It preserves the low-memory, contiguous-access pattern that makes the current system efficient for multi-agent scenarios. However, it requires the Burst systems to be aware of the agent count and offset — this is doable with a companion `IComponentData` storing `agentCount`.

#### Phase 5: Squad / Commander in DOTS

The commander-to-squad-to-agent data flow becomes:

```
Commander Entity                        Squad Entity                   Agent Entities
─────────────────                    ────────────────                ────────────────
DynamicBuffer<Task>                   SquadGoalBuffer                 Per-agent buffers
CommanderDataBuffer   ──[system]──→   (copied by ECS system)  ──→   (read by agent systems)
                  ←──[system]──      AgentStatusBuffer       ←──    (written by agent systems)
```

`SquadInstance` becomes an ECS `ISystem` that copies data between buffers. The current slot-pair caching becomes entity-to-entity buffer element mapping, resolved at bake time.

### Migration Path Recommendation

```
Current (GameObject)                    Intermediate                    DOTS
─────────────────────               ────────────────────             ──────────
ManagedBlackboardStorage    →    Replace object[] with        →    DynamicBuffer<ValueUnion>
                                  typed NativeArrays
                                  (eliminate boxing, keep
                                   GameObject API)

NodeMethod + FieldBinding   →    Replace Expression trees     →    Bake indices directly into
                                  with baked direct access         IBufferElementData
                                  (pre-AOT compatible)

TreeBaker                   →    Same bake, output to         →    Entity Baker
                                  struct arrays OR entities

BlackBoard (MonoBehaviour)  →    Keep MonoBehaviour,          →    Baker + IComponentData
                                  add "DOTS data" sidecar

CommanderTreeRunner         →    Keep MonoBehaviour,          →    ECS system with
                                  schedule Jobs for                 per-agent entities
                                  per-agent evaluation             or shared buffer + offset
```

### Key Insight

**The current slot-offset architecture is already more DOTS-aligned than BD Regular's SharedVariable model.** Your system already uses bake-time name→index resolution, integer slot access, and a flat array — all three are DOTS-native patterns. BD Regular's scattered `SharedVariable<T>` heap objects are actually harder to convert to DOTS (which is why BD Pro had to be a completely separate codebase).

**The main blocking issues:**
1. `object[] values` with boxing — needs typed unmanaged storage
2. Expression tree compilation — needs baked index direct access
3. MonoBehaviour `BlackBoard` — needs entity baking
4. `SquadInstance` boxed copy loop — needs ECS buffer copy system

**The pre-existing strengths:**
1. Slot offset architecture — stable integer indices (DOTS-native)
2. `FieldData` struct — blittable, ready for `IBufferElementData`
3. Bake-time resolution — same principle as Entity Baking
4. Stride — maps naturally to offset-based buffer access
5. `IBlackBoardAccess` decoupling — migration path exists at the interface boundary
