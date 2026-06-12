# Blackboard Shared Variables — Edge Cases & Optimization Analysis

## Motivation

The original `BlackboardDefinition` used a mix of legacy struct-based variables and a `[SerializeReference]` `List<BlackboardVariableBase>`. The struct system was too rigid — it couldn't support arbitrary custom types, which became necessary when user feedback showed a need for more generic nodes and custom types flowing between Commander trees and Agent trees.

The migration to a purely `[SerializeReference]`-based system (`sharedVariables`) enables:

- **Any serializable type** as a blackboard variable (not just a hardcoded enum of types)
- **Custom C# types** for commander ↔ agent communication (e.g., `TacticalRole`, `FormationType`, custom structs)
- **Generic node implementations** that work with any variable type without code changes

This document captures edge cases, risks, and optimization opportunities in the current implementation.

---

## Implementation Flow: Before vs After

### Before (Legacy Struct + Generic Mix)

```
BlackboardDefinition
├── old struct-based variable arrays (fixed types only)
└── genericVariables: List<BlackboardVariableBase>  ← [SerializeReference]

Slot computation: struct arrays first, then genericVariables appended
Index mapping: fragile, different code paths for struct vs generic
Type support: limited to predefined enum of types
```

Serialization issues:
- `DeleteArrayElementAtIndex` on `[SerializeReference]` left null holes
- `GetAllVariables()` filtered nulls as a workaround
- Slot indices between editor and runtime could desync

### After (Pure sharedVariables)

```
BlackboardDefinition
└── sharedVariables: List<BlackboardVariableBase>    ← [SerializeReference]
    [FormerlySerializedAs("genericVariables")]

Slot computation: single unified path — cumulative stride sum
Index mapping: consistent — GetVariableSlotRange / ResolveSlotOffset
Type support: any serializable type via BlackboardVariable<T>
```

Cleanup mechanisms:
- `RemoveNullHoles()` on definition load (defensive, handles migration artifacts)
- `BuildSerializedReferences` detects layout changes and remaps by name

---

## Runtime Data Flow

```
Editor (BlackBoardView)
    └── Edits sharedVariables in the definition asset
        └── HandleRenames / HandleTypeChanges propagate to tree assets

Editor (BlackBoardEditor)
    └── OnInspectorGUI every frame:
        1. BuildSerializedReferences(definition)
           ├── Saves snapshot: lastBuiltVarNames[], lastBuiltVarStrides[]
           ├── Detects layout change via HasSameVariableLayout()
           ├── On change: SaveReferencesByNameFromSnapshot() ← uses SNAPSHOT layout
           │   └── RemapRenamedReferences() ← rename propagation
           ├── Rebuilds serializedReferences to match total slot count
           └── RestoreReferencesByName() ← restores refs by name using NEW layout
        2. Draws ObjectFields for reference types with override pattern
        3. ApplyModifiedProperties + conditional SetDirty

Play Mode Entry (BlackBoard.Initialize)
    └── Creates ManagedBlackboardStorage from definition
    └── Copies serializedReferences into storage for ref types
    └── CommanderTreeRunner initializes agents + bridges

Runtime Frame (CommanderTreeRunner)
    └── TickAgents:
        ├── bridge.PushDataProviders()      ← components → self BB
        ├── bridge.CopyCommanderToAgent()   ← commander BB → self BB
        ├── agent.Evaluate()                ← agent tree reads self BB
        └── bridge.CopyAgentToCommander()   ← self BB → commander BB
    └── EvaluateCommander:
        └── commander tree evaluates against commander BB
```

---

## Edge Cases — Sorted by Severity

### HIGH Severity

| ID | Component | Edge Case | Current Behavior | Fix Recommendation |
|----|-----------|-----------|-----------------|-------------------|
| **H1** | `ManagedBlackboardStorage` | `GetVariableSlotRange(OOB index)` returns sentinel `(-1, -1)` instead of stale defaults | ✅ Fixed: returns sentinel `baseSlot=-1, stride=-1` so callers can detect failure |
| **H2** | `CommanderBindingBridge` | `ResolveBindings` silently returned if storage is not `ManagedBlackboardStorage` | ✅ Fixed: logs error with storage type before returning |

