# Commander System — Code Audit

> Generated 2026-06-20 | Updated 2026-06-20  
> Scanned: 30 files, ~3,500 LOC  
> **Resolved**: H1, H2 (see [fieldbinding-compiled-delegates.md](fieldbinding-compiled-delegates.md))  
> **False alarms**: E1 (unique definition per bake), E2 (dead init path never called)

---

## Severity Legend

| Icon | Severity | Meaning |
|------|----------|---------|
| 🔴 | Critical | Crash / data corruption / major frame hit |
| 🟠 | High | Significant perf regression / subtle logic bug under common scenarios |
| 🟡 | Medium | Degraded perf under edge conditions / maintenance hazard |
| 🟢 | Low | Cosmetic / debug-only / init-time only |
| ✅ | Resolved | Fix implemented and verified |
| ❌ | False alarm | Verified not actually an issue |

---

## 1. Hot-Path Allocations & Performance

Issues that trigger **every frame** on the commander tick loop.

| # | Status | Severity | File | Line(s) | Issue | Impact |
|---|--------|----------|------|---------|-------|--------|
| 1 | ✅ | ~~🔴~~ | [NodeMethod.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Core/NodeMethod.cs#L50-L71) | 53, 70 | **`FieldBinding.ReadFromBBGeneric`**: calls `bb.GetBoxed()` (value-type boxing) then `fieldInfo.SetValue()` (reflection). | **Fixed**: `Expression.Compile()` produces typed delegate. Same file, lines 49-96. [Doc](fieldbinding-compiled-delegates.md) |
| 2 | ✅ | ~~🔴~~ | [NodeMethod.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Core/NodeMethod.cs#L73-L97) | 76, 96 | **`FieldBinding.WriteToBBGeneric`**: `fieldInfo.GetValue()` (reflection + boxing) then `bb.SetBoxed()`. | **Fixed**: Typed write delegate. Same fix as #1. [Doc](fieldbinding-compiled-delegates.md) |
| 3 | 🟠 | [CommanderBindingBridge.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Runtime/CommanderBindingBridge.cs#L178-L196) | 193, 195 | **`CopyCommanderToAgent`**: `GetBoxed` + `SetBoxed` per binding per agent per frame. Value types are boxed on both sides. | With 10 bindings × 8 agents = 80 box/unbox pairs/frame. |
| 4 | 🟠 | [CommanderBindingBridge.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Runtime/CommanderBindingBridge.cs#L203-L218) | 215, 216 | **`CopyAgentToCommander`**: identical boxing pattern as #3, opposite direction. | Same magnitude, add another 80 box/unbox pairs/frame. |
| 5 | 🟠 | [ConditionalAbort.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Runtime/ConditionalAbort.cs#L154-L169) | 159, 167 | **`EvaluateLeafCondition`**: calls `ResolveInputsGeneric` + `WriteOutputsGeneric` on every condition check every tick for SELF/LowerPriority abort. The write-back (`WriteOutputsGeneric`) is wasted — only the boolean result is needed. | Per composite with SELF/Both abort: re-evaluates all children's conditions each frame, each paying boxing+reflection. |
| 6 | 🟡 | [ConditionalAbort.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Runtime/ConditionalAbort.cs#L41-L76) | 43-76 | **SELF abort re-evaluates ALL children** every tick, even if no child is RUNNING. The `runningChildLocal < 0` case still loops and evaluates all conditions, then discards results. | Each composite with SELF/Both abort scans all children regardless of running state. |
| 7 | 🟡 | [BlackBoard.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Core/BlackBoard.cs#L490-L510) | 496-509 | **`SetBoxed` syncs `serializedReferences`** every call, including when called from the Bridge at frame rate. This does `GetSlotKind()` + Unity Object serialization sync — needed for editor, wasted at runtime. | Extra work per Bridge copy operation. |
| 8 | 🟢 | [BlackBoard.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Core/BlackBoard.cs#L418-L426) | 421 | **`Set<T>(string, T)` has unconditional `Debug.Log`** with string interpolation. Called on hot path by name-based setters. | Boxing + string alloc every call. Should be `#if UNITY_EDITOR` or removed. |

---

## 2. Edge Cases & Correctness

Issues that produce wrong results, crashes, or data loss in specific scenarios.

| # | Severity | File | Line(s) | Issue | Trigger |
|---|----------|------|---------|-------|---------|
| 1 | — | ~~🔴~~ | [CommanderTreeRunner.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Runtime/CommanderTreeRunner.cs#L246-L267) | 256-261 | **`ResizeSquadDataStrides` mutates `vars[i].Stride` directly** on the definition's variable list. ~~If two commanders share the same `BlackboardDefinition` asset, stride changes from one corrupt the other.~~ | **False alarm**: `TreeBaker.BakeTree` line 26 `ScriptableObject.CreateInstance<BlackboardDefinition>()` creates a unique instance per bake. `Object.Instantiate(existing)` also clones. Each runner owns its definition exclusively — no sharing possible. |
| 2 | ❌ | ~~🔴~~ | [CommanderBindingBridge.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Runtime/CommanderBindingBridge.cs#L67-L86) | 82-83 | **`ResolveBindings` reads `selfStorage.Definition` / `commanderStorage.Definition`**, but `ManagedBlackboardStorage.Definition` is `null` when `Initialize(IReadOnlyList)` was called (line 38 of ManagedBlackboardStorage.cs). Causes silent binding failure. | **False alarm**: The `IReadOnlyList` overload is dead code — never called. Only `BlackBoard.Initialize(BlackboardDefinition)` (line 53 of BlackBoard.cs) invokes storage init, always setting `definition`. |
| 3 | 🟠 | [ManagedBlackboardStorage.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Core/ManagedBlackboardStorage.cs#L125-L195) | 152-157 | **`ResizeFromVariables` reads old stride from `runtimeVariables`** using `varIndex` index into the new `variables` list. If variable count differs (additions/removals between resize calls), `oldStride` defaults to 1 for the extras — slot offset tracking drifts. | Only triggered if variables are added/removed at runtime (rare), but recovery is silent data corruption. |
| 4 | 🟠 | [TreeBaker.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Runtime/TreeBaker.cs#L525-L566) | 539-552 | **`PackFieldEntryWithArray`**: if a variable name doesn't resolve in the scope map (e.g., commander var referenced from an agent-only scope), it writes `FieldData.FromVariable(-1)`. Runtime BB access at slot -1 produces default values or crashes. | Misconfigured tree binding referencing wrong scope. |
| 5 | 🟡 | [CommanderTreeRunner.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Runtime/CommanderTreeRunner.cs#L44-L72) | 46-51 | **`TickAgents`**: `cachedBridges` is check-null early, but `cachedBridges.Length` vs `registeredAgents.Count` mismatch is not guarded. If someone adds agents to `registeredAgents` without going through `RegisterAgent`, index-out-of-bounds crash on `cachedBridges[i]`. | Manual list manipulation. |
| 6 | 🟡 | [BlackBoard.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Core/BlackBoard.cs#L131-L204) | 159 | **`RemapRenamedReferences`**: pairs removals and additions by list order to detect renames. If two variables swap names (A↔B), the pairing is ambiguous — A maps to B's old refs, B maps to A's old refs (likely intended), but a third variable being added simultaneously (add B, remove C, add D) causes mis-pairing. | Layout change with simultaneous renames + additions. |
| 7 | 🟡 | [TreeBaker.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Runtime/TreeBaker.cs#L506-L523) | 514-518 | **`IsArrayFieldEntry`** does O(n) linear name scan for every field entry during bake. If two variables accidentally share the same name, picks the first — silent misclassification. | Duplicate variable names (shouldn't happen but not enforced). |
| 8 | 🟢 | [CommanderBindingBridge.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Runtime/CommanderBindingBridge.cs#L136) | 136 | **Dead variable**: `int selfSlot = selfVarIndex;` is assigned but never read. `selfBaseSlot` is used instead. | Compiles clean, no runtime impact. |
| 9 | 🟢 | [AgentTreeRunner.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Runtime/TreeRunner.cs#L52-L55) | 53, 60 | **Data providers collected twice**: `AgentTreeRunner.OnPostInitialize` (line 60) and `CommanderBindingBridge.CollectDataProviders` (line 52) both call `GetComponentsInChildren<IBlackboardDataProvider>()` on the same GameObject. | Two allocations for identical data. Init-time only. |
| 10 | 🟢 | [AgentTreeRunner.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Runtime/TreeRunner.cs#L71-L139) | 96-104 | **`ResolveTrackedBindings` fallback logic**: old data without GUIDs falls back to reference comparison (`group.targetTree == authoringAsset`) which can break across domain reload when ScriptableObject references get mangled. Then falls through to "first group with no identity" which may pick the wrong group. | Editor domain reload with legacy saved data. |

---

## 3. Redundancy & Maintenance Hazards

Duplicated logic, repeated computations, and code that increases bug surface.

| # | Severity | File(s) | Issue | Duplications |
|---|----------|---------|-------|-------------|
| 1 | 🟠 | [TreeBaker.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Runtime/TreeBaker.cs#L470-L483), [ManagedBlackboardStorage.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Core/ManagedBlackboardStorage.cs#L97-L118), [BlackBoard.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Core/BlackBoard.cs#L381-L395), [BlackBoard.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Core/BlackBoard.cs#L103-L113), [BlackboardEnumGenerator.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Editor/BlackboardEnumGenerator.cs#L131-L145) | **Stride-to-slot-offset computation duplicated 5+ times**. Same loop: `for each variable: slot += max(stride,1)`. Each copy has subtly different behavior (`ResolveSlotOffset` returns -1 for bad index; `GetVariableSlotRange` logs an error; `GetTotalSlotCount` skips null vars). | 5 files |
| 2 | 🟠 | [BlackboardDefinition.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Core/BlackBoardDefinition.cs#L38-L49), [BlackBoard.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Core/BlackBoard.cs#L397-L416), [CommanderBindingBridge.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Runtime/CommanderBindingBridge.cs#L102-L126), [TreeBaker.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Runtime/TreeBaker.cs#L514-L518) | **Variable name → index lookup** reimplemented at 4 call sites. Three do linear scan; `BlackboardDefinition.GetVariableIndex` already exists and is the canonical implementation. | 4 files |
| 3 | 🟡 | [TreeBaker.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Runtime/TreeBaker.cs#L428-L464), [TreeBaker.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Runtime/TreeBaker.cs#L289-L316), [BlackboardDefinition.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Core/BlackBoardDefinition.cs#L66-L84) | **Variable clone + fallback logic** copy-pasted 3 times. Each has the same pattern: try `Clone()`, fallback via `Activator.CreateInstance` reflection, last resort `BlackboardVariable<object>` stub. A bug fix in one copy won't propagate. | 3 locations in 2 files |
| 4 | 🟡 | [ConditionalAbort.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Runtime/ConditionalAbort.cs#L42-L117) | **SELF and LOWER_PRIORITY abort share identical condition-evaluation logic** (lines 48-66 vs 97-108) but are implemented as separate code blocks. Both call `EvaluateLeafCondition`/`EvaluateCompositeCondition` + compare `lastConditionResult`. The LP block (lines 97-108) does a subset of what SELF does — could be unified into a single helper. | Same file, 20+ duplicated lines |
| 5 | 🟡 | [BB_CompareMethods.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Runtime/Methods/BB_CompareMethods.cs) | **BB_CompareInt and BB_CompareFloat are identical** except for the `int`/`float` keywords (lines 6-26 vs 28-48). Same for Vector2/Vector3 (lines 68-116) and GameObject/Transform (lines 118-152). 7 classes, 3 unique implementations. | 1 file, 7 classes → 3 patterns |
| 6 | 🟢 | [BlackBoard.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Core/BlackBoard.cs#L62-L68) | **`Initialize` computes stride offsets for debug logging** (lines 62-68) immediately after `storage.Initialize(definition)` already computed them internally. Same loop, same logic, debug-only. | Same method, 6 lines apart |
| 7 | 🟢 | [BlackBoard.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Core/BlackBoard.cs#L235-L296) | **`SaveReferencesByName` and `SaveReferencesByNameFromSnapshot`** are near-duplicates (lines 235-263 vs 270-292). One works from `IReadOnlyList<BlackboardVariableBase>`, the other from raw `string[]`+`int[]`. Both do the same slot-walk + reference-check logic. | Same file, 2 methods |
| 8 | 🟢 | [CommanderTreeRunner.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Runtime/CommanderTreeRunner.cs#L78-L122) | **`CopySquadsToTree` / `CopySquadsFromTree` / `GetRunnerSquads`** are static methods on `CommanderTreeRunner` but their logic has nothing commander-specific — they operate purely on `BehaviourTreeRunnerBase` and `SquadInstance`. Should live on a squad helper or on `BehaviourTreeRunnerBase`. | N/A (misplaced, not duplicated) |

---

## Summary

### Critical Issues (🔴) — Fix Immediately

| # | Category | Status | Issue |
|---|----------|--------|-------|
| ~~H1~~ | Hot Path | ✅ Resolved | `FieldBinding.ReadFromBBGeneric` / `WriteToBBGeneric` use `FieldInfo.SetValue`/`GetValue` (reflection) every tick → compiled delegates ([doc](fieldbinding-compiled-delegates.md)) |
| E1 | Edge Case | ❌ False alarm | Mutating definition stride is safe — each runner owns a unique baked/cloned definition |
| E2 | Edge Case | ❌ False alarm | `Initialize(IReadOnlyList)` null-definition path is dead code — never called |

### High-Severity (🟠) — Fix Next

| # | Category | Issue |
|---|----------|-------|
| H3-4 | Hot Path | `CopyCommanderToAgent` / `CopyAgentToCommander` box every value type per binding per agent |
| H5 | Hot Path | `EvaluateLeafCondition` wastes `WriteOutputsGeneric` on abort-condition checks |
| E3 | Edge Case | `ResizeFromVariables` slot tracking drifts if variable count changes |
| E4 | Edge Case | `PackFieldEntryWithArray` writes slot -1 for unresolvable vars |
| R1 | Redundancy | Stride-to-slot offset computed in 5 different places |
| R2 | Redundancy | Variable name lookup reimplemented in 4 places |

### Highest-Impact Single Fix

> ~~**Replacing `FieldBinding` reflection with source-generated typed accessors** would eliminate the two largest per-frame costs (reflection + boxing on every shared-var field read/write). This affects every node evaluation in every tree — agent trees and commander trees alike.~~
>
> **DONE 2026-06-20**: See [fieldbinding-compiled-delegates.md](fieldbinding-compiled-delegates.md). Expression-compiled delegates eliminate boxing + reflection on the hot path. 17 tests passing. Full reflection fallback preserved for AOT platforms.
