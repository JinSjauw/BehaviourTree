# Conditional Aborts — How It Works

## Problem

Without conditional aborts, a composite node runs children left-to-right and **never re-evaluates earlier conditions** while a later child is running. For example:

```
Sequence
├── EnemyNear? (Condition)  ← was true, enabled Chase
└── Chase (Action, 10s)     ← running… but enemy ran away after 2s
```

Without abort, Chase runs to completion even though the condition that enabled it is no longer true. Conditional aborts solve this by **re-evaluating conditions every tick** while an action runs, and aborting the action when conditions change.

## Two Abort Types

### Self Abort

A composite re-evaluates its **own direct children's** conditions every tick. Applies to the composite itself.

| Composite | Trigger | What happens |
|---|---|---|
| **Sequence** | Condition was true → now false | The condition that enabled the running action broke. Abort the running action, restart from the broken condition. |
| **Selector** | Condition was false → now true | A higher-priority condition just became available. Abort the running action, restart from this condition. |
| **Priority** | Same as Selector | Abort running child, restart from newly-met condition. |

```
Sequence (AbortType = Self)           Selector (AbortType = Self)
├── EnemyNear?  ← re-checked          ├── EnemyNear?  ← re-checked
└── Chase (RUNNING) ← aborted         ├── Patrol (RUNNING) ← aborted on EnemyNear true
                                       └── Idle
```

### Lower Priority Abort

A **parent** composite checks its child composites for conditions. When a child composite's condition transitions **false→true**, the parent aborts the currently-running lower-priority sibling. The parent itself doesn't need an abort type — the LP pass always runs.

```
Selector (parent, AbortType = None)
├── Sequence "Combat" (AbortType = LowerPriority)
│       ├── HasAmmo? (Condition)   ← was false, now true
│       └── Shoot
├── Patrol (RUNNING)               ← ABORTED — Combat has higher priority
└── Idle
```

**Key constraint**: LP only triggers on false→true. If the condition goes true→false, the LP branch's own Self abort handles the internal action — there's no lower-priority sibling to abort (the LP branch itself was running).

### Both

A composite does both Self and LowerPriority checks. `AbortType.Both` is equivalent to `Self | LowerPriority`.

## Execution Order

`CheckConditionalAbort` runs at the **start of every composite's Execute()**, before any children are ticked. Two passes:

1. **Self pass** (if `abortType == Self || Both`): checks direct method-bearing children for any status transition
2. **LP pass** (always): checks child composites with `abortType == LowerPriority || Both` for false→true condition transitions

**Self short-circuits LP**. If Self fires, it returns immediately — the LP pass doesn't run that tick. This is safe because both passes would target the same running child, and Self already aborted it. If LP's condition remains met, it catches up next tick; if it was transient, no abort was needed.

## How Conditions Are Found

### For Self: iterate direct children

Self only checks immediate children of the composite. It iterates `child 0..N`, and for each child that has a `methodInstances[i] != null`, calls `EvaluateLeafCondition`. Each child's condition is evaluated independently — all are checked, left-to-right.

### For LP: recursive descent via `FindFirstCondition`

When a parent finds a child composite with compatible abort type (`LowerPriority` or `Both`), it calls `EvaluateCompositeCondition` → `FindFirstCondition`. This recursively searches inside the child composite:

```
FindFirstCondition(children):
  for each child:
    if non-composite with method  → EvaluateLeafCondition  → return result
    if composite with right abort → recurse FindFirstCondition
  return true (no condition found = "always met")
```

**Recursion gate**: only descends into composites whose abort type matches the required one (`LowerPriority == LowerPriority`, `Both == LowerPriority`, `Both == Both`). Composites with `None` or `Self` are skipped — Self conditions are only relevant within their own branch.

**First found wins**: left-to-right, the first qualifying condition decides. If a composite has both a direct condition and a nested LP composite, the direct condition shadows the nested one.

### `EvaluateLeafCondition`

Evaluates a single node's method as a condition for abort purposes:

