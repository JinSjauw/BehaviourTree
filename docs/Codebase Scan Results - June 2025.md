# Codebase Scan Results — June 2025

**Scope:** `d:\Dev\BehaviourTreeEditor\Assets\Scripts` — 80 .cs files across 4 assemblies  
**Categories:** Allocations in hot paths · Edge cases & bugs · Dead & redundant code  
**Total findings:** 57

---

## 1. Allocations in Hot Paths

### HIGH (per-frame allocations during tree evaluation)

#### 1. Boxing in `ManagedBlackboardStorage.Set<T>()` via `object[]` storage

- **File:** `Assets/Scripts/BehaviourTree/Core/ManagedBlackboardStorage.cs`
- **Line:** 156 — `values[index] = value;`
- **Explanation:** The `values` array is `object[]`. Every time a value type (int, float, bool, Vector2, Vector3, enum) is written to the blackboard, it boxes. Fires from `BlackBoard.Set<T>()`, `SetBoxed()`, `FieldBinding.WriteToBBGeneric()`, and both `CommanderBindingBridge.CopyAgentToCommander()` and `CopyCommanderToAgent()` — every frame per agent in commander mode.

#### 2. Boxing in `FieldBinding.ReadFromBBGeneric()` via `FieldInfo.SetValue()`

- **File:** `Assets/Scripts/BehaviourTree/Core/NodeMethod.cs`
- **Line:** 70 — `fieldInfo.SetValue(instance, value);`
- **Explanation:** `SetValue(object, object)` boxes the value parameter. Runs for every node's `ResolveInputsGeneric()` call — every tick per node.

#### 3. Boxing in `FieldBinding.WriteToBBGeneric()` via `FieldInfo.GetValue()`

- **File:** `Assets/Scripts/BehaviourTree/Core/NodeMethod.cs`
- **Line:** 76 — `object value = fieldInfo.GetValue(instance);`
- **Explanation:** `GetValue()` boxes value-type fields. Runs for every node's `WriteOutputsGeneric()` — every tick per node. Boxed value then flows into finding #1.

#### 4. Double box/unbox in `FieldReader.ReadPackedConstant<T>()`

- **File:** `Assets/Scripts/BehaviourTree/Runtime/FieldReader.cs`
- **Line:** 68 — `return (T)(object)fd.GetInt();`
- **Explanation:** `GetInt()` returns int. `(object)` boxes it. `(T)` unboxes. Pointless for `T = int`. Same pattern at lines 69-70 for float and bool.

#### 5. `Debug.Log()` with string interpolation in `BlackBoard.Set<T>(string)`

- **File:** `Assets/Scripts/BehaviourTree/Core/BlackBoard.cs`
- **Line:** 420 — `Debug.Log($"[Blackboard] Setting {keyName} to {value} at {index}");`
- **Explanation:** NOT guarded by `#if UNITY_EDITOR`. Allocates string every frame for each data provider. Must be guarded or removed.

#### 6. `Debug.Log()` with string interpolation in `BB_LogMethods`

- **File:** `Assets/Scripts/BehaviourTree/Runtime/Methods/BB_LogMethods.cs`
- **Lines:** 10, 40, 46, 57 — `Debug.Log($"{value}")` and variants
- **Explanation:** Log nodes allocate strings every execution. While debugging tools, when left in trees they generate per-frame allocations.

#### 7. Boxing in `FieldReader.GetEnum<T>()` via `blackboard.Get<object>()`

- **File:** `Assets/Scripts/BehaviourTree/Runtime/FieldReader.cs`
- **Line:** 143 — `object raw = blackboard.Get<object>(fd.value);`
- **Explanation:** Enum reads go through `object[]` storage. `Enum.ToObject()` on line 145 allocates new boxed enum.

#### 8. `Enum.ToObject()` at multiple call sites

- **File:** `Assets/Scripts/BehaviourTree/Runtime/FieldReader.cs`
- **Lines:** 71, 141, 145
- **Explanation:** `Enum.ToObject()` allocates a new boxed enum instance every call. Line 71 fires for constant enum field reads; lines 141/145 for BB variable reads.

