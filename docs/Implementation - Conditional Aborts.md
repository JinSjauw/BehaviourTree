# Conditional Aborts — Implementation Plan

Based on [Design - Conditional Aborts](Design%20-%20Conditional%20Aborts.md).

---

## Phase 1: Data Model (Core assembly)

### 1.1 New file: `Core/AbortType.cs`

```csharp
namespace BehaviourTree
{
    public enum AbortType : byte
    {
        None = 0,
        Self = 1,
        LowerPriority = 2,
        Both = 3,
    }
}
```

### 1.2 Modify: `Core/NodeState.cs`

Add `INACTIVE = 4`.

### 1.3 Modify: `Core/NodeData.cs`

Add field:
```csharp
public AbortType abortType;
```
after `firstChildIndex`/`lastChildIndex` (or at end, before closing brace).

### 1.4 Modify: `Core/Nodes/CompositeNode.cs`

Add field:
```csharp
[HideInInspector] public AbortType abortType = AbortType.None;
```

### 1.5 Modify: `Core/NodeMethod.cs`

Add virtual method on `NodeMethod`:
```csharp
/// <summary>
/// Called by AbortSubtree before resetting a node's state.
/// Override to clean up blackboard values or other shared state.
/// IMPORTANT: Only use ctx and ctx.blackBoard — NodeMethod instances
/// are shared across agents, so instance fields are not safe here.
/// </summary>
public virtual void OnAbort(int nodeIndex, ref TickContext ctx) { }
```

Note: `TickContext` is defined in the Runtime assembly. Since Core can't reference Runtime,
we either:
- **Option A**: Change `OnAbort` to not take `TickContext` (use `IBlackBoardAccess` instead)
- **Option B**: Move `OnAbort` to `CompositeMethod` in Runtime, and have `AbortSubtree` call it only via interface check

**Decision**: Option A — `OnAbort(IBlackBoardAccess bb)` makes more sense. The method only needs
the BB for cleanup. `nodeIndex` is available via `TickContext` but the methods can be designed
without it.

```csharp
public virtual void OnAbort(IBlackBoardAccess bbAccess) { }
```

---

## Phase 2: Runtime Infrastructure

### 2.1 Modify: `Runtime/TickContext.cs`

Add to `TickContext`:
```csharp
/// <summary>Per-node last condition result for abort transition detection.</summary>
public bool[] lastConditionResult;
```

### 2.2 Modify: `Runtime/TreeEvaluator.cs`

In constructor, after allocating `activeChildIndex`:
```csharp
bool[] lastConditionResult = new bool[nodeDatas.Length];
```

In `Evaluate()`, wire into tickContext:
```csharp
tickContext.lastConditionResult = lastConditionResult;
```

Store as a private field alongside the other arrays:
```csharp
private bool[] lastConditionResult;
```

### 2.3 New file: `Runtime/ConditionalAbort.cs`

Partial of `TickFunctions` containing:

- `CheckConditionalAbort(int compositeIndex, ref TickContext ctx) → int`
- `AbortSubtree(int nodeIndex, ref TickContext ctx) → void`
- `EvaluateLeafCondition(int nodeIndex, ref TickContext ctx) → bool`
- `EvaluateCompositeCondition(int compositeIndex, AbortType requiredType, ref TickContext ctx) → bool`
- `FindFirstCondition(int first, int last, AbortType requiredType, ref TickContext ctx) → bool`

### 2.4 Modify: `Runtime/TreeBaker.cs`

In `FillNodeData`, COMPOSITE branch (line ~388-403), add after `nodeData.fieldDataCount = ...`:
```csharp
nodeData.abortType = composite.abortType;
```

### 2.5 Modify: Composite methods

Wire `CheckConditionalAbort` at the top of each `Execute()`:

**SequenceMethod** — insert after `child` assignment:
```csharp
int abortResult = TickFunctions.CheckConditionalAbort(nodeIndex, ref ctx);
if (abortResult != ctx.activeChildIndex[nodeIndex])
{
    child = abortResult;
}
```

**SelectorMethod** — same pattern.

**PriorityMethod** — insert before `ctx.activeChildIndex[nodeIndex] = 0`:
```csharp
TickFunctions.CheckConditionalAbort(nodeIndex, ref ctx);
```
(Priority already resets the index; CheckConditionalAbort may override it.)

