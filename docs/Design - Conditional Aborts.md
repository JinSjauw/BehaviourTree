# Conditional Aborts — Behaviour Designer Style

## 1. How Behaviour Designer does it

Conditional aborts are configured **on the composite node**, not on individual children. From the [official docs](https://opsive.com/support/documentation/behavior-designer/conditional-aborts/): *"Conditional aborts can be accessed from any Composite task."* The composite tracks its children's conditions across ticks and reacts to changes.

**None** — Default. Children evaluate sequentially, no background re-evaluation.

**Self** — The composite re-evaluates its own conditional children every tick while actions *within the same branch* are RUNNING. If a condition transitions `SUCCESS → FAILURE`, the currently running action inside this composite is aborted.

```
Sequence (AbortType = Self)
├── HasTarget? (Condition)       ← re-checked every tick while MoveTo is RUNNING
└── MoveTo (Action, wait 10s)    ← aborted if HasTarget becomes false
```

**Lower Priority** — The composite's **parent** re-evaluates this composite's conditions at the start of each tick. If a condition transitions `FAILURE → SUCCESS` while a lower-priority sibling (to the right) is RUNNING under the same parent, the parent aborts the sibling and restarts evaluation from this composite.

```
Selector (Parent)
├── Sequence (AbortType = LowerPriority)   ← Parent checks conditions inside this
│   ├── HasAmmo? (Condition)
│   └── Shoot (Action)
├── Patrol (Action, runs many ticks)       ← aborted if HasAmmo? becomes true
└── Idle (Action)                          ← or this
```

**Both** — Self + Lower Priority combined.

## 2. What this codebase already has

The building blocks are all present:

- [TickComposite](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Runtime/CompositeTick.cs#L12-L26) delegates to `CompositeMethod.Execute()`, which iterates children.
- [TickDispatcher.TickNode](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Runtime/TickContext.cs#L57-L73) dispatches by node type, stores result in `nodeStates[nodeIndex]`.
- `nodeStates[]` tracks INACTIVE / RUNNING / SUCCESS / FAILURE per node.
- `activeChildIndex[]` tracks which child a composite is currently on.
- Composite children can be `DecoratorNode` (has `methodName`, evaluates a condition method).

What is missing:

- No `AbortType` enum or field.
- No condition re-evaluation at composite evaluation start.
- No `lastConditionResult[]` to detect condition transitions.
- No subtree abort helper.

## 3. Design

### 3.1 Data model

```csharp
// ─── Core/AbortType.cs ───
public enum AbortType : byte
{
    None = 0,
    Self = 1,
    LowerPriority = 2,
    Both = 3,
}
```

On `CompositeNode` (editor asset):

```csharp
public class CompositeNode : BehaviourNode
{
    // ... existing fields (compositeType, methodName, fieldEntries) ...
    [HideInInspector] public AbortType abortType = AbortType.None;   // NEW
}
```

On `NodeData` (runtime struct, baked from CompositeNode):

```csharp
public struct NodeData
{
    // ... existing fields ...
    public AbortType abortType;           // NEW — only meaningful for COMPOSITE nodes
}
```

On `TickContext`:

```csharp
public struct TickContext
{
    // ... existing fields ...
    public bool[] lastConditionResult;  // NEW — per-child-node, true = condition was met last tick
}
```

### 3.2 Condition evaluation helpers

Two helpers: one for leaf conditions (decorator/condition nodes with methods), one for finding the first condition inside a child composite subtree.

```csharp
/// <summary>Evaluates a leaf node's method as a standalone condition.</summary>
internal static bool EvaluateLeafCondition(int nodeIndex, ref TickContext ctx)
{
    NodeMethod method = ctx.methodInstances[nodeIndex];
    if (method == null) return true;

    method.ResolveInputsGeneric(ctx.blackBoard);
    NodeState result = method.Execute(0, ref ctx);
    method.WriteOutputsGeneric(ctx.blackBoard);
    return result == NodeState.SUCCESS;
}

/// <summary>
/// Finds and evaluates the first method-bearing condition node inside a child
/// composite subtree. Used for LowerPriority abort where the parent needs to check
/// a child composite's condition.
///
/// Recursion rule: only recurses into nested child composites that share the same
/// abort type (LowerPriority or Both). A nested composite with AbortType.None or Self
/// is NOT descended into. This matches Behaviour Designer's behavior and prevents
/// infinite recursion into unrelated subtrees.
///
/// Returns true if no qualifying condition was found (treated as "always met").
/// </summary>
internal static bool EvaluateCompositeCondition(int compositeIndex, AbortType requiredType, ref TickContext ctx)
{
    ref NodeData composite = ref ctx.nodeDatas[compositeIndex];
    return FindFirstCondition(composite.firstChildIndex, composite.lastChildIndex, requiredType, ref ctx);
}

private static bool FindFirstCondition(int first, int last, AbortType requiredType, ref TickContext ctx)
{
    if (first < 0) return true;

    // Pass 1: check direct children for method-bearing nodes
    for (int i = first; i <= last; i++)
    {
        if (ctx.methodInstances[i] != null)
            return EvaluateLeafCondition(i, ref ctx);
    }

    // Pass 2: no direct condition found — recurse into nested composites
    // that share the same abort type (LowerPriority or Both)
    for (int i = first; i <= last; i++)
    {
        ref NodeData child = ref ctx.nodeDatas[i];
        if (child.firstChildIndex < 0) continue;
        if (child.nodeType != BehaviourNodeType.COMPOSITE) continue;

        bool hasRequired = child.abortType == requiredType ||
                           child.abortType == AbortType.Both;
        if (!hasRequired) continue;

        bool found = FindFirstCondition(child.firstChildIndex, child.lastChildIndex, requiredType, ref ctx);
        if (!found) return false; // condition found and FAILED
    }

    return true; // no qualifying method-bearing children found
}
```

### 3.3 Conditional abort check — called at composite evaluation start

Two separate passes. Self abort checks this composite's own children. Lower Priority checks this composite's children that are themselves composites with the abort type.

```csharp
/// <summary>
/// Re-evaluates child conditions for conditional abort.
/// Called at the start of each composite's Execute().
/// Returns the child index to resume from (may change if abort occurred).
/// </summary>
internal static int CheckConditionalAbort(int compositeIndex, ref TickContext ctx)
{
    ref NodeData composite = ref ctx.nodeDatas[compositeIndex];
    int first = composite.firstChildIndex;
    int last = composite.lastChildIndex;
    if (first < 0) return ctx.activeChildIndex[compositeIndex];

    AbortType parentAbortType = composite.abortType;
    int childCount = last - first + 1;

    // Find the currently RUNNING child of THIS composite
    int runningChildLocal = -1;
    for (int c = 0; c < childCount; c++)
    {
        if (ctx.nodeStates[first + c] == NodeState.RUNNING)
        {
            runningChildLocal = c;
            break;
        }
    }

    // ── SELF abort pass ──
    // This composite re-evaluates its own children's conditions.
    // If a child condition transitions true→false while an action to the right is RUNNING,
    // abort the running action.
    if (parentAbortType == AbortType.Self || parentAbortType == AbortType.Both)
    {
        for (int c = 0; c < childCount; c++)
        {
            int childIdx = first + c;
            // Self only checks leaves with methods (decorators, conditions)
            if (ctx.methodInstances[childIdx] == null) continue;

            bool conditionMet = EvaluateLeafCondition(childIdx, ref ctx);
            bool wasMet = ctx.lastConditionResult[childIdx];
            ctx.lastConditionResult[childIdx] = conditionMet;

            if (wasMet && !conditionMet && runningChildLocal >= 0 && runningChildLocal > c)
            {
                AbortSubtree(first + runningChildLocal, ref ctx);
                return c; // re-evaluate from the failed condition
            }
        }
    }

    // ── LOWER PRIORITY abort pass ──
    // This composite checks children that are composites with LowerPriority/Both abort type.
    // If that child composite's condition transitions false→true, and a sibling to the right
    // is RUNNING, abort the sibling and restart from this child composite.
    for (int c = 0; c < childCount; c++)
    {
        int childIdx = first + c;
        ref NodeData childNode = ref ctx.nodeDatas[childIdx];

        // Only composites can have abort types
        if (childNode.nodeType != BehaviourNodeType.COMPOSITE) continue;
        AbortType childAbort = childNode.abortType;
        if (childAbort != AbortType.LowerPriority && childAbort != AbortType.Both) continue;

        // Evaluate this child composite's condition — pass its own abort type
        // for the recursion constraint (only recurse into nested composites
        // that share LowerPriority or Both)
        bool conditionMet = EvaluateCompositeCondition(childIdx, childAbort, ref ctx);
        bool wasMet = ctx.lastConditionResult[childIdx];
        ctx.lastConditionResult[childIdx] = conditionMet;

        // Condition transitioned false→true, and a sibling to the right is RUNNING
        if (!wasMet && conditionMet && runningChildLocal >= 0 && runningChildLocal > c)
        {
            // Abort the running sibling (not a child of the aborted composite — a sibling of it)
            AbortSubtree(first + runningChildLocal, ref ctx);
            ctx.activeChildIndex[compositeIndex] = 0;
            return c; // restart from this newly-active child composite
        }
    }

    return ctx.activeChildIndex[compositeIndex];
}
```

The critical difference from the previous version: **LowerPriority scans for child COMPOSITES with the abort type**, not child leaves with methods. It dives into the child composite via `EvaluateCompositeCondition` to find its first condition, then checks for sibling transitions. This matches the Behaviour Designer model where the abort type on a composite signals its parent to handle it.

### 3.3b Recursion constraints and validation

The `FindFirstCondition` recursion follows two rules (matching Behaviour Designer and UE5 Observer Aborts):

1. **Direct children first** — Method-bearing leaves (decorators, conditions) are checked before recursing.
2. **Only recurse into composites sharing the same abort kind** — A nested composite is descended into only if its `abortType` is compatible with the `requiredType`. The check: `child.abortType == requiredType || child.abortType == Both`.

The `requiredType` is the abort type of the composite currently being evaluated (passed from `CheckConditionalAbort`). This means:

| Parent calls | Child's abortType | Recurses into child? | requiredType inside child |
|---|---|---|---|
| LowerPriority pass | LowerPriority | Yes | LowerPriority |
| LowerPriority pass | Both | Yes | Both |
| LowerPriority pass | None | **No** | — |
| LowerPriority pass | Self | **No** | — |
| Self pass | (not composites) | Self only checks direct leaves | Self (direct only, no recursion) |

For nested grandchild composites inside the child, the same logic applies:

| Inside child with... | Grandchild's abortType | Recurses? |
|---|---|---|
| requiredType=LowerPriority | LowerPriority | Yes |
| requiredType=LowerPriority | Both | Yes (Both ⊇ LowerPriority) |
| requiredType=LowerPriority | None | No |
| requiredType=LowerPriority | Self | **No** — Self only aborts within its own branch, not relevant to parent-level abort |
| requiredType=Both | LowerPriority | Yes |
| requiredType=Both | Both | Yes |
| requiredType=Both | None | No |
| requiredType=Both | Self | **No** — Self doesn't participate in cross-level abort

This prevents the abort check from crawling into unrelated subtrees. For example:

```
Selector (Parent)
├── Sequence "Combat" (AbortType = LowerPriority)
│   ├── Sequence "SubBranch" (AbortType = None)        ← NOT recursed into
│   │   ├── HasAmmo? (Condition)
│   │   └── Shoot
│   └── Wait
└── Idle
```

The "SubBranch" Sequence has `AbortType.None`, so `EvaluateCompositeCondition` skips it. No condition is found in "Combat" → the abort has no effect. The editor should flag this.

#### Editor validation

During bake, validate that composites with `AbortType != None` have at least one reachable condition:

```csharp
internal static bool HasValidConditionForAbort(int compositeIndex, AbortType abortType,
    NodeData[] nodeDatas, NodeMethod[] methodInstances)
{
    if (abortType == AbortType.None) return true;
    ref NodeData composite = ref nodeDatas[compositeIndex];
    if (composite.firstChildIndex < 0) return false;

    bool found = false;
    HasConditionRecursive(composite.firstChildIndex, composite.lastChildIndex,
        abortType, nodeDatas, methodInstances, ref found);
    return found;
}

private static void HasConditionRecursive(int first, int last, AbortType requiredType,
    NodeData[] nodeDatas, NodeMethod[] methodInstances, ref bool found)
{
    if (first < 0 || found) return;

    // Direct children with methods
    for (int i = first; i <= last; i++)
        if (methodInstances[i] != null) { found = true; return; }

    // Recurse into nested composites sharing the same abort type
    for (int i = first; i <= last; i++)
    {
        ref NodeData child = ref nodeDatas[i];
        if (child.firstChildIndex < 0) continue;
        if (child.nodeType != BehaviourNodeType.COMPOSITE) continue;

        bool hasRequired = child.abortType == requiredType ||
                           child.abortType == AbortType.Both;
        if (!hasRequired) continue;

        HasConditionRecursive(child.firstChildIndex, child.lastChildIndex,
            requiredType, nodeDatas, methodInstances, ref found);
    }
}
```

The editor shows a warning icon on composites where `AbortType != None && !HasValidConditionForAbort(...)`. The tree still evaluates — the abort pass returns immediately with no conditions found — but the user gets clear feedback.

### 3.4 Subtree abort helper

```csharp
internal static void AbortSubtree(int nodeIndex, ref TickContext ctx)
{
    if (nodeIndex < 0 || nodeIndex >= ctx.nodeDatas.Length) return;

    ref NodeData node = ref ctx.nodeDatas[nodeIndex];

    // Reset this node
    ctx.nodeStates[nodeIndex] = NodeState.INACTIVE;
    ctx.activeChildIndex[nodeIndex] = 0;

    // Recursively reset children
    for (int i = node.firstChildIndex; i >= 0 && i <= node.lastChildIndex; i++)
    {
        AbortSubtree(i, ref ctx);
    }
}
```

### 3.5 Composite methods — wiring it in

Each composite calls `CheckConditionalAbort` at the start of `Execute()`:

```csharp
// SequenceMethod.Execute:
public override NodeState Execute(int nodeIndex, ref TickContext ctx)
{
    ref NodeData node = ref ctx.nodeDatas[nodeIndex];
    if (node.firstChildIndex < 0) return NodeState.SUCCESS;

    int child = ctx.activeChildIndex[nodeIndex];

    // ── Conditional abort check ──
    int abortResult = TickFunctions.CheckConditionalAbort(nodeIndex, ref ctx);
    if (abortResult != ctx.activeChildIndex[nodeIndex])
    {
        // Abort was triggered — reset and start from the abort position
        child = abortResult;
    }

    int childCount = node.lastChildIndex - node.firstChildIndex + 1;
    // ... rest unchanged ...
}
```

Same call added to `SelectorMethod`, `PriorityMethod`, and `ParallelMethod`.

### 3.6 TreeEvaluator changes

```csharp
public TreeEvaluator(...)
{
    // ... existing init ...
    lastConditionResult = new bool[nodeDatas.Length];  // defaults to false — safe starting state
    ctx.lastConditionResult = lastConditionResult;
}
```

### 3.7 TreeBaker changes

```csharp
// In ConvertNode / FillNodeData:
nodeData.abortType = node is CompositeNode cn ? cn.abortType : AbortType.None;
```

### 3.8 Editor changes

Add an `AbortType` dropdown to the composite node inspector. When the user selects a composite in the graph:

```csharp
EditorGUILayout.PropertyField(serializedObject.FindProperty("abortType"), 
    new GUIContent("Abort Type"));
```

Mark the tree dirty and trigger re-bake on change.

## 4. How it flows at runtime

```
TreeEvaluator.Evaluate()
  └── TickDispatcher.TickNode(0)
        └── TickComposite(compositeIndex)         ← e.g. the top-level Selector
              └── CheckConditionalAbort(compositeIndex)
                    ├── SELF pass (if this composite's own abortType is Self/Both):
                    │     For each child that is a decorator/condition leaf:
                    │       EvaluateLeafCondition() → compare to lastConditionResult
                    │       If true→false transition & action to the right RUNNING
                    │         → AbortSubtree(running action child)
                    │
                    └── LOWER PRIORITY pass (for ALL child composites):
                          For each child that is a composite with abortType LowerPriority/Both:
                            EvaluateCompositeCondition(child) → dives in, finds first condition
                            If false→true transition & sibling to the right RUNNING
                              → AbortSubtree(running sibling)
                              → Restart parent from this child composite index

              └── Continue normal composite evaluation from (possibly updated) child index
```

Concrete example:

```
Selector (Parent composite)
├── Sequence "Ammo" (child composite, AbortType = LowerPriority)
│   ├── HasAmmo? (Condition)     → condition leaf, checked by EvaluateCompositeCondition
│   └── Shoot (Action)
├── Sequence "Patrol" (child composite)            → RUNNING
│   └── ...
└── Idle (Action)
```

1. Parent Selector's `Execute()` calls `CheckConditionalAbort`.
2. LowerPriority pass finds child "Ammo Sequence" (index 0) has `AbortType = LowerPriority`.
3. `EvaluateCompositeCondition` dives into Ammo Sequence → finds `HasAmmo?` → evaluates it → returns true (condition met).
4. `lastConditionResult[0]` was false (no ammo last tick), now true → transition detected.
5. "Patrol" (index 1) is RUNNING → it's a lower-priority sibling → `AbortSubtree(Patrol)`.
6. Parent restarts from index 0 (Ammo Sequence). Shoot runs.

## 5. Edge cases

### 5.1 OnAbort callback — architecture

When a branch is aborted, nodes that were modifying blackboard values (counters, cooldowns, position offsets) need to clean up. Without an abort callback, a `SetVariable` node that wrote `"targetPos" = flankOffset` leaves the value dangling after the flank branch is aborted.

Three architectures:

#### Option A: Virtual on `NodeMethod` base class (recommended)

```csharp
// In NodeMethod.cs base class:
public virtual void OnAbort(int nodeIndex, ref TickContext ctx) { }
```

`AbortSubtree` calls `method.OnAbort()` on every method-bearing node before resetting its state:

```csharp
internal static void AbortSubtree(int nodeIndex, ref TickContext ctx)
{
    if (nodeIndex < 0 || nodeIndex >= ctx.nodeDatas.Length) return;

    // Call OnAbort BEFORE resetting state (method can read current state to clean up)
    NodeMethod method = ctx.methodInstances[nodeIndex];
    method?.OnAbort(nodeIndex, ref ctx);

    ctx.nodeStates[nodeIndex] = NodeState.INACTIVE;
    ctx.activeChildIndex[nodeIndex] = 0;

    ref NodeData node = ref ctx.nodeDatas[nodeIndex];
    for (int i = node.firstChildIndex; i >= 0 && i <= node.lastChildIndex; i++)
        AbortSubtree(i, ref ctx);
}
```

Example override in a method that writes to the blackboard:

```csharp
public class SetTargetPosition : ActionMethod
{
    public override NodeState Execute(int nodeIndex, ref TickContext ctx)
    {
        ctx.blackBoard.SetVector3("targetPos", flankCalculatedPos);
        return NodeState.RUNNING;
    }

    public override void OnAbort(int nodeIndex, ref TickContext ctx)
    {
        ctx.blackBoard.SetVector3("targetPos", Vector3.zero); // Reset
    }
}
```

| Pro | Con |
|---|---|
| Simplest, one virtual dispatch per aborted node | Every NodeMethod has the vtable slot (0 cost unless overridden) |
| No extra interface, no reflection | Must be careful not to use instance state (NodeMethod instances are shared across agents — only use `ctx` and `ctx.blackBoard`) |
| Matches UE5 `BTTaskNode::OnTaskAborted` | |

#### Option B: `IAbortHandler` interface

```csharp
public interface IAbortHandler
{
    void OnAbort(int nodeIndex, ref TickContext ctx);
}
```

Only methods that need it implement the interface:

```csharp
public class SetTargetPosition : ActionMethod, IAbortHandler
{
    public void OnAbort(int nodeIndex, ref TickContext ctx)
    {
        ctx.blackBoard.SetVector3("targetPos", Vector3.zero);
    }
}
```

`AbortSubtree` checks with `method is IAbortHandler abortable ? abortable.OnAbort(...) : 0`:

| Pro | Con |
|---|---|
| Opt-in, no overhead for methods that don't need it | `is` check per node (branch predictor makes this negligible) |
| Clear contract in signature | Two interfaces to implement for methods that need it |

#### Option C: `Action<...>` delegate per method

```csharp
// In NodeMethod base:
public delegate void AbortHandler(int nodeIndex, ref TickContext ctx);
public AbortHandler OnAborted;

// In SetTargetPosition constructor or attribute:
public SetTargetPosition()
{
    OnAborted = (int nodeIndex, ref TickContext ctx) =>
    {
        ctx.blackBoard.SetVector3("targetPos", Vector3.zero);
    };
}
```

`AbortSubtree` calls `method.OnAborted?.Invoke(nodeIndex, ref ctx)`:

| Pro | Con |
|---|---|
| Fast dispatch (direct delegate call, no virtual/is check) | Allocates delegates per NodeMethod instance |
| Can wire up dynamically | Not serialization-friendly for editor-configurable cleanup |

#### Recommendation: Option A

The virtual method is the standard pattern (UE5, Unreal Behavior Trees, Behaviour Designer internal patterns). The performance cost of calling `OnAbort` during an abort walk is dominated by the recursive `AbortSubtree` state reset — one virtual dispatch per aborted node is noise. Only methods that need it override it; everyone else pays zero cost thanks to JIT devirtualization at runtime.

The critical rule: **never use instance fields in `OnAbort`**. All node methods share one `NodeMethod` instance per tree across all agents. Only use `ctx` and `ctx.blackBoard`.

---

### 5.2 Other edge cases

## 6. Files to create / modify

| File | Change |
|---|---|
| `Core/AbortType.cs` | **New** — enum |
| `Core/NodeData.cs` | Add `AbortType abortType` field |
| `Core/Nodes/CompositeNode.cs` | Add `AbortType abortType` field |
| `Runtime/TickContext.cs` | Add `bool[] lastConditionResult` |
| `Runtime/ConditionalAbort.cs` | **New** — `CheckConditionalAbort`, `AbortSubtree`, `EvaluateChildCondition` |
| `Runtime/TreeEvaluator.cs` | Allocate `lastConditionResult[]`, wire into `TickContext` |
| `Runtime/TreeBaker.cs` | Copy `abortType` from `CompositeNode` into `NodeData` |
| `Runtime/Methods/SequenceMethod.cs` | Call `CheckConditionalAbort` at start |
| `Runtime/Methods/SelectorMethod.cs` | Call `CheckConditionalAbort` at start |
| `Runtime/Methods/PriorityMethod.cs` | Call `CheckConditionalAbort` at start |
| `Runtime/Methods/ParallelMethod.cs` | Call `CheckConditionalAbort` at start |
| `Editor/*.cs` | Composite inspector shows `AbortType` dropdown |