**Root cause:** `object[]` in `ManagedBlackboardStorage` forces boxing on every value-type access. Reflection-based `FieldInfo.GetValue/SetValue` in `NodeMethod` compounds this.

---

### MEDIUM (per-frame but fewer calls, or conditionally triggered)

#### 9. `new ParallelChildState[]` on abort

- **File:** `Assets/Scripts/BehaviourTree/Runtime/Methods/ParallelMethod.cs`
- **Line:** 32 — `children = new ParallelChildState[childCount];`
- **Explanation:** Array reallocated on every abort (line 26: `children = null`) and on first entry. Not every frame but every abort cycle.

#### 10. String interpolation in `LeafTick.TickLeaf()` error path

- **File:** `Assets/Scripts/BehaviourTree/Runtime/LeafTick.cs`
- **Line:** 16 — `$"Method instance not found..."`
- **Explanation:** `Debug.LogError` with interpolation allocates. Fires only when leaf has no method instance (corruption/error).

#### 11. String interpolation in `CompositeTick.TickComposite()` error path

- **File:** `Assets/Scripts/BehaviourTree/Runtime/CompositeTick.cs`
- **Line:** 23 — `$"Composite method instance not found..."`
- **Explanation:** Same pattern as #10 but for composites.

#### 12. `GetComponent<T>()` in `TreeRunner.Initialize()` and `CommanderTreeRunner.Initialize()`

- **File:** `Assets/Scripts/BehaviourTree/Runtime/TreeRunner.cs` — Lines 133, 134, 137
- **File:** `Assets/Scripts/BehaviourTree/Runtime/CommanderTreeRunner.cs` — Line 139
- **Explanation:** One-time during init. But `GetComponentsInChildren` allocates a temp array internally by Unity. If `Initialize()` is called multiple times, allocations repeat.

---

### LOW (one-time, editor-only, or non-hot-path)

| # | File | Line(s) | Issue |
|---|---|---|---|
| 13 | `TreeBaker.cs` | 41, 55, 57-60, 63-64, 84, 163, 201, 241 | Multiple `new Dictionary<>`, `new List<>`, `new HashSet<>`, `.ToArray()` — one-time bake |
| 14 | `CommanderBindingBridge.cs` | 94, 95, 157, 158 | `new List<int>()` + `.ToArray()` — once during init |
| 15 | `MethodRegistry.cs` | 20, 35-42, 128, 148 | Reflection scanning — static ctor, once only |
| 16 | `TreeEvaluator.cs` | 29, 33-35, 38 | `new NodeState[]`, `new int[]`, `new bool[]`, `new NodeMethod[]` — one-time per instance |
| 17 | `BlackBoard.cs` | 56-88 | `Debug.Log` loops inside `#if UNITY_EDITOR` — stripped from builds |
| 18 | `NodeMethod.cs` | 152 | `new FieldBinding[bindings.Length]` — once per node method instance |

---

## 2. Edge Cases & Bugs

### HIGH

#### 1. Missing `EditorUtility.SetDirty` in `BehaviourTreeAsset.Initialize()`

- **File:** `Assets/Scripts/BehaviourTree/Editor/BehaviourTreeAsset.cs`
- **Lines:** 15-38
- **Code:** Mutates `root.children` (replacing with `Instantiate` copies) and `nodesList` — never calls `SetDirty`
- **Risk:** Modified child references may not persist between editor sessions or domain reloads. User data loss risk.

#### 2. Missing `EditorUtility.SetDirty` in `BehaviourTreeAsset.CreateBlackBoard()`

- **File:** `Assets/Scripts/BehaviourTree/Editor/BehaviourTreeAsset.cs`
- **Lines:** 41-49
- **Code:** Creates `BlackboardDefinition` sub-asset, assigns to `blackboardDefinition` field, calls `AssetDatabase.SaveAssets()` — but never `SetDirty(this)`
- **Risk:** Assignment may be lost on domain reload or scene save.

#### 3. Null reference on port access in `CopyPasteHandler.PasteNodes()`

- **File:** `Assets/Scripts/BehaviourTree/Editor/CopyPasteHandler.cs`
- **Lines:** 139-140
- **Code:** `sourceView.output` and `targetView.input` accessed without null checks; `ConnectTo` called directly
- **Risk:** Crashes editor during paste if port initialization hasn't completed or ports don't exist for the node type.