**ParallelMethod** — insert before `children == null` check:
```csharp
int abortResult = TickFunctions.CheckConditionalAbort(nodeIndex, ref ctx);
if (abortResult != ctx.activeChildIndex[nodeIndex])
{
    children = null; // Reset per-child state on abort
}
```

---

## Phase 3: Editor

### 3.1 Modify: `Editor/NodeInspectorViewEditors/CustomNodeEditor.cs`

**Add field:**
```csharp
private SerializedProperty abortTypeProp;
```

**In OnEnable:**
```csharp
if (target is CompositeNode)
    abortTypeProp = serializedObject.FindProperty("abortType");
```

**After BuildFieldEntries in OnInspectorGUI:**
```csharp
if (abortTypeProp != null)
{
    EditorGUILayout.Space();
    EditorGUILayout.LabelField("Conditional Abort", EditorStyles.boldLabel);
    EditorGUILayout.PropertyField(abortTypeProp, new GUIContent("Abort Type"));

    AbortType currentAbort = (AbortType)abortTypeProp.enumValueIndex;
    if (currentAbort != AbortType.None)
    {
        CompositeNode composite = (CompositeNode)target;
        if (!HasValidConditionForAbort(composite, currentAbort))
        {
            EditorGUILayout.HelpBox(
                "No reachable Condition node found. Add a Condition node as a " +
                "descendant for this abort type to take effect.",
                MessageType.Warning);
        }
    }
}
```

**Add `using` at top:**
```csharp
using BehaviourTree;
```

**Add helper:**
```csharp
private static bool HasValidConditionForAbort(CompositeNode composite, AbortType abortType)
{
    bool found = false;
    HasConditionRecursiveEditor(composite.children, abortType, ref found);
    return found;
}

private static void HasConditionRecursiveEditor(List<BehaviourNode> children,
    AbortType requiredType, ref bool found)
{
    if (children == null || found) return;

    for (int i = 0; i < children.Count; i++)
    {
        if (children[i] is LeafNode leaf &&
            leaf.NodeType == BehaviourNodeType.CONDITION &&
            !string.IsNullOrEmpty(leaf.methodName))
        {
            found = true;
            return;
        }
    }

    for (int i = 0; i < children.Count; i++)
    {
        if (children[i] is CompositeNode childComposite)
        {
            AbortType childAbort = childComposite.abortType;
            bool compatible = childAbort == requiredType || childAbort == AbortType.Both;
            if (compatible)
                HasConditionRecursiveEditor(childComposite.children, requiredType, ref found);
        }
    }
}
```

---

## Phase 4: Verification

- [ ] Read all modified files to verify correctness
- [ ] Check that `BehaviourTree.Core.asmdef` includes the new `AbortType.cs` namespace
- [ ] Check for compilation errors
- [ ] Run smoke test: open Unity, create a composite with abort type, verify dropdown + warning

---

## Files Summary

| File | Phase | Action |
|---|---|---|
| `Core/AbortType.cs` | 1 | Create |
| `Core/NodeState.cs` | 1 | Edit: add INACTIVE |
| `Core/NodeData.cs` | 1 | Edit: add abortType |
| `Core/Nodes/CompositeNode.cs` | 1 | Edit: add abortType |
| `Core/NodeMethod.cs` | 1 | Edit: add OnAbort |
| `Runtime/TickContext.cs` | 2 | Edit: add lastConditionResult |
| `Runtime/TreeEvaluator.cs` | 2 | Edit: allocate + wire |
| `Runtime/ConditionalAbort.cs` | 2 | Create |
| `Runtime/TreeBaker.cs` | 2 | Edit: copy abortType |
| `Runtime/Methods/SequenceMethod.cs` | 2 | Edit: wire AbortCheck |
| `Runtime/Methods/SelectorMethod.cs` | 2 | Edit: wire AbortCheck |
| `Runtime/Methods/PriorityMethod.cs` | 2 | Edit: wire AbortCheck |
| `Runtime/Methods/ParallelMethod.cs` | 2 | Edit: wire AbortCheck |
| `Editor/NodeInspectorViewEditors/CustomNodeEditor.cs` | 3 | Edit: dropdown + warning |
