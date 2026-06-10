# Behaviour Tree Editor — System Documentation

## 1. Method Registration System (Current Implementation)

### Overview

Methods (Actions, Conditions, Decorators) are now class-based. To add a new method, inherit from the appropriate base class and implement `Execute()`. Registration is fully automatic — no enum, no attributes, no codegen.

### Adding a new method

```csharp
// Runtime/Methods/MyNewAction.cs
using BehaviourTree.Core;

namespace BehaviourTree.Runtime
{
    public class MyNewAction : ActionMethod
    {
        [SharedVar] public int someVariable;   // auto-resolved from BB each tick
        public float someConstant;             // baked constant from editor

        public override NodeState Execute()
        {
            // someVariable already holds the BB value (resolved by ResolveInputs)
            // someConstant was set during tree init (deserialized from FieldData)
            return NodeState.SUCCESS;
        }
    }
}
```

That's it. The class appears in the search menu immediately after recompilation.

### Field categories

| Category | Defined by | Populated | Persists |
|---|---|---|---|
| **Baked constant** | Public field, no `[SharedVar]` | `DeserializeFields()` during tree init | Yes, on instance |
| **Local mutable state** | Private field | Default value, mutated by `Execute()` | Yes, on instance (timers, counters) |
| **Shared variable** | `[SharedVar]` attribute | `ResolveInputs()` reads from BB before each `Execute()`; `WriteOutputs()` writes back after | Via blackboard |
| **Toggle variable** | `[SharedVar(true)]` attribute | Same as Shared, but inspector can toggle between constant and BB reference | Either constant or BB |

### Base class hierarchy

```
Core/NodeMethod.cs

  NodeMethod                         ← abstract base
    ├── ActionMethod : NodeMethod    ← abstract, Execute() → NodeState
    ├── ConditionMethod : NodeMethod ← abstract, Execute() → NodeState
    └── DecoratorMethod : NodeMethod ← abstract, Execute(NodeState childResult) → NodeState
```

### Core types (all in `Core/`)

| File | Contents |
|---|---|
| `NodeMethod.cs` | `NodeMethod`, `ActionMethod`, `ConditionMethod`, `DecoratorMethod`, `FieldBinding` |
| `NodeMethodAttribute.cs` | Optional `[NodeMethod("name")]` for custom stable names |
| `IBlackBoardAccess.cs` | Interface for typed slot read/write (`GetInt/SetInt/GetFloat/...`) |
| `BlackBoard.cs` | `MonoBehaviour` implementing `IBlackBoardAccess` via explicit interface |
| `BTreeVarAttribute.cs` | `[SharedVar]` and `[SharedArray]` attributes |

### How FieldBinding works

`FieldBinding` is a per-field descriptor created during `MethodRegistry` scanning. Each public instance field on a method class gets one.

```
FieldBinding:
  FieldInfo fieldInfo       ← reflection handle to the field
  FieldType fieldType       ← Int/Float/Bool/Vector2/Vector3/GameObject/Transform
  int bbSlotIndex           ← -1 = constant; >=0 = BB slot index
  bool isOutput             ← true = write back to BB after Execute
```

Lifecycle:
1. **Scan time** (`MethodRegistry` static constructor): `CreateBindings(Type)` scans public fields, detects `[SharedVar]`, sets `isOutput`
2. **Tree init** (`TreeEvaluator` constructor): `DeserializeFields(FieldData[])` — constants set directly on instance fields; SharedVars get `bbSlotIndex` from FieldData
3. **Pre-tick** (`ResolveInputs(IBlackBoardAccess)`): copies BB values into `[SharedVar]` instance fields
4. **Post-tick** (`WriteOutputs(IBlackBoardAccess)`): copies `[SharedVar]` instance fields back to BB

### MethodRegistry

`Runtime/MethodRegistry.cs` — static class with static constructor.

- `static MethodRegistry()` — scans all assemblies for `NodeMethod` subclasses, builds `Dictionary<string, Type>` (method name → type) and `Dictionary<Type, FieldBinding[]>` (type → field bindings)
- `CreateInstance(string name)` — `Activator.CreateInstance(type)` for a method name
- `GetCategory(string name)` — returns `BehaviourNodeType.ACTION/CONDITION/DECORATOR` based on base class
- `GetMethodNames()` — returns all registered method name strings
- `OnRegistryRebuilt` event — fires after every scan; UI subscribes to invalidate caches

### Lifecycle per tick

```
TreeEvaluator.Evaluate(blackBoard):
  for each node:
    NodeMethod method = methodInstances[nodeIndex]
    method.ResolveInputs(blackBoard)    ← BB → instance fields
    result = method.Execute()            ← pure domain logic
    method.WriteOutputs(blackBoard)      ← instance fields → BB
```

### Data flow diagram

