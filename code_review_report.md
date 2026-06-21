# Code Review Report — `feature/Framework_Commander_v2`

**Date:** 2026-06-21  
**30 files changed:** 1640 insertions, 374 deletions  
**10 new source files, 6 new prefabs/scriptable objects**

---

## Severity Legend

| Symbol | Meaning |
|---|---|
| 🔴 Critical | Will crash or silently produce wrong results at runtime |
| 🟠 High | Likely bug under specific but common conditions |
| 🟡 Medium | Code smell, duplication, or edge-case gap |
| 🔵 Low | Style violation, dead code, minor cleanup |

---

## 1. Critical Bugs

### 1.1 `CheckOrderMethod.Execute()` — Never Reads from Blackboard

**File:** [CheckOrderMethod.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Runtime/Methods/CheckOrderMethod.cs)  
**Severity:** 🔴 Critical

```csharp
// CURRENT (broken):
return receivedOrderSlot == expectedOrder ? NodeState.SUCCESS : NodeState.FAILURE;
```

`receivedOrderSlot` is the **slot offset** (e.g., 5), not the actual order value stored in the BB. `expectedOrder` may also be a variable slot, not a constant int. The method never calls `BB.GetBoxed()`.

**Fix:** Read both values from the blackboard:

```csharp
object receivedObj = bb.GetBoxed(receivedOrderSlot);
int receivedVal = receivedObj is int r ? r : -1;
int expectedVal = expectedOrder; // constant path
if (IsToggleVariable) expectedVal = bb.GetBoxed(expectedOrder) is int e ? e : -1;
return receivedVal == expectedVal ? NodeState.SUCCESS : NodeState.FAILURE;
```

---

### 1.2 `SendOrderMethod.Execute()` — Destroys Slot Offset

**File:** [SendOrderMethod.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Runtime/Methods/SendOrderMethod.cs)  
**Severity:** 🔴 Critical

```csharp
// CURRENT (broken):
ordersSlot = orderValue;  // overwrites the baked slot offset with the order value
```

After the first execution, `ordersSlot` no longer points to the BB slot. All subsequent calls write to a random/wrong location.

**Fix:** Call `bb.SetBoxed(ordersSlot, orderValue)` instead, and read `orderValue` from BB if it's a dynamic variable.

---

### 1.3 `TreeEvaluator` Constructor — Null Check After Array Access