#### 4. `FieldReader` index parameter has no bounds check

- **File:** `Assets/Scripts/BehaviourTree/Runtime/FieldReader.cs`
- **Lines:** 38, 54, 80 — all `Get*` and `Set*` methods
- **Code:** `ref readonly FieldData fd = ref fields[index];` — no bounds validation in 18 methods
- **Risk:** `IndexOutOfRangeException` during tree evaluation if baked field count mismatches runtime expectation.

---

### MEDIUM

#### 5. `BehaviourTreeEditor.BakeTree`: hardcoded file path without collision check

- **File:** `Assets/Scripts/BehaviourTree/Editor/BehaviourTreeEditor.cs`
- **Lines:** 279-281 — `string path = $"Assets/{runtimeAsset.name}.asset";`
- **Risk:** Path collision throws exception if another baked asset has the same name. No `GenerateUniqueAssetPath` usage.

#### 6. `SequenceMethod` / `SelectorMethod`: `NONE` state silently passed as non-terminal

- **Files:** `Assets/Scripts/BehaviourTree/Runtime/Methods/SequenceMethod.cs`, `SelectorMethod.cs`
- **Lines:** 27-44
- **Risk:** Loop only checks RUNNING and FAILURE/SUCCESS. `NONE` falls through to `child++` — silently treated as pass in Sequence, fail in Selector. No defensive guard.

#### 7. `ManagedBlackboardStorage.GetVariableSlotRange` returns sentinel `(-1, -1)` without caller validation

- **File:** `Assets/Scripts/BehaviourTree/Core/ManagedBlackboardStorage.cs`
- **Lines:** 97-118
- **Risk:** Callers use returned sentinel as slot index. `CommanderBindingBridge` stores `-1` into binding arrays — bridge copies corrupt blackboard data silently.

#### 8. `CommanderBindingBridge`: `agentID` added to `commanderBase` without stride/bounds validation

- **File:** `Assets/Scripts/BehaviourTree/Runtime/CommanderBindingBridge.cs`
- **Lines:** 191, 213 — `(stride > 1) ? commanderBase + agentID : commanderBase`
- **Risk:** If `agentID >= stride`, indexes past array bounds. Storage bounds-check saves from crash but data is silently misrouted.

---

### LOW

| # | File | Line(s) | Issue |
|---|---|---|---|
| 9 | `BehaviourTreeEditor.cs` | 121-135 | `OnPlayModeStateChanged` switch missing `ExitingEditMode` case — no `default` guard |
| 10 | `CopyPasteHandler.cs` | 72 | `sourceNodeView.NodeSO.guid` accessed without null check on `NodeSO` |
| 11 | `BlackBoardEditor.cs` | 256-261 | `SetDirty` called before `ApplyModifiedProperties` — minor ordering concern |
| 12 | `TickContext.cs` | 67 | `handlers[(int)nodeType]` — no bounds check; safe for valid enums, crashes on corrupted data |
| 13 | `RuntimeAssetHelper.cs` | 31, 55 | `DontSaveInBuild` flag pattern — no actual bug, note only |

---

## 3. Dead & Redundant Code

### Unused Types (HIGH waste)

#### 1. `FieldReader` — 125+ lines dead

- **File:** `Assets/Scripts/BehaviourTree/Runtime/FieldReader.cs` — Lines 13-260
- **Issue:** `public ref struct` with 10 typed Get/Set methods, 2 constructors, internal field logic. **Never instantiated or referenced anywhere in the codebase.** Actual field deserialization is done directly in `NodeMethod.DeserializeFields()`.

#### 2. `ByteHelper` — completely unused utility class

- **File:** `Assets/Scripts/BehaviourTree/Utility/ByteHelper.cs` — Lines 1-20+
- **Issue:** `public static class` with struct-to-byte-array marshalling. **Never called from any file.** Only `using BehaviourTree.Utility;` is in `TreeBaker.cs` which never uses `ByteHelper`.

---

### Duplicate Logic (MEDIUM)

#### 1. `GetSourceTree()` duplicated in two runners