- **`ConditionMethod`**: calls `Execute()` → SUCCESS/FAILURE
- **`ActionMethod`**: returns `true` (don't execute — side effects)
- **`DecoratorMethod`**: returns `true` (not supported yet — would need child evaluation without polluting `nodeStates`)
- **`null`**: returns `true` (no condition = "always met")

Only `ConditionMethod` nodes are treated as condition-bearing. Actions and decorators are ignored to avoid unintended side effects during abort re-evaluation.

## State Tracking

### `lastConditionResult[]`

Per-node `bool[]` array on `TickContext`, indexed the same as `nodeDatas`. Tracks the **last evaluated condition result** for each node. Used by both Self and LP passes:

| Pass | Writes to | Reads from | Transition direction |
|---|---|---|---|
| Self | `lastConditionResult[directChild]` | Same index | Any (`wasMet != conditionMet`) |
| LP | `lastConditionResult[childComposite]` | Same index | `!wasMet && conditionMet` only |

Self and LP write to **different indices** (a Self condition at index 3 within its parent vs. the LP child composite at index 1 as seen by the grandparent), so they don't interfere.

**Default is `false`** (C# default for `new bool[]`). On the first tick, all conditions appear as "was false." Since the `runningChildLocal >= 0` guard prevents aborts when no child is running, there are no false positives.

### `runningChildLocal`

At the start of `CheckConditionalAbort`, we find the **first RUNNING child** of the calling composite. This is the local child index (0-based within the composite's children). The abort checks use `runningChildLocal > c` to ensure:
- The abort only fires when there's a sibling to the right of the condition (`>` not `>=`)
- The condition composite itself (if it's the running one) won't abort itself

## `AbortSubtree`

Recursive state reset when an abort fires:

```
AbortSubtree(nodeIndex):
  1. method.OnAbort(blackBoard)   ← cleanup hook (virtual, default no-op)
  2. nodeStates[nodeIndex] = INACTIVE
  3. activeChildIndex[nodeIndex] = 0
  4. for each child: AbortSubtree(childIndex)
```

`INACTIVE` (4) is distinct from `NONE` (0, the default/uninitialized state) so aborted nodes can be distinguished during debugging.

## Composite Integration

| Composite | Wired how |
|---|---|
| **Sequence** | `CheckConditionalAbort` → if result differs from `activeChildIndex`, update `child` local. Loop restarts from abort position. |
| **Selector** | Same pattern as Sequence. |
| **Priority** | Calls `CheckConditionalAbort` before resetting `activeChildIndex` to 0. Doesn't use the return value (always restarts from 0). |
| **Parallel** | If abort result differs, resets `children[]` array to `null` (clears per-child state). |

**Important**: `CheckConditionalAbort` returns the child index to resume from but does **not** modify `activeChildIndex` — the caller is responsible for that. This prevents a subtle bug where internal mutation made the caller's comparison a no-op.

## Editor

The `CustomNodeEditor` shows an `Abort Type` dropdown on `CompositeNode`:

- Uses `SerializedProperty` for full Undo support
- Shows a **warning** when `AbortType != None` but no reachable `Condition` node exists:
  - Scans direct children for `LeafNode` with `CONDITION` type + non-empty `methodName`
  - Recurses into child composites with compatible abort type (`LowerPriority` or `Both`)
  - Skips composites with `None` or `Self` (mirrors runtime recursion gate)

## Data Flow Summary

```
Editor                   Bake                     Runtime
────────────────────────────────────────────────────────────
CompositeNode    →       NodeData          →       TickContext
  .abortType               .abortType               .nodeDatas[i].abortType
                                                     .lastConditionResult[]
                                                     .activeChildIndex[]
                                                     .nodeStates[]
                              
CompositeMethod.Execute(nodeIndex, ref ctx):
  1. CheckConditionalAbort(compositeIndex, ref ctx)
     ├─ Self pass: evaluate direct conditions → abort on transition
     └─  LP pass: evaluate child composites  → abort sibling on false→true
  2. Normal child tick loop (may restart from abort position)
```

## Files

| File | Role |
|---|---|
| `Core/AbortType.cs` | Enum: None, Self, LowerPriority, Both |
| `Core/NodeState.cs` | +INACTIVE (4) for aborted nodes |
| `Core/NodeData.cs` | +abortType field |
| `Core/Nodes/CompositeNode.cs` | +abortType on editor asset |
| `Core/NodeMethod.cs` | +virtual OnAbort(IBlackBoardAccess) |
| `Runtime/TickContext.cs` | +lastConditionResult[] |
| `Runtime/TreeEvaluator.cs` | Allocates lastConditionResult[], wires to TickContext |
| `Runtime/ConditionalAbort.cs` | CheckConditionalAbort, AbortSubtree, EvaluateLeafCondition, FindFirstCondition |
| `Runtime/TreeBaker.cs` | Copies abortType CompositeNode→NodeData; enum baking fix |
| `Runtime/Methods/SequenceMethod.cs` | Wired |
| `Runtime/Methods/SelectorMethod.cs` | Wired |
| `Runtime/Methods/PriorityMethod.cs` | Wired |
| `Runtime/Methods/ParallelMethod.cs` | Wired |
| `Editor/NodeInspectorViewEditors/CustomNodeEditor.cs` | AbortType dropdown + validation warning |