**File:** [TreeEvaluator.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Runtime/TreeEvaluator.cs#L38-L84)  
**Severity:** 🔴 Critical

The null/empty guard for `nodeDatas` is at **lines 80-84**, but the constructor accesses `nodeDatas.Length` on lines 43, 45, 46, and indexes `nodeDatas[0]` on line 119. If `nodeDatas` is null or empty, the constructor throws before reaching the guard.

**Fix:** Move the guard to the top of the constructor, before any array access.

---

## 2. High-Severity Bugs

### 2.1 `ForEachRoleMethod` — Dynamic Variable Path Broken

**File:** [ForEachRoleMethod.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Runtime/Methods/ForEachRoleMethod.cs)  
**Severity:** 🟠 High

Two issues:

1. **`targetRoleSlot` used as raw int (line 37):** If `isToggleVariable` is ON (dynamic mode), the value must be read from BB via `bb.GetBoxed(targetRoleSlot)`, not used directly.

2. **Type-boxing pattern match (line 44):** `roleBoxed is int roleVal` will fail if the role enum is stored as a boxed `TacticalRole` instead of `int`. The pattern silently returns `false` and skips the role match, producing false negatives.

---

### 2.2 `SquadInstance.EnsureResolved` — Fragile Asset Matching

**File:** [SquadInstance.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Runtime/SquadInstance.cs#L79)  
**Severity:** 🟠 High

```csharp
group.treeAsset == treeDef.sourceTreeAsset  // ScriptableObject reference equality
```

In builds, `sourceTreeAsset` may be `null` or a different `ScriptableObject` instance than what `SquadDefinition` stores. Use GUID comparison (`sourceTreeGuid`) instead.

---

### 2.3 `CommanderTreeRunner.GetMaxSquadDataStride` — Only Checks First Variable

**File:** [CommanderTreeRunner.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Runtime/CommanderTreeRunner.cs#L186)  
**Severity:** 🟠 High

```csharp
int GetMaxSquadDataStride()
{
    // ... iteration to find first squad-data variable ...
    return variable.Stride;  // only returns the first match
}
```

If different squad-data variables have different strides (e.g., `AgentRole` stride=8, `AgentOrders` stride=4), the returned stride is wrong for some variables.

---

### 2.4 `BehaviourTreeEditor.OnSelectTree` — Stale `currentRunner`

**File:** [BehaviourTreeEditor.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Editor/BehaviourTreeEditor.cs#L307-L322)  
**Severity:** 🟠 High

When selecting a GameObject **without** a `BehaviourTreeRunnerBase`, `currentRunner` is never cleared — it retains the previous selection. This leaks stale state into tracked bindings and debug visuals.

---

## 3. Performance / Hot-Path Allocations

### 3.1 `PushTrackedBindings` — Reflection Per Frame

**File:** [BehaviourTreeRunnerBase.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Runtime/BehaviourTreeRunnerBase.cs#L168-L183)  
**Severity:** 🟠 High  
**Hot path:** Yes — called every `Update()` frame

```csharp
object value = binding.cachedPropertyInfo?.GetValue(binding.targetComponent);
// or
object value = binding.cachedFieldInfo?.GetValue(binding.targetComponent);
```

`PropertyInfo.GetValue()` and `FieldInfo.GetValue()` allocate a boxed object every call. For many tracked bindings across many agents, this adds significant GC pressure.

**Mitigation:** Pre-compile delegates via `Expression` trees during `ResolveTrackedBindings()`, similar to how `NodeMethod.CompileAccessors` works.

---

### 3.2 `ForEachAgentMethod` — `Debug.Log` in Per-Agent Loop

**File:** [ForEachAgentMethod.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Runtime/Methods/ForEachAgentMethod.cs#L34-L38)  
**Severity:** 🟡 Medium  
**Hot path:** Yes — called per agent per frame

Two `Debug.Log` calls inside the per-agent loop string-interpolate and allocate every iteration. Must be wrapped in `#if UNITY_EDITOR` or removed.

---

### 3.3 `VariableMethods.LogVariable` — Array Allocation Per Call

**File:** [VariableMethods.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Runtime/Methods/VariableMethods.cs#L141-L144)  
**Severity:** 🟡 Medium  
**Hot path:** Yes — called whenever LogVariable node executes

```csharp
object[] values = new object[stride];          // heap allocation
string message = string.Join(", ", values);     // string[] + string allocation
```

If stride is 8 and this node fires every frame for every agent, that's `8 * agentCount` object allocations plus string join per frame.

---

### 3.4 `BehaviourTreeEditor.PollDebugState` — Per-Frame Editor Hook

**File:** [BehaviourTreeEditor.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Editor/BehaviourTreeEditor.cs#L121-L125)  
**Severity:** 🟡 Medium  
**Hot path:** Yes — `EditorApplication.update` fires every editor frame in play mode

The `RefreshDebugVisuals` method is called every editor frame. Verify it doesn't allocate. If it does (e.g., `matchingVars.ToArray()`, LINQ), this is a major editor-performance sink.

---

### 3.5 `CustomNodeEditor` — Per-Inspector-Frame Allocations

**File:** [CustomNodeEditor.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Editor/NodeInspectorViewEditors/CustomNodeEditor.cs)  
**Severity:** 🟡 Medium  
**Hot path:** Yes — `OnInspectorGUI` called every editor repaint

- **Line 191:** `char.ToUpper(info.fieldName[0]) + info.fieldName.Substring(1)` — allocates two strings per field per frame
- **Line 398/810:** `matchingVars.ToArray()` — allocates new string array per dropdown per frame
- **Line 879-892:** `GetCompareOpNames` — returns `new string[]` on every call

These are editor-only but accumulate across many fields.

---

### 3.6 `BehaviourTreeEditor.BuildAssetBarMenu` — LINQ On Project Changes

**File:** [BehaviourTreeEditor.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Editor/BehaviourTreeEditor.cs#L171-L184)  
**Severity:** 🔵 Low  
**Hot path:** Semi-frequent — fires on `OnProjectChanged` (file recompile, asset import)

LINQ `.Select().Where().Select().OrderByDescending().Take()` creates multiple intermediate collections and anonymous-type allocations, plus `File.GetLastWriteTime` I/O.

---

## 4. Code Duplication (Consolidation Candidates)

### 4.1 Slot-Offset Calculation — 3 Identical Copies

| Location | File |
|---|---|
| `TreeBaker.ResolveSlotOffset` (477-490) | [TreeBaker.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Runtime/TreeBaker.cs#L477-L490) |
| `SquadInstance.ComputeBaseSlot` (227-240) | [SquadInstance.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Runtime/SquadInstance.cs#L227-L240) |
| `SquadSpawner.AssignRole` (186-192) | [SquadSpawner.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Runtime/SquadSpawner.cs#L186-L192) |

**Severity:** 🟡 Medium  
**Fix:** Extract a `public static int ComputeSlotOffset(IReadOnlyList<BlackboardVariableBase> variables, int targetIndex)` into a shared helper (e.g., on `BlackboardDefinition` or a utility class).

---

### 4.2 `DrawConstantField` vs `DrawConstantFieldForType`

**File:** [CustomNodeEditor.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Editor/NodeInspectorViewEditors/CustomNodeEditor.cs#L258-L317) vs lines 820-873  
**Severity:** 🟡 Medium

Nearly identical type-switch logic. `DrawConstantField` adds `isRoleDropdown`/`isOrderDropdown` branches. Consolidate by parameterizing the dropdown source.

---

### 4.3 `DrawVariableDropdown` vs `DrawVariableDropdownWithSquadFilter`

**File:** [CustomNodeEditor.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Editor/NodeInspectorViewEditors/CustomNodeEditor.cs#L320-L403) vs lines 739-813  
**Severity:** 🟡 Medium

~80% duplicated filtering, population, and rendering logic. Only differences: stride filter polarity (`> 1` vs `<= 1`), labels, and read-only proxy display.

---

### 4.4 `CreateNewSquad` vs `OnCreateNewSquadNavClicked`

**File:** [SquadDefinitionEditor.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Editor/SquadDefinitionEditor.cs#L338-L362) vs lines 385-409  
**Severity:** 🔵 Low

~25 identical lines. Only difference is the sub-asset name (`_BB_Definition` vs `_Schema`).

---

### 4.5 `OnAddBindingClicked` vs `OnSceneAddClicked`

**File:** [TrackedVariablesView.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Editor/TrackedVariablesView.cs#L195-L227)  
**Severity:** 🔵 Low

~15 identical lines creating a `ComponentMemberSearchProvider` and opening a SearchWindow.

---

### 4.6 `EvaluateCommander` Debug Provider Update Duplicates Base Class

**File:** [CommanderTreeRunner.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Runtime/CommanderTreeRunner.cs#L152-L157)  
**Severity:** 🔵 Low

Lines 152-157 duplicate the debug-provider update from `BehaviourTreeRunnerBase.Evaluate()` (lines 80-85). Call `base.Evaluate()` instead.

---

### 4.7 Clone Fallback Logic Duplication in `TreeBaker`

**File:** [TreeBaker.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Runtime/TreeBaker.cs)  
**Severity:** 🔵 Low

The clone-with-reflection fallback in `CopyGenericVariables` (447-464) mirrors `ResolveSubtreeVarIndex` (297-316). Extract a shared private method.

---

## 5. Code Smells & Naming

### 5.1 `GetRunnerSquads` — Runtime Type Dispatch

**File:** [CommanderTreeRunner.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Runtime/CommanderTreeRunner.cs#L87-L102)  
**Severity:** 🟡 Medium

```csharp
if (runner is AgentTreeRunner agentRunner) ... 
else if (runner is CommanderTreeRunner commanderRunner) ...
```

This violates open-closed principle and makes adding new runner types brittle. Expose `registeredSquads` as a `protected virtual` or `abstract` member on the base class.

---

### 5.2 `NodeMethod.ReadConstant` — Unreachable Code

**File:** [NodeMethod.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Core/NodeMethod.cs#L371-L377)  
**Severity:** 🔵 Low

The `fd.IsBoxedConstant` branch inside `ReadConstant` is dead code. The caller (`DeserializeFields`) already branches on `IsBoxedConstant` separately at line 292. `IsConstant` and `IsBoxedConstant` are mutually exclusive, and `ReadConstant` is only called when `IsConstant` is true.

---

### 5.3 Debug.Log Left in Production

| File | Line | Message |
|---|---|---|
| [TreeEvaluator.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Runtime/TreeEvaluator.cs#L38) | 38 | `"Created Tree Evaluator"` |
| [VariableMethods.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Runtime/Methods/VariableMethods.cs#L75) | 75 | `"SetVariable: slot={...}"` — per execution |
| [VariableMethods.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Runtime/Methods/VariableMethods.cs#L144) | 144 | LogVariable output |
| [VariableMethods.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Runtime/Methods/VariableMethods.cs#L148) | 148 | LogVariable output |
| [ForEachAgentMethod.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Runtime/Methods/ForEachAgentMethod.cs#L34) | 34 | Per-agent log |
| [ForEachAgentMethod.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Runtime/Methods/ForEachAgentMethod.cs#L38) | 38 | Per-agent log |

**Severity:** 🔵 Low (but some are hot-path)

Wrap in `#if UNITY_EDITOR` or remove. The `ForEachAgentMethod` ones are particularly harmful (see §3.2).

---

## 6. Edge-Case & Null-Safety Gaps

### 6.1 `BehaviourTreeRunnerBase.Initialize` — Missing Null Check

**File:** [BehaviourTreeRunnerBase.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Runtime/BehaviourTreeRunnerBase.cs#L60)  
**Severity:** 🟡 Medium

```csharp
blackBoard.Initialize(runtimeAsset.blackboardDefinition);
```

If `runtimeAsset` was baked but `blackboardDefinition` is null, this crashes. Add a null guard.

---

### 6.2 `CustomNodeEditor` — `fieldName[0]` on Empty String

**File:** [CustomNodeEditor.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Editor/NodeInspectorViewEditors/CustomNodeEditor.cs#L191)  
**Severity:** 🟡 Medium

```csharp
char.ToUpper(info.fieldName[0]) + info.fieldName.Substring(1)
```

If `fieldName` is empty, this throws `IndexOutOfRangeException`. Guard with `string.IsNullOrEmpty`.

---

### 6.3 `TrackedVariablesView` — NPE on Missing UXML Elements

**File:** [TrackedVariablesView.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Editor/TrackedVariablesView.cs#L197-L225)  
**Severity:** 🟡 Medium

`OnAddBindingClicked` and `OnSceneAddClicked` access `addBindingButton.worldBound` and `sceneAddButton.worldBound` without null-checking the buttons first. If the UXML template changes, this crashes.

---

### 6.4 `ManagedBlackboardStorage.Get<T>` — Silent Catch

**File:** [ManagedBlackboardStorage.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Core/ManagedBlackboardStorage.cs#L220)  
**Severity:** 🔵 Low

Empty `catch { }` swallows `Convert.ChangeType` exceptions silently in builds (the warning is `#if UNITY_EDITOR` only). Consider logging a one-time warning in builds too.

---

### 6.5 `OrderSearchProvider` — Serializable Texture Field

**File:** [OrderSearchProvider.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Editor/OrderSearchProvider.cs#L16)  
**Severity:** 🔵 Low

```csharp
private Texture2D identationIcon;  // typo: should be "indentation"
```

Not marked `[NonSerialized]` — Unity will attempt to serialize this reference, producing a broken asset reference after domain reload. Add `[NonSerialized]` and fix the typo.

---

## 7. Style Violations

### 7.1 Same-Line Assignment Body

**File:** [ManagedBlackboardStorage.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Core/ManagedBlackboardStorage.cs#L117)  
**Severity:** 🔵 Low

```csharp
if (stride <= 1) stride = 1;
```

Violates project rule: only `return` statements may appear on the same line as `if` without braces. Use:

```csharp
if (stride <= 1) { stride = 1; }
```

### 7.2 Unused Using Directive

**File:** [BlackBoardDefinition.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Core/BlackBoardDefinition.cs#L4)  
**Severity:** 🔵 Low

```csharp
using UnityEngine.Serialization;  // unused
```

Remove it.

### 7.3 Nested Redundant Preprocessor

**File:** [RuntimeAssetHelper.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Runtime/RuntimeAssetHelper.cs#L34-L36)  
**Severity:** 🔵 Low

```csharp
#if UNITY_EDITOR      // outer
    // ...
    #if UNITY_EDITOR  // inner — redundant
```

Remove the inner `#if UNITY_EDITOR`.

---

## 8. Summary Table

| # | Issue | Severity | File |
|---|---|---|---|
| 1 | `CheckOrderMethod` never reads from BB | 🔴 Critical | CheckOrderMethod.cs |
| 2 | `SendOrderMethod` destroys slot offset | 🔴 Critical | SendOrderMethod.cs |
| 3 | `TreeEvaluator` ctor null check after access | 🔴 Critical | TreeEvaluator.cs |
| 4 | `ForEachRoleMethod` dynamic var + enum boxing | 🟠 High | ForEachRoleMethod.cs |
| 5 | `SquadInstance` fragile asset equality | 🟠 High | SquadInstance.cs |
| 6 | `GetMaxSquadDataStride` only first variable | 🟠 High | CommanderTreeRunner.cs |
| 7 | Stale `currentRunner` in `OnSelectTree` | 🟠 High | BehaviourTreeEditor.cs |
| 8 | `PushTrackedBindings` reflection per frame | 🟠 High | BehaviourTreeRunnerBase.cs |
| 9 | `Debug.Log` in per-agent loop | 🟡 Medium | ForEachAgentMethod.cs |
| 10 | `LogVariable` array allocation per call | 🟡 Medium | VariableMethods.cs |
| 11 | Slot-offset calc duplicated 3x | 🟡 Medium | TreeBaker, SquadInstance, SquadSpawner |
| 12 | `DrawConstantField` duplication | 🟡 Medium | CustomNodeEditor.cs |
| 13 | `DrawVariableDropdown` duplication | 🟡 Medium | CustomNodeEditor.cs |
| 14 | Runtime type dispatch in `GetRunnerSquads` | 🟡 Medium | CommanderTreeRunner.cs |
| 15 | `fieldName[0]` on empty string | 🟡 Medium | CustomNodeEditor.cs |
| 16 | Missing UXML element null checks | 🟡 Medium | TrackedVariablesView.cs |
| 17 | Missing `runtimeAsset.blackboardDefinition` null check | 🟡 Medium | BehaviourTreeRunnerBase.cs |
| 18 | `PollDebugState` per-frame editor hook | 🟡 Medium | BehaviourTreeEditor.cs |
| 19 | Per-inspector-frame string allocations | 🟡 Medium | CustomNodeEditor.cs |
| 20 | `BuildAssetBarMenu` LINQ on project change | 🔵 Low | BehaviourTreeEditor.cs |
| 21 | Unreachable code in `ReadConstant` | 🔵 Low | NodeMethod.cs |
| 22 | `Debug.Log` left in production (6 instances) | 🔵 Low | Multiple files |
| 23 | Empty `catch { }` in `Get<T>` | 🔵 Low | ManagedBlackboardStorage.cs |
| 24 | Serializable `identationIcon` texture | 🔵 Low | OrderSearchProvider.cs |
| 25 | `CreateNewSquad` / `OnCreateNewSquadNavClicked` dupe | 🔵 Low | SquadDefinitionEditor.cs |
| 26 | `OnAddBindingClicked` / `OnSceneAddClicked` dupe | 🔵 Low | TrackedVariablesView.cs |
| 27 | `EvaluateCommander` duplicates base-class debug update | 🔵 Low | CommanderTreeRunner.cs |
| 28 | Clone fallback duplication in TreeBaker | 🔵 Low | TreeBaker.cs |
| 29 | Same-line assignment body (style) | 🔵 Low | ManagedBlackboardStorage.cs |
| 30 | Unused `using UnityEngine.Serialization` | 🔵 Low | BlackBoardDefinition.cs |
| 31 | Nested redundant `#if UNITY_EDITOR` | 🔵 Low | RuntimeAssetHelper.cs |
| 32 | Runtime path requires Resources folder | 🔵 Low | OrderRegistry.cs |

---

## 9. Priority Action Items

### Immediate (before merge)
1. Fix `CheckOrderMethod` and `SendOrderMethod` — they are non-functional
2. Fix `TreeEvaluator` constructor null-check ordering
3. Fix `ForEachRoleMethod` dynamic variable path and enum boxing

### High Priority (next)
4. Fix `SquadInstance` asset matching to use GUIDs
5. Fix stale `currentRunner` in `BehaviourTreeEditor`
6. Pre-compile delegates in `PushTrackedBindings` to eliminate per-frame reflection
7. Remove `Debug.Log` from `ForEachAgentMethod` hot loop
8. Fix `GetMaxSquadDataStride` to consider all squad-data variables

### Medium Priority (this release cycle)
9. Consolidate slot-offset calculation into one shared method
10. Consolidate duplicated `DrawConstantField` and `DrawVariableDropdown` in CustomNodeEditor
11. Expose `registeredSquads` as virtual member to eliminate runtime type dispatch
12. Wrap remaining `Debug.Log` calls in `#if UNITY_EDITOR`

### Low Priority (backlog)
13. Fix style violations (unused usings, same-line bodies, redundant preprocessor)
14. Fix `OrderSearchProvider` typo and serialization hazard
15. Consolidate `CreateNewSquad`/`OnCreateNewSquadNavClicked` and `OnAddBindingClicked`/`OnSceneAddClicked`