- **Files:** `TreeRunner.cs` (141-149), `CommanderTreeRunner.cs` (163-171)
- **Issue:** Identical 9-line implementation in both. Can be extracted to `RuntimeAssetHelper`.

---

### Test Code in Runtime Assembly (MEDIUM)

- **File:** `Assets/Scripts/BehaviourTree/Runtime/Methods/CommanderTestMethods.cs`
- **Issue:** 5 test-only classes (`TEST_CommanderAssign`, `TEST_CheckRole`, `TEST_AgentExecute`, `TEST_AgentReportHealth`, `TEST_AgentExecuteOrder`) with `[NodeMethod]` attributes in runtime assembly. Should move to test assembly or wrap in `#if UNITY_EDITOR`.

---

### Unused Field (MEDIUM)

- **File:** `Assets/Scripts/BehaviourTree/Runtime/Methods/BB_LogMethods.cs`
- **Line:** 9 — `private string methodName;`
- **Issue:** Declared but never assigned or read anywhere.

---

### Commented-Out Code (LOW)

| # | File | Lines | Content |
|---|---|---|---|
| 1 | `BehaviourTreeEditorGraphView.cs` | 423-432 | `CreateCompositeNode(BehaviourNodeType, Vector2)` — replaced by string overload |
| 2 | `BehaviourTreeEditorGraphView.cs` | 713-716 | `SetupRuntimeDebugProxies(TreeRunner)` — moved to `RuntimeDebugManager` |
| 3 | `BehaviourTreeEditorGraphView.cs` | 453 | `//Undo.RecordObject(node, ...)` in `CreateLeafNode` |
| 4 | `BehaviourTreeEditorGraphView.cs` | 466 | `//Undo.RecordObject(node, ...)` in `CreateDecoratorNode` |
| 5 | `NodeMethod.cs` | 200, 223 | 2 commented-out `Debug.Log` trace lines |
| 6 | `CommanderBindingBridge.cs` | 87-89, 139-141, 194 | 3 commented-out `Debug.Log` blocks |
| 7 | `MethodRegistry.cs` | 60 | 1 commented-out `Debug.Log` |
| 8 | `CustomNodeEditor.cs` | 108 | `//EditorGUILayout.PropertyField(blackBoardTypeIDProp);` |
| 9 | `LeafNode.cs` | 6 | `//[CreateAssetMenu(...)]` |
| 10 | `DecoratorNode.cs` | 6 | `//[CreateAssetMenu(...)]` |
| 11 | `RootNode.cs` | 5 | `//[CreateAssetMenu(...)]` |

---

### Redundant Using Directives (LOW)

| File | Line | Redundant Import |
|---|---|---|
| `BehaviourTreeEditor.cs` | 8 | `using BehaviourTree.Editor;` — file already in this namespace |
| `InspectorView.cs` | 5 | `using BehaviourTree.Editor;` — file already in this namespace |
| `BlackBoardView.cs` | 5 | `using BehaviourTree.Editor;` — file already in this namespace |
| `BehaviourNodeView.cs` | 1 | `using BehaviourTree;` — parent namespace auto-accessible |
| `NodeMethod.cs` | 3 | `using BehaviourTree;` — parent namespace auto-accessible |
| `RuntimeBTreeAsset.cs` | 1 | `using BehaviourTree;` — no BehaviourTree types used in file |
| `TreeBaker.cs` | 2 | `using BehaviourTree.Utility;` — `ByteHelper` never used |

---

### Other (LOW)

| # | File | Line | Issue |
|---|---|---|---|
| 1 | `TreeEvaluator.cs` | 29 | `Debug.Log("Created Tree Evaluator")` — unconditional, fires in builds |
| 2 | `CopyPasteHandler.cs` | 164 | `#region Helpers` wraps a single small block — remove directive |
| 3 | `NodeMethod.cs` | 247 | `OnAbort` — empty virtual, intentional hook, called from 1 place, never overridden |

---

## Summary

| Category | HIGH | MEDIUM | LOW | Total |
|---|---|---|---|---|
| Allocations in hot paths | 8 | 4 | 6 | 18 |
| Edge cases & bugs | 4 | 4 | 5 | 13 |
| Dead & redundant code | 2 | 4 | 20 | 26 |
| **Total** | **14** | **12** | **31** | **57** |
