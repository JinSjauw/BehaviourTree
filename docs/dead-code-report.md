# Dead Code Report

> Generated 2026-06-20 | Scanned: ~100 `.cs` files in `Assets/Scripts/BehaviourTree`  
> Method: `Grep`-verified callers for every candidate

---

## Severity Legend

| Icon | Meaning |
|------|---------|
| 🔴 | Entire type/file deletable — zero consumers |
| 🟠 | Method/field unused — type still has live code |
| 🟡 | Unused constant — no runtime cost, just clutter |
| 🟢 | Dead but could become live (planned API, commented out) |

---

## 1. Entire Types / Files — Deletable

| # | Severity | File | Lines | What | Why |
|---|----------|------|-------|------|-----|
| 1 | 🔴 | [CommanderTypes.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Core/CommanderTypes.cs) | 7-39 | `TacticalRole`, `FormationType`, `TacticPhase` enums | Zero references anywhere — planned commander data types never integrated |
| 2 | 🔴 | [IBehaviourTreeAuthoringAsset.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Core/IBehaviourTreeAuthoringAsset.cs) | 3-8 | Interface `IBehaviourTreeAuthoringAsset` | Zero implementations, never referenced |
| 3 | 🔴 | [ByteHelper.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Utility/ByteHelper.cs) | 6-36 | `ByteHelper.StructToBytes<T>()`, `ByteArrayToFixedBuffer()` | Zero callers anywhere in the codebase |
| 4 | 🔴 | [NodeToolTipRegistry.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Editor/NodeToolTipRegistry.cs) | 5-11 | `NodeToolTipRegistry` class | Empty stub (comments only), `[CreateAssetMenu]` exists but never referenced from code. Replaced by `TooltipRegistry` |

---

## 2. Unused Enum Values

| # | Severity | File | Line | What | Why |
|---|----------|------|------|------|-----|
| 5 | 🔴 | [BlackBoard.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Core/BlackBoard.cs) | 10 | `BlackBoardType.SQUAD` | Only `BlackBoardType.SELF` is used (TreeBaker.cs:347). `SQUAD` has zero reads anywhere |

---

## 3. Uncalled Methods

| # | Severity | File | Lines | What | Why |
|---|----------|------|-------|------|-----|
| 6 | 🟠 | [BlackBoard.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Core/BlackBoard.cs) | 235-263 | `SaveReferencesByName(IReadOnlyList<BlackboardVariableBase>)` | Only `SaveReferencesByNameFromSnapshot` variant (line 270) is called. Original overload superseded |
| 7 | 🟠 | [SquadDefinition.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Core/SquadDefinition.cs) | 93-101 | `GetBindingGroup()` | `GetOrCreateBindingGroup()` (line 74) is used everywhere. Read-only variant superseded |
| 8 | 🟠 | [BaseEditorTreeAsset.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Editor/BaseEditorTreeAsset.cs) | 20-44 | `Initialize()` | Tree population happens via `GraphView.PopulateView()` + `AssetDatabase.LoadAllAssetsAtPath`. This method is never called |
| 9 | 🟠 | [BaseEditorTreeAsset.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Editor/BaseEditorTreeAsset.cs) | 231-242 | `ClearNodes()` | Cleanup handled by `GraphView.ClearView()` → `DeleteElements(graphElements)`. No caller of this method |
| 10 | 🟠 | [CommanderTreeRunner.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Runtime/CommanderTreeRunner.cs) | 174, 210 | `RegisterAgent()`, `UnregisterAgent()` | Public API, zero callers. Agent list populated via Inspector serialization, not programmatically |
| 11 | 🟠 | [CommanderTreeRunner.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Runtime/CommanderTreeRunner.cs) | 311, 325 | `RegisterSquad()`, `UnregisterSquad()` | Public API, zero callers. Planned but unused |
| 12 | 🟠 | [TreeRunner.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Runtime/TreeRunner.cs) | 62, 77 | `AgentTreeRunner.RegisterSquad()`, `UnregisterSquad()` | Public API, zero callers. `registeredSquads` field (line 21) is therefore always empty |

---

## 4. Unpopulated Field (Dead by Association)

| # | Severity | File | Line | What | Why |
|---|----------|------|------|------|-----|
| 13 | 🟠 | [TreeRunner.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Runtime/TreeRunner.cs) | 21 | `AgentTreeRunner.registeredSquads` | Read by `CommanderTreeRunner.GetRunnerSquads()` (line 119), but `RegisterSquad()` is never called → list is always empty → read path dead |

---

## 5. Unused Path Constants

| # | Severity | File | Line | What | Why |
|---|----------|------|------|------|-----|
| 14 | 🟡 | [BehaviourTreeEditorPaths.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Editor/BehaviourTreeEditorPaths.cs) | 14 | `GraphNoteUss` | Zero C# references. USS may apply via UXML but no code evidence |
| 15 | 🟡 | [BehaviourTreeEditorPaths.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Editor/BehaviourTreeEditorPaths.cs) | 22 | `ArrayElementRowUss` | Only `ArrayElementRowUxml` is used (BlackBoardView.cs:127). USS constant never referenced |
| 16 | 🟡 | [BehaviourTreeEditorPaths.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Editor/BehaviourTreeEditorPaths.cs) | 30 | `TrackedBindingRowUss` | Only `TrackedBindingRowUxml` is used (TrackedVariablesView.cs:361). USS constant never referenced |

---

## 6. Commented-Out Code

| # | Severity | File | Line | What | Why |
|---|----------|------|------|------|-----|
| 17 | 🟢 | [BehaviourTreeEditorGraphView.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Editor/BehaviourTreeEditorGraphView.cs) | 798-801 | `SetupRuntimeDebugProxies` — commented out | Logic inlined into `PopulateView()` (line 649). Original left as comment instead of deleted |

---

## What Was Not Found

- No unreachable `default:` switch cases (all enum values handled or default is reachable)
- No dead `catch` blocks
- No `#if false` / `#if UNITY_EDITOR && false` blocks
- No `[Obsolete]` attributes
- No unused `private static` methods (all called at least once)
- All `#if UNITY_EDITOR` blocks are standard Unity conditional compilation, active in editor builds

---

## Summary

| Category | Count | Impact if removed |
|----------|:-----:|-------------------|
| Deletable types/files | 4 | Remove ~120 lines, zero risk |
| Unused enum values | 1 | Remove 1 line |
| Uncalled methods | 7 | Remove ~120 lines, zero risk |
| Unpopulated field | 1 | Remove ~5 lines, zero risk |
| Unused path constants | 3 | Remove 3 lines |
| Commented-out code | 1 | Remove 4 lines |
| **Total** | **17** | **~250 lines removable** |

### Quick wins (🧹):

- **Delete `CommanderTypes.cs`** — 3 enums, zero references, 40 lines
- **Delete `ByteHelper.cs`** — 2 unused utility methods, 36 lines
- **Delete `IBehaviourTreeAuthoringAsset.cs`** — never-implemented interface, 8 lines
- **Delete `NodeToolTipRegistry.cs`** — empty stub, 11 lines
- **Delete `BlackBoardType.SQUAD`** — unused enum member, 1 line
- **Delete `BlackBoard.SaveReferencesByName(vars)` overload** — superseded by snapshot variant, 29 lines
- **Delete `BaseEditorTreeAsset.Initialize()` / `ClearNodes()`** — never called, 25 + 12 lines