```
┌─ Editor ──────────────────────────────────────────────────────────┐
│                                                                     │
│  MethodMetadataCache                                                │
│    Scans NodeMethod subclasses → field schema → ParamInfo[]         │
│                                                                     │
│  CustomNodeEditor                                                   │
│    Reads methodName string from LeafNode/DecoratorNode              │
│    Gets ParamInfo[] → draws inspector fields                        │
│    Saves field entries to NodeFieldEntry[] on the node              │
│                                                                     │
│  NodeSearchProvider                                                 │
│    Reads MethodRegistry.GetMethodNames() → builds search menu       │
│    OnSelect → CreateLeafNode(methodName) / CreateDecoratorNode()    │
│                                                                     │
└──────────────────────────────────┬──────────────────────────────────┘
                                   │
┌─ Bake (TreeBaker) ──────────────▼──────────────────────────────────┐
│                                                                     │
│  For each LeafNode/DecoratorNode:                                   │
│    NodeData.methodName = node.methodName   (string)                  │
│    PackFieldEntry() for each NodeFieldEntry → FieldData[]           │
│      Constants → FieldData.FromConstant(value)                      │
│      SharedVars → FieldData.FromVariable(slotIndex)                 │
│                                                                     │
└──────────────────────────────────┬──────────────────────────────────┘
                                   │
┌─ Runtime ───────────────────────▼──────────────────────────────────┐
│                                                                     │
│  TreeEvaluator constructor:                                         │
│    For each NodeData with methodName:                               │
│      instance = MethodRegistry.CreateInstance(methodName)           │
│      instance.DeserializeFields(fieldSlice, bindings)               │
│      methodInstances[nodeIndex] = instance                          │
│                                                                     │
│  TreeEvaluator.Evaluate(blackBoard):                                │
│    dispatches through handlers → EvaluateLeaf → method.Execute()    │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

### Files created during rewrite

| File | Purpose |
|---|---|
| `Core/NodeMethod.cs` | Base classes + FieldBinding |
| `Core/NodeMethodAttribute.cs` | Optional `[NodeMethod("name")]` |
| `Core/IBlackBoardAccess.cs` | BB access contract |
| `Runtime/Methods/DecoratorMethods.cs` | Inverter, Repeater |
| `Runtime/Methods/TimeMethods.cs` | WaitSeconds, Cooldown |
| `Runtime/Methods/BB_CompareMethods.cs` | All BB_Compare* |
| `Runtime/Methods/BB_CheckMethods.cs` | BB_Check*, BB_Edge*, BB_HasChanged* |
| `Runtime/Methods/BB_SetMethods.cs` | BB_Set*, BB_Clear*, BB_Toggle* |
| `Runtime/Methods/BB_LogMethods.cs` | All BB_Log* |
| `Runtime/Methods/CommanderTestMethods.cs` | TEST_* stubs |

### Files deleted during rewrite

`MethodID.cs`, `MethodCategoryAttribute.cs`, `GenerateNodeFieldBindingsAttribute.cs`, `DecoratorMethod.cs` (old delegate), `StandardMethods.cs`, `StandardMethods.Blackboard.cs`, all `*_NodeFields` structs (6 files), `NodeFieldBindings.cs`, `NodeFieldBindingGenerator.cs`

---

## 2. Tick-Based Evaluator Refactor (Implemented)

### What changed — old vs new

The old evaluator used a **stack-based** depth-first traversal (`EvaluatorFrame[]` + `while` loop + `INodeHandler.Process()`). The new evaluator uses a **tick-based** recursive dispatch with fixed-size arrays.

| | Old (stack) | New (tick) |
|---|---|---|
| State | `EvaluatorFrame[]` stack (variable depth) | `int[] activeChildIndex` (fixed per node, 1 int each) |
| Composites | Push child → recurse → pop | Tick child → check result → advance or save index |
| Parallel | Inline leaf evaluation only | **All children ticked every frame** (supports nested composites) |
| Dispatch | `INodeHandler.Process()` virtual call | Static delegate table `TickHandler[]` |
| Control flow | `while (frameCount > 0)` loop | `TickNode(0)` → recursive depth-first dispatch |
| Frame cycle | `frameCount == 0` triggers re-init + `Array.Clear(nodeStates)` | `activeChildIndex` reset on composite completion; no clearing needed |

### Architecture

```
TickDispatcher.TickNode(nodeIndex, ref ctx)
    │
    ├── TickLeaf        (ACTION / CONDITION)
    ├── TickSequence    (SEQUENCE)
    ├── TickSelector    (SELECTOR)
    ├── TickPriority    (PRIORITY)
    ├── TickDecorator   (DECORATOR)
    ├── TickParallel    (PARALLEL)
    └── TickSubtree     (SUBTREE)