### MEDIUM Severity

| ID | Component | Edge Case | Current Behavior | Fix Recommendation |
|----|-----------|-----------|-----------------|-------------------|
| **M1** | `BlackboardVariable<T>` | `GetValue(elementIndex)` OOB on stride > 1 falls back to `singleValue` silently | Returns wrong element without error | Log warning or clamp elementIndex |
| **M2** | `BlackboardVariable<T>` | `SetValue(elementIndex)` OOB on stride > 1 silently does nothing | Write silently dropped | Log warning |
| **M3** | `BlackboardVariable<T>` | `EnsureArraySize()` stride shrinks (8→3) truncates array | Data loss without warning | Log warning on stride reduction |
| **M4** | `ManagedBlackboardStorage` | Unresolved types (`slotType == null`) allow any write through `CanWriteBoxed` | Incompatible data could be stored silently | Type resolution should be validated at init time |
| **M5** | `ManagedBlackboardStorage` | `Get<T>` doesn't attempt `Convert.ChangeType` like `NodeMethod.ReadFromBBGeneric` does | Inconsistent coercion between storage read and node execution | Align behavior or document the split |
| **M6** | `CommanderBindingBridge` | `CopyCommanderToAgent` with `agentID >= stride` writes OOB on commander storage | Commander storage corruption possible | Add bounds check with error log |
| **M7** | `BlackBoardEditor` | `DrawReferenceSlotEditor` with `slotIndex` OOB on `serializedRefs` would throw | Crashes if slot computation diverges from `BuildSerializedReferences` | Rare — same logic used, but could add guard |
| **M8** | `BlackBoard` | Variable RENAME was NOT propagated to per-component `serializedReferences` | Per-component overrides lost on rename | ✅ Fixed: `RemapRenamedReferences` in `BuildSerializedReferences` |

### LOW Severity