```

All tick functions are `internal static` methods on the `partial class TickFunctions`, split across files by node category. The `TickDispatcher` holds a fixed-size `TickHandler[]` delegate table (indexed by `BehaviourNodeType`), initialized in its static constructor.

### TickContext

```csharp
struct TickContext {
    NodeData[] nodeDatas;              // baked tree structure (read-only ref)
    NodeMethod[] methodInstances;      // per-node method instances (read-only ref)
    NodeState[] nodeStates;            // mutable: per-node status, persists across ticks
    int[] activeChildIndex;            // mutable: resumption point for composites
    Dictionary<int, ParallelChildState[]> parallelStates;  // mutable: Parallel child tracking
    BlackBoard blackBoard;             // per-frame: current blackboard
}
```

`TickContext` is a value-type struct populated by `TreeEvaluator.Evaluate()` each frame. It's passed `ref` to all tick functions so mutations to `nodeStates`, `activeChildIndex`, and `parallelStates` survive the recursive descent.

**Key insight**: `nodeStates` and `activeChildIndex` are never cleared between frames. State naturally resets when composites reach their terminal condition (e.g., Sequence exhausts all children → resets `activeChildIndex` to 0 → returns SUCCESS). The next `Evaluate()` call picks up from that clean state.

### How each composite resets

Every composite tick function resets `activeChildIndex[nodeIndex] = 0` on **all three** exit paths:

| Exit | Sequence | Selector |
|---|---|---|
| Stop condition hit | `FAILURE` → reset | `SUCCESS` → reset |
| All children exhausted | `SUCCESS` → reset | `FAILURE` → reset |
| RUNNING (mid-sequence) | Save current `child`, resume next tick | Save current `child`, resume next tick |

This is critical — without the reset on the stop-condition path, the next evaluation cycle would resume from a stale child index. (The old stack-based evaluator was immune to this because the frame stack was rebuilt from scratch each cycle.)

### TreeEvaluator.Evaluate() flow

```
Evaluate(blackBoard):
    1. Populate tickContext fields from stored arrays + current blackBoard
    2. Guard: if nodeDatas[0].firstChildIndex < 0 → return (empty tree)
    3. result = TickDispatcher.TickNode(0, ref tickContext)   // start from effective root
    4. nodeStates[0] = result
    5. currentNodeIndex = 0
```

That's it. No `while` loop, no `UnwindSpecialComposites()`, no frame stack manipulation. The entire evaluation is a single recursive call chain driven by the node structure.

### Files created

| File | Purpose |
|---|---|
| `Runtime/TickContext.cs` | `TickContext` struct, `ParallelChildState`, `TickHandler` delegate, `TickDispatcher` |
| `Runtime/LeafTick.cs` | `TickLeaf` — resolves inputs, executes Action/Condition, writes outputs |
| `Runtime/CompositeTick.cs` | `TickSequence`, `TickSelector`, `TickPriority` |
| `Runtime/DecoratorTick.cs` | `TickDecorator` — ticks child, passes result through decorator method |
| `Runtime/ParallelTick.cs` | `TickParallel` — all children ticked every frame |
| `Runtime/SubtreeTick.cs` | `TickSubtree` — delegates to expanded first child |

### Files modified

| File | Changes |
|---|---|
| `Runtime/TreeEvaluator.cs` | Replaced `EvaluatorFrame[] stack` + `while` loop with `int[] activeChildIndex` + `TickNode(0)` dispatch. Constructor unchanged except replacing `frameStack`/`EvaluatorContext` with `activeChildIndex`/`TickContext`. `Evaluate()` simplified to 6 lines. |

### Files deleted (12)

`Runtime/Handlers/INodeHandler.cs`, `NodeHandlerRegistry.cs`, `EvaluatorContext.cs`, `ActionHandler.cs`, `ConditionHandler.cs`, `DecoratorHandler.cs`, `SequenceHandler.cs`, `SelectorHandler.cs`, `ParallelHandler.cs`, `PriorityHandler.cs`, `SubtreeHandler.cs`, `CompositeHandler.cs`

### What stays (unchanged)

`NodeData[]`, `FieldData[]`, `NodeMethod[] methodInstances`, `NodeState[] nodeStates`, `MethodRegistry`, all method classes, `TreeBaker`, `RuntimeBehaviourTreeAsset`, `TreeRunner.cs`, `CommanderTreeRunner.cs`.

### Key behavioral differences

1. **Parallel now supports nested composites** — old implementation inline-evaluated only leaf children (`EvaluateLeaf`). New implementation calls `TickNode` for each child, so Parallel can contain Sequence/Selector/Decorator/etc.

2. **No `Array.Clear(nodeStates)`** — the old evaluator cleared all node states when `frameCount == 0` (each new cycle). The tick-based evaluator never clears states; they self-correct via composite resets and tick result propagation.

3. **No iteration guard** — the old `while` loop had a `maxIterations = 10000` guard against infinite loops. The tick-based approach uses bounded recursion (tree depth determines max call stack depth), making the guard unnecessary.

4. **No `UnwindSpecialComposites()`** — Priority previously required a pre-pass to unwind the frame stack and reset. Now `TickPriority` simply sets `activeChildIndex[nodeIndex] = 0` before delegating to `TickSelector`.

---

## 3. Coding Style Rules (from `.trae/rules/project_rules.md`)

- No single-letter variable names (except `i`, `j`, `k` in simple loops; `T` in generics)
- No `var` when concrete type is known
- No `idx` abbreviations — always `*Index`
- No underscore prefix for variable names (`private int bbAccess` not `_bbAccess`)