| ID | Component | Edge Case | Current Behavior |
|----|-----------|-----------|-----------------|
| **L1** | `BlackboardVariable<T>` | `Clone()` for reference types (GameObject) is shallow | Same ref, not deep copy. By design |
| **L2** | `BlackboardVariableBase` | `GetValueType()` with renamed/missing assembly returns null | Handled everywhere with null checks |
| **L3** | `BlackboardVariableBase` | `Stride` set to 0 or negative | Clamped to >= 1 |
| **L4** | `BlackboardVariableBase` | `IsValueType()` returns false for enums | Explicitly excludes `.IsEnum` |
| **L5** | `ManagedBlackboardStorage` | `Get<T>` / `GetBoxed` with OOB index | Returns `default`/`null`, logs warning |
| **L6** | `ManagedBlackboardStorage` | `Set<T>` / `SetBoxed` with OOB index | Silent no-op, logs warning on type mismatch only |
| **L7** | `BlackBoard` | `BuildSerializedReferences` with null definition | Returns early, clears data |
| **L8** | `BlackBoard` | First call (`lastBuiltVarNames` is null) | Skips layout detection, just resizes |
| **L9** | `BlackBoard` | Variable name is empty/null in snapshot | Skipped in save/restore (empty names can't be remapped) |
| **L10** | `BlackBoard` | `Initialize` at play start with `serializedReferences.Count > storage.Count` | Guard `i < storage.Count` prevents OOB |
| **L11** | `BlackBoard` | `Set()` during play mode with `slotIndex >= serializedReferences.Count` | Skip sync (play mode writes don't persist) |
| **L12** | `BlackBoard` | `Set()` with non-UnityEngine.Object ref type (e.g., string) | Storage stores it, but `serializedReferences` sync skipped |
| **L13** | `BlackBoard` | Stride changes (1→8) on ref type — extra 7 slots are null | Best effort restore, extra slots null |
| **L14** | `BlackBoard` | Type change (value→reference) — old value-type slot becomes reference | Nothing was saved (was value type), new slot is null ✓ |
| **L15** | `BlackBoard` | Type change (GameObject→Transform) — different but assignable type | `UnityEngine.Object` is common base, works correctly |
| **L16** | `BlackBoardView` | `HandleRenames` with different count of added vs removed names | Pairs up to `Math.Min`, extra = deletions/new vars |
| **L17** | `BlackBoardView` | `HandleTypeChanges` first frame after domain reload | Initial type sync (may trigger redundant propagations) |
| **L18** | `BlackBoardEditor` | Domain reload clears `overrideActiveSlots` + `lastLayoutHash` | Rebuilds from `serializedReferences` on next frame |
| **L19** | `BlackBoardEditor` | Selecting different BlackBoard component | `overrideActiveSlots` retains stale data, cleared by layout hash compare |
| **L20** | `BlackboardDefinition` | Null holes in `sharedVariables` from migration or legacy deletes | ✅ Fixed: `RemoveNullHoles()` on definition load |

---

## Recent Fixes Applied

| Date | Issue | Fix |
|------|-------|-----|
| Recent | `BuildSerializedReferences` saved from live ScriptableObject (already mutated) → wrong slot offsets | `SaveReferencesByNameFromSnapshot` uses snapshot (`lastBuiltVarNames`/`lastBuiltVarStrides`) |
| Recent | Variable reorder swapped override refs between wrong variables | Same as above — snapshot-based save |
| Recent | Variable rename lost per-component overrides in `serializedReferences` | `RemapRenamedReferences` detects renames and remaps dictionary keys |
| Recent | `SetDirty` called unconditionally every frame in `BlackBoardEditor` | `BeginChangeCheck` + `so.hasModifiedProperties` guard |
| Recent | Duplicate slot computation in `BlackBoardEditor` | Removed — `BuildSerializedReferences` is sole owner of layout |
| Recent | Reference-type ObjectFields directly editable in `BlackBoardEditor` | Override pattern: definition value shown read-only with Override button |
| Recent | Null holes in `sharedVariables` from migration / legacy deletes | `RemoveNullHoles()` cleanup on definition load |
| Recent | Field renamed `genericVariables` → `sharedVariables` | `[FormerlySerializedAs]` preserves existing serialized data |
| Recent | **E16** — `Type.GetType()` reflection every variable access (per-frame) | Cached resolved `Type` in `[NonSerialized]` field on `BlackboardVariableBase` |
| Recent | **E1** — `GetAllVariables()` list allocation every call (per-frame) | Returns raw `sharedVariables` directly; null filtering is dead code |
| Recent | **E17** — `SnapshotNameSet`/`SnapshotTypeMap` allocs every frame | Guarded with `so.hasModifiedProperties` — only snapshots on actual changes |
| Recent | **H1** — `GetVariableSlotRange` OOB returned stale defaults `(0, 1)` | Returns sentinel `(-1, -1)` so callers can detect failure |
| Recent | **H2** — `ResolveBindings` silently returned on non-ManagedBlackboardStorage | Logs error with storage type before returning |

---

## Optimization Analysis

### HOT PATH: Per-Frame Allocations (Editor)

`BlackBoardEditor.OnInspectorGUI()` runs **every inspector frame** (60+ times/sec when selected).

| # | Location | Allocation | Severity | Fix |
|---|----------|-----------|----------|-----|
| **E1** | `definition.GetAllVariables()` | `new List<BlackboardVariableBase>()` for null-filtered copy every frame | **High** — 60 allocs/sec | Cache result and invalidate on layout hash change |
| **E2** | `int[] slotOffsets = new int[varCount]` | Array allocation every frame | **Medium** | Reuse array, resize only when varCount changes |
| **E3** | `ComputeLayoutHash(allVars)` | Iterates all variables with `string.GetHashCode()` every frame | **Low** | Only recompute on `so.hasModifiedProperties` or after `BuildSerializedReferences` detected a change |
| **E4** | `serializedRefs.GetArrayElementAtIndex(i)` | Called N times in layout-change loop. Each call creates internal SerializedProperty | **Low** | Already only runs when hash differs (rare) |
| **E5** | `bv.GetBoxedValue(elementIndex)` in `DrawReferenceSlotEditor` | Boxing for value types, called per slot per frame | **Medium** | Since this path only applies to reference types, boxing doesn't happen. But the call still goes through `GetValue()` → `arrayValues` bounds check. |
| **E6** | `bv.GetValueType()` called 3 times per variable (classification pass + 2 render passes) | `Type.GetType(assemblyQualifiedName)` — reflection every call | **Medium** | **Cache result per variable.** `GetValueType()` is called up to 3× per variable per frame in the editor, and also in `BuildSerializedReferences`, `Initialize`, and `RestoreReferencesByName`. |

### HOT PATH: `GetAllVariables()` — Called from 7+ Sites

`GetAllVariables()` allocates a **new list** and copies all non-null variables **every call**.

Call sites:
- `BlackBoardEditor.OnInspectorGUI()` — every frame
- `BlackBoard.BuildSerializedReferences()` — every frame (via editor)
- `BlackBoard.Initialize()` — once at play start
- `BlackBoard.RestoreReferencesByName()` — layout change only
- `ManagedBlackboardStorage.InitializeFromVariables()` — once at play start
- `CommanderBindingBridge.ResolveBindings()` — once at play start per agent
- `TreeBaker.ResolveSlotOffset()` — during bake

| # | Issue | Severity | Fix |
|---|-------|----------|-----|
| **E7** | List allocation + null-filtered copy | **High** | Cache result in `BlackboardDefinition` with invalidation on mutation. Or return the raw list since `RemoveNullHoles()` guarantees no nulls exist. |

### HOT PATH: `BuildSerializedReferences` — Every Frame

This is the heavyweight method called every inspector frame.

| # | Location | Issue | Severity | Fix |
|---|----------|-------|----------|-----|
| **E8** | `HasSameVariableLayout()` | O(n) comparison every frame even when nothing changed | **Medium** | Cache a layout hash in `BlackboardDefinition`. If hash matches `lastLayoutHash`, skip entire layout comparison. |
| **E9** | `SaveReferencesByNameFromSnapshot()` + `RemapRenamedReferences()` + `RestoreReferencesByName()` | Only on layout change, but heavy: dict allocation, HashSet×2, List×2, slot offset recomputation | **Low** | Only on layout change (rare), acceptable |
| **E10** | `serializedReferences.Clear()` + `for` loop `Add(null)` | Rebuilds list element-by-element. With 100+ slots this is wasteful | **Low** | Only on layout change |
| **E11** | `GetTotalSlotCount()` recomputes slot offsets from scratch | Same computation `InitializeFromVariables` already does | **Low** | Could store totalSlotCount in the definition |

### HOT PATH: CommanderBindingBridge — Play Mode Per-Frame

`CopyCommanderToAgent()` and `CopyAgentToCommander()` run **per agent per frame**.

| # | Location | Issue | Severity | Fix |
|---|----------|-------|----------|-----|
| **E12** | `GetBoxed(index)` / `SetBoxed(index, value)` | Direct array access — already O(1). Good. | None | — |

`ResolveBindings()` runs once at init, but has issues:

| # | Location | Issue | Severity | Fix |
|---|----------|-------|----------|-----|
| **E13** | Name-based linear lookup: for each binding, scans both variable lists | O(N_bindings × M_variables). 10 bindings × 50 vars = 500 string comparisons | **Medium** | Build `Dictionary<string, int>` (name→index) once per definition, reuse for all bindings |
| **E14** | `GetVariableSlotRange()` per binding | O(n) scan to compute cumulative stride. Called 2× per binding (self + commander) | **High** | Precompute and cache slot offsets in `ManagedBlackboardStorage.InitializeFromVariables()` — store `int[] variableSlotOffsets` array for O(1) lookup |

### HOT PATH: `GetVariableSlotRange` — O(n) Scan

```csharp
// Current: O(n) cumulative stride sum on every call
for (int prevIndex = 0; prevIndex < variableIndex; prevIndex++)
    baseSlot += (runtimeVariables[prevIndex].Stride > 1 ? ... : 1);
```

Called from:
- `CommanderBindingBridge.ResolveBindings()` — 2× per binding (self + commander)
- Potentially `TreeBaker.PackFieldEntryWithArray()` during baking

| # | Issue | Severity | Fix |
|---|-------|----------|-----|
| **E15** | O(n) scan per call instead of O(1) cached lookup | **High** | Store `int[] variableBaseSlots` in `ManagedBlackboardStorage` during `InitializeFromVariables`. Then `GetVariableSlotRange` is a simple array access. |

### REFLECTION: `GetValueType()` — Expensive Per-Call

```csharp
public Type GetValueType() => Type.GetType(variableTypeName);
```

`Type.GetType(string)` is a reflection call that searches loaded assemblies. Called every frame for every variable in the editor flow.

| # | Issue | Severity | Fix |
|---|-------|----------|-----|
| **E16** | Reflection call per variable per access | **High** | Cache the resolved `Type` in a `[NonSerialized]` field on `BlackboardVariableBase` after first resolution. Invalidate only when `variableTypeName` changes. |

### EDITOR: HandleRenames / HandleTypeChanges — Every Frame

| # | Location | Issue | Severity | Fix |
|---|----------|-------|----------|-----|
| **E17** | `SnapshotNameSet()` + `SnapshotTypeMap()` | Iterates all elements, allocates `HashSet` + `Dictionary` every frame | **Medium** | Only run when `serializedObject.hasModifiedProperties` is true |
| **E18** | `PropagateRename()` — `AssetDatabase.FindAssets("t:BehaviourTreeAssetBase")` | Scans ALL behaviour tree assets in the project on every rename | **Low** | Only on rename (rare). Could be optimized by pre-finding trees that reference this definition. |
| **E19** | `PropagateTypeChange()` — same full project scan | Same | **Low** | Same as above |
| **E20** | `UpdateTreeNodes()` — `AssetDatabase.LoadAllAssetsAtPath()` | Loads ALL sub-assets for each matching tree | **Low** | Only on rename/type change |

### MISC: String Interpolation Allocations

| # | Location | Issue | Severity | Fix |
|---|----------|-------|----------|-----|
| **E21** | `$"{unresolvedCount} variable(s)..."`, `$"{bv.Name} [{stride}]"`, `$"{typeName}{strideInfo}"` | Small string allocations every frame in editor IMGUI | **Low** | Negligible impact for typical variable counts (< 50). Only worth fixing if profiling shows GC pressure. |

---

## Optimization Ranking — By Severity (Frequency × Cost)

Ranked from most severe to least. Rationale: frequency (per-frame > per-init > per-change) and per-unit cost (reflection >> allocation >> O(n) scan >> string compare).

| Rank | ID | Issue | Frequency | Cost | Severity | Fix |
|------|----|-------|-----------|------|----------|-----|
| **1** | **E16** | `Type.GetType(assemblyQualifiedName)` reflection per variable access | Every frame, every variable, every access site (3× per var in editor alone) | **Reflection** — searches all loaded assemblies | ~~CRITICAL~~ **✅ FIXED** | Cached resolved `Type` in `[NonSerialized]` field on `BlackboardVariableBase` |
| **2** | **E1** | `GetAllVariables()` allocates `new List<>` every call | Every frame (BlackBoardEditor) + multiple times in BuildSerializedReferences + init | **GC allocation** — 60+ allocs/sec | ~~HIGH~~ **✅ FIXED** | Returns raw `sharedVariables` directly — null filtering is dead code since `RemoveNullHoles()` guarantees cleanliness |
| **3** | **E17** | `SnapshotNameSet()` + `SnapshotTypeMap()` allocates `HashSet` + `Dictionary` + iterates all elements | Every frame in BlackBoardView IMGUI | **GC allocation** — 60+ allocs/sec | ~~HIGH~~ **✅ FIXED** | Guarded with `so.hasModifiedProperties` — only snapshots when something actually changed |
| **4** | **E8** | `HasSameVariableLayout()` O(n) comparison every frame | Every frame in BuildSerializedReferences | **CPU** — O(n) string/int comparison | **MEDIUM** | Cache layout hash. If hash unchanged since last frame, skip the full O(n) comparison |
| **5** | **E14/E15** | `GetVariableSlotRange()` O(n) cumulative stride scan | Once per-agent at init (2× per binding: self + commander) | **CPU** — O(n) × bindings at play-start | **MEDIUM** | Precompute `int[] variableBaseSlots` during `InitializeFromVariables`. Makes lookup O(1) |
| **6** | **E13** | `ResolveBindings()` O(N×M) string comparisons per binding | Once per-agent at init | **CPU** — 10 bindings × 50 vars = 500 comparisons | **MEDIUM** | Build `Dictionary<string, int>` per definition before the loop; use O(1) lookup |
| **7** | **E6** | `GetValueType()` called 3× per variable per frame in editor (classification + 2 render passes) | Every frame | **Reflection** × 3 | **LOW-MEDIUM** | Fixed by **E16** — redundant calls eliminated when Type is cached |
| **8** | **E2** | `int[] slotOffsets = new int[varCount]` | Every frame in BlackBoardEditor | **GC allocation** — small array | **LOW** | Reuse array, resize only when varCount changes |
| **9** | **E3** | `ComputeLayoutHash()` iterates all vars + string.GetHashCode() | Every frame in BlackBoardEditor | **CPU** — minimal per variable | **LOW** | Already cheap. Only recompute if layout actually changed |
| **10** | **E18** | `PropagateRename()` — `AssetDatabase.FindAssets` scans ALL tree assets | Only on rename (rare) | **CPU** — project-wide scan | **LOW** | Pre-index trees by definition reference |
| **11** | **E19** | `PropagateTypeChange()` — same full project scan | Only on type change (rare) | **CPU** — project-wide scan | **LOW** | Same pre-indexing as E18 |
| **12** | **E20** | `UpdateTreeNodes()` — `AssetDatabase.LoadAllAssetsAtPath()` | Only on rename/type change (rare) | **CPU** — loads sub-assets | **LOW** | Only on change |
| **13** | **E4** | `serializedRefs.GetArrayElementAtIndex(i)` in layout-change loop | Only on layout change (rare) | **CPU** — SerializedProperty creation | **LOW** | Rare, acceptable |
| **14** | **E5** | `bv.GetBoxedValue(elementIndex)` in editor draw | Every frame per ref variable | **CPU** — bounds check + array access | **LOW** | Cheap operation, reference types only |
| **15** | **E9** | `SaveReferencesByNameFromSnapshot()` heavy allocs | Only on layout change (rare) | **GC allocation** — Dict + lists | **LOW** | Rare, acceptable |
| **16** | **E10** | `serializedReferences.Clear()` + `Add(null)` rebuild | Only on layout change (rare) | **GC allocation** — list rebuild | **LOW** | Rare, acceptable |
| **17** | **E11** | `GetTotalSlotCount()` recomputes slot offsets | Only in BuildSerializedReferences (layout change path) | **CPU** — O(n) | **LOW** | Rare, but could be cached in definition |
| **18** | **E21** | String interpolation (`$"{name} [{stride}]"` etc.) | Every frame in editor IMGUI | **GC allocation** — tiny strings | **NEGLIGIBLE** | Typical < 50 vars, not worth optimizing |
