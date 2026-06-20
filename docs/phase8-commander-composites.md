# Phase 8 — Commander Composites

## Overview

This phase adds "agent-aware" composite nodes to the commander behaviour tree. These composites enable the commander to broadcast orders to groups of agents, issue targeted orders to a single agent, and build higher-level "find best target" macros — all without the individual leaf nodes needing to know about agent iteration logic.

The mechanism is a transient `currentAgentOffset` field on the runtime `BlackBoard`. When set by a composite, every subsequent `GetBoxed`/`SetBoxed` call on the BB transparently adds this offset to the stored slot index. The children of the composite read and write from `baseSlot + agentOffset` as if they were operating on a single agent's data — even though the underlying storage is a flat strided array.

---

## Design Rationale

### Why `agentOffset` on `BlackBoard` and not on `TickContext`?

`TickContext` flows through `CompositeMethod.Execute()` as a `ref` parameter, so composites already have access to it. The problem is that leaf node BB access does not go through `TickContext` — `FieldBinding.ReadFromBBGeneric` calls `bb.GetBoxed(bbSlotIndex)` directly on the `IBlackBoardAccess` reference. Changing `IBlackBoardAccess` to accept an offset parameter would require modifying every caller of `GetBoxed`/`SetBoxed` (baking, bridge, storage, field bindings). Adding the field to `BlackBoard` itself is a single change with a single point of injection — the composite sets it before ticking children, and clears it after.

### Why not a "dynamic variable" dictionary (like Behaviour Designer)?

Behaviour Designer's Dynamic Variables are name-keyed runtime entries created without a definition. This works because BD tasks declare `SharedInt myVar` fields directly and reference them by name. However, our architecture resolves all variable references through `FieldBinding.bbSlotIndex` — a slot index baked at tree-compile time. Adding a name-based resolution path would require:

- A name table in baked data alongside slot indices
- `FieldBinding` differentiation between slot-based and name-based bindings
- `IBlackBoardAccess` additions for name-based access
- Editor changes to support free-form text entry in variable dropdowns

For the few transient variables we need (`_targetAgentID`, `_lowestValue`), the complexity is not justified. We bake these as normal stride=1 variables in the commander's `BlackboardDefinition`. They are visible in the editor, debuggable at runtime, and cost zero additional plumbing.

### Why no parallel ForEachAgent?

A parallel variant would need to track per-agent child completion states, resume individual agents each frame, and handle partial failures — essentially a nested Parallel evaluator. The use case ("all agents pathfind simultaneously, don't advance until all are done") is better served by the commander broadcasting an order flag, while each agent's own tree handles the long-running operation:

```
Commander: ForEachAgent → Action: SetReadyToBreach(true)   ← instant SUCCESS
Agent:     Condition: ReadyToBreach → Action: PathfindToPosition → RUNNING → ...
```

The commander fires the signal; each agent self-manages its own pathfinding. No need for the commander to wait on per-agent RUNNING states.

### Why support RUNNING in sequential ForEachAgent?

A child inside ForEachAgent may return RUNNING (e.g. a `Wait 2s` node for pacing). If the loop restarts from agent 0 every frame, agents at the end of the list would never be reached. The composite must persist which agent it was processing when a child returned RUNNING, and resume at that same agent next frame. This uses the existing `activeChildIndex` mechanism (for which child) extended with `runningAgentIndex` (for which agent within that child's iteration).

---

## Mechanism: `currentAgentOffset`

### What changes

`BlackBoard` gains a single transient field:

```csharp
public class BlackBoard : MonoBehaviour, IBlackBoardAccess
{
    // NEW: transient agent offset, set by commander composites during iteration
    [System.NonSerialized] public int currentAgentOffset = 0;

    // ... existing fields ...
}
```

`GetBoxed` and `SetBoxed` in `ManagedBlackboardStorage` (the actual storage layer behind `BlackBoard`) apply the offset:

```csharp
// ManagedBlackboardStorage — concept (actual implementation may differ)
public object GetBoxed(int index)
{
    int finalSlot = index + owningBlackBoard.currentAgentOffset;
    if (values == null || finalSlot < 0 || finalSlot >= values.Length)
        return null;
    return values[finalSlot];
}

public void SetBoxed(int index, object value)
{
    int finalSlot = index + owningBlackBoard.currentAgentOffset;
    if (values == null || finalSlot < 0 || finalSlot >= values.Length)
        return;
    if (!CanWriteBoxed(finalSlot, value)) return;
    values[finalSlot] = value;
}
```

**Important:** The offset is applied at the storage level, not in `BlackBoard.GetBoxed`/`SetBoxed` (which are wrappers that also sync `serializedReferences`). This means `serializedReferences` sync does **not** use the offset — serialized references exist only for reference-type variables (GameObject, Transform) which are per-component, not squad-data. Commander tree leaves accessing squad-data value types (int, float, bool, Vector3, etc.) go straight to storage.

No changes to `TickContext`, `FieldBinding`, `NodeMethod`, `IBlackBoardAccess`, or any leaf node. Everything under the composite automatically reads/writes the correct agent's slots.

### How the composites set it

Each composite sets `currentAgentOffset` before ticking its children and resets it to 0 after the loop/selection completes. The offset is scoped to the subtree — when the composite returns, it is cleared.

### Edge case: `GetBoxed` checks bounds

If the offset pushes the slot index out of range (e.g. a node reads a non-squad variable with stride 1, and `agentOffset = 5`), the storage returns `null` (or default). This is a user error — wiring a squad-data variable in a non-agent context — and should be caught by validation during bake, not at runtime.

---

## Primitives

### ForEachAgent

Loops through all agents (0 to `agentCount - 1`), setting `currentAgentOffset` and ticking children for each.

```
Behaviour: Sequential
Loop:      for agentIndex in 0..agentCount:
               set currentAgentOffset = agentIndex
               tick children
               if child returns RUNNING → save agentIndex, return RUNNING
               if child returns FAILURE → continue to next agent (or return FAILURE?)
           reset currentAgentOffset = 0
           return SUCCESS
```

**RUNNING resume:** When a child returns `RUNNING`, the composite stores the `activeChildIndex` (as all composites do) **and** the `runningAgentIndex` — the current loop position. Next frame, it resumes at the same child, same agent.

**FAILURE policy:** A child returning FAILURE does not stop the loop (it continues to the next agent). This is the "try each agent" semantics — useful for scanning. If you want "stop on first failure", wrap in a Sequence with a condition.

**Agent count** is read from the commander BB at the start of each tick via a baked BB variable (e.g. `_agentCount` stride=1, maintained by `CommanderTreeRunner`). This ensures the loop respects dynamic registration/unregistration.

**Pseudocode:**

```csharp
[NodeMethod("ForEachAgent")]
public sealed class ForEachAgentMethod : CompositeMethod
{
    // Baked field: slot offset of the _agentCount BB variable
    [SharedVar] public int agentCountSlot;
    
    public override NodeState Execute(int nodeIndex, ref TickContext ctx)
    {
        ref NodeData node = ref ctx.nodeDatas[nodeIndex];
        if (node.firstChildIndex < 0) return NodeState.SUCCESS;

        int agentCount = ctx.blackBoard.GetInt(agentCountSlot);
        int startAgent = ctx.runningAgentIndex?[nodeIndex] ?? 0;
        int child = ctx.activeChildIndex[nodeIndex];

        for (int agentIndex = startAgent; agentIndex < agentCount; agentIndex++)
        {
            ctx.blackBoard.currentAgentOffset = agentIndex;

            // Tick each child in sequence
            int childCount = node.lastChildIndex - node.firstChildIndex + 1;
            while (child < childCount)
            {
                int childIndex = node.firstChildIndex + child;
                NodeState result = TickDispatcher.TickNode(childIndex, ref ctx);

                if (result == NodeState.RUNNING)
                {
                    ctx.activeChildIndex[nodeIndex] = child;
                    ctx.runningAgentIndex[nodeIndex] = agentIndex;
                    return NodeState.RUNNING;
                }
                if (result == NodeState.FAILURE)
                {
                    // Continue to next agent — FAILURE is not terminal
                    child = 0;
                    goto nextAgent;
                }
                child++;
            }
            child = 0;
            nextAgent:;
        }

        ctx.blackBoard.currentAgentOffset = 0;
        ctx.activeChildIndex[nodeIndex] = 0;
        ctx.runningAgentIndex[nodeIndex] = 0;
        return NodeState.SUCCESS;
    }
}
```

### ForEachRole

Same as ForEachAgent but filters: only ticks agents where `AgentRole[agentIndex]` equals a target role.

```
Behaviour: Sequential, filtered
Loop:      for agentIndex in 0..agentCount:
               if AgentRole[agentIndex] != targetRole → skip
               set currentAgentOffset = agentIndex
               tick children
           reset currentAgentOffset = 0
```

**Target role** is a baked constant field on the node (set at edit time) or a BB variable. Constant is simpler and sufficient for most cases.

```csharp
[NodeMethod("ForEachRole")]
public sealed class ForEachRoleMethod : CompositeMethod
{
    [SharedVar] public int agentCountSlot;
    [SharedVar] public int agentRoleSlot;       // base slot of AgentRole[stride=agentCount]
    public TacticalRole targetRole;              // baked constant from editor dropdown

    public override NodeState Execute(int nodeIndex, ref TickContext ctx)
    {
        // ... same loop structure as ForEachAgent, but with:
        // TacticalRole role = (TacticalRole)ctx.blackBoard.GetInt(agentRoleSlot + agentIndex);
        // if (role != targetRole) continue;
    }
}
```

### SelectAgent

Reads an agent ID from a BB variable, sets `currentAgentOffset` to that value, ticks children once, then clears the offset.

```
Behaviour: Single-shot
Flow:      agentID = BB.GetInt(targetAgentIDSlot)
           if agentID < 0 or agentID >= agentCount → return FAILURE (guard)
           set currentAgentOffset = agentID
           tick children
           reset currentAgentOffset = 0
           return result from children
```

**Invalid ID guard:** If the scan found nothing (e.g. `_targetAgentID` is still -1 from reset), the children are not executed and the node returns FAILURE. This can be caught by a parent Selector fallback.

```csharp
[NodeMethod("SelectAgent")]
public sealed class SelectAgentMethod : CompositeMethod
{
    [SharedVar] public int targetAgentIDSlot;    // BB variable holding the agent ID
    [SharedVar] public int agentCountSlot;        // for bounds check

    public override NodeState Execute(int nodeIndex, ref TickContext ctx)
    {
        ref NodeData node = ref ctx.nodeDatas[nodeIndex];
        if (node.firstChildIndex < 0) return NodeState.SUCCESS;

        int agentID = ctx.blackBoard.GetInt(targetAgentIDSlot);
        int agentCount = ctx.blackBoard.GetInt(agentCountSlot);

        // Guard: invalid target → fail immediately
        if (agentID < 0 || agentID >= agentCount)
            return NodeState.FAILURE;

        ctx.blackBoard.currentAgentOffset = agentID;

        // Tick all children in sequence
        NodeState result = TickChildren(node, ref ctx);

        ctx.blackBoard.currentAgentOffset = 0;
        return result;
    }
}
```

---

## Macros (Convenience Composites)

Macros are leaf composites that internally combine ForEachAgent + SelectAgent + comparison logic. They expose a single variable picker and tick their children as orders targeting the found agent.

### GetLowestAgent

Finds the agent with the lowest value of a given squad-data variable. Writes the agent's ID to `_targetAgentID` and its value to `_lowestValue`. Then selects that agent and ticks its children (the orders).

```
Internally:
    1. Reset _targetAgentID = -1, _lowestValue = float.MaxValue
    2. ForEachAgent:
         if IsAlive(agent) AND variableValue[agent] < _lowestValue:
             _lowestValue = variableValue[agent]
             _targetAgentID = agent
    3. if _targetAgentID < 0 → return FAILURE
    4. SelectAgent(_targetAgentID) → tick children
```

The user wires a single property: **Variable** — which squad-data variable to compare (dropdown from commander BB definition, filtered to numeric types). The children of GetLowestAgent are the orders to issue to the found agent.

### GetHighestAgent

Same as GetLowestAgent, but `variableValue[agent] > _highestValue`. Initial reset: `_highestValue = float.MinValue`.

### GetNearestAgent

Finds the agent nearest to a reference position. Reference position is a Vector3 BB variable (e.g. a squad waypoint).

```
Internally:
    1. Reset _targetAgentID = -1, _nearestDistance = float.MaxValue
    2. ForEachAgent:
         dist = Vector3.Distance(AgentPosition[agent], referencePosition)
         if dist < _nearestDistance:
             _nearestDistance = dist
             _targetAgentID = agent
    3. if _targetAgentID < 0 → return FAILURE
    4. SelectAgent(_targetAgentID) → tick children
```

Fields:
- **Agent Position Variable** — the squad-data Vector3 variable holding each agent's world position
- **Reference Position Variable** — the BB variable with the reference point

---

## Transient Variables

The macros and the two-phase ForEachAgent→SelectAgent pattern use these transient BB variables:

| Variable | Type | Stride | Purpose |
|---|---|---|---|
| `_targetAgentID` | int | 1 | Holds the agent ID found by a scan |
| `_lowestValue` | float | 1 | Tracks the lowest value during GetLowestAgent |
| `_highestValue` | float | 1 | Tracks the highest value during GetHighestAgent |
| `_nearestDistance` | float | 1 | Tracks the nearest distance during GetNearestAgent |
| `_agentCount` | int | 1 | Current number of registered agents (maintained by CommanderTreeRunner) |

These are **normal** variables in the commander's `BlackboardDefinition`. They are visible in the BB inspector, debuggable at runtime, and wiresd in the editor via the standard variable dropdown. They are not hidden or treated specially — they are just internal plumbing that happens to be user-visible. Naming convention (`_` prefix) indicates they are system-managed.

`_agentCount` is updated by `CommanderTreeRunner` whenever agents register/unregister. The composite reads it each tick to get the current loop bounds.

---

## Use Cases

### Use Case 1: Broadcast — "Everyone suppress that position"

All agents receive the same order in a single frame.

```
Tree:
    ForEachAgent
    ├── Action: SetAttackTarget(suppressTarget)   ← writes to AttackTarget[agentOffset]
    └── Action: SetStance(SUPPRESSIVE)             ← writes to Stance[agentOffset]
```

ForEachAgent sets `currentAgentOffset` → 0, 1, 2... each iteration. `SetAttackTarget` writes to `AttackTarget[0]`, `AttackTarget[1]`, etc. The agent's own tree reads `AttackTarget` (stride=1 in the merged agent BB) next frame and reacts.

All children return SUCCESS immediately (fire-and-forget). The whole loop completes in one tick.

### Use Case 2: Targeted Order — "Find weakest agent, order retreat"

Two-phase pattern using the primitives explicitly:

```
Sequence
├── Action: ResetBB(_targetAgentID, -1)           ← clear from previous ticks
├── Action: ResetBB(_lowestValue, 9999)            ← reset tracker
│
├── ForEachAgent                                    ← Phase 1: SCAN
│   ├── Condition: IsAlive
│   └── Sequence
│       ├── Condition: Compare AgentHealth < _lowestValue
│       ├── Action: SetBB(_lowestValue, AgentHealth)
│       └── Action: SetBB(_targetAgentID, agentID)  ← NEED: way to read current agentOffset
│
├── SelectAgent(_targetAgentID)                     ← Phase 2: ORDER
│   ├── Action: SetRetreatFlag(true)                ← writes to RetreatFlag[_targetAgentID]
│   └── Action: SetMoveTarget(fallbackPosition)
```

Note: in the ForEachAgent loop, the leaf needs to know the current `agentOffset` to write to `_targetAgentID`. This can be handled by:
- A dedicated `CurrentAgentID` leaf action that reads `currentAgentOffset` from the BB and writes it to a variable
- Or baking a special "intrinsic" `_currentAgentID` variable that always reflects `currentAgentOffset`

### Use Case 3: Macro — "Order nearest scout to investigate"

Using GetNearestAgent:

```
Sequence
├── Action: SetBB(_investigatePoint, heardGunshotPosition)
├── GetNearestAgent
│   ├── agentPositionVar: AgentPosition
│   ├── referenceVar: _investigatePoint
│   │
│   └── Action: SetInvestigateTarget(_investigatePoint)   ← writes to selected agent
```

The user sees a single composite node with two variable pickers. Internally, it runs the full ForEachAgent→SelectAgent scan.

### Use Case 4: ForEachRole — "Order all flankers to advance"

```
ForEachRole(targetRole: FLANKER)
├── Action: SetMoveTarget(flankingPosition)
└── Action: SetStance(AGGRESSIVE)
```

Only agents whose `AgentRole[agentIndex] == FLANKER` are iterated. Scout, Suppressor, etc. are skipped.

### Use Case 5: Choreographed Sequence — "Agents take cover one at a time"

Uses RUNNING support:

```
ForEachAgent
└── Sequence
    ├── Action: SetMoveTarget(coverPoint[agentIndex])
    └── Condition: HasReachedDestination    ← returns RUNNING until agent arrives
```

Agent 0 starts moving → RUNNING. Next frame: agent 0 still moving → RUNNING again. When SUCCESS: agent 1 starts. This prevents traffic jams and creates orderly, tactical movement. Without RUNNING support, all agents would receive orders simultaneously.

---

## Edge Cases

### 1. SelectAgent with no valid target

If `_targetAgentID` remains at its reset value (-1) because the scan found no match:

```
Sequence
├── GetLowestAgent(AgentHealth)     ← no agents alive → _targetAgentID stays -1
├── SelectAgent(_targetAgentID)     ← guard: -1 → returns FAILURE
└── Selector fallback:
    └── Action: Log("No valid target")
```

SelectAgent's guard prevents reading from garbage slots. Parent can fall back with a Selector.

### 2. Dynamic agent count changes mid-evaluation

If an agent unregisters during a frame (between `TickAgents` and `EvaluateCommander`), the loop bound `_agentCount` may be stale. This is acceptable — the next tick reads the updated count. The composite won't crash because all BB access is bounds-checked.

### 3. Nested ForEachAgent

```
ForEachAgent              ← outer: agentOffset 0..N-1
└── ForEachAgent          ← inner: overwrites agentOffset
    └── Action: ...
```

The inner loop overwrites `currentAgentOffset`. When it exits, the outer loop's next iteration sets the correct offset again. No special nesting support needed — the offset is always set explicitly by whichever composite is currently running.

### 4. `currentAgentOffset` and non-squad variables

If a leaf reads a stride=1 variable (e.g. `_targetAgentID` which is a transient scalar) while `currentAgentOffset = 3`, the storage adds 3 to the slot index, reading from the wrong slot. This is a **variable wiring error** — the user wired a non-squad variable into a context where agent offset is active.

**Mitigation options:**
- **Bake-time validation:** Warn if a node inside a composite reads a stride=1 variable. Needs scope-awareness in the baker.
- **Runtime guard:** `ManagedBlackboardStorage` could check if the resulting slot index exceeds the variable's slot range and clamp/log. This is defensive but adds per-access cost.
- **Documentation:** Document the convention — only squad-data variables (stride > 1) should be used inside ForEachAgent/SelectAgent children.

Leaning toward bake-time validation + documentation. The runtime cost of per-access bounds checking isn't worth it for a wiring error that won't happen at runtime if the baker catches it.

### 5. RUNNING resume across variable stride changes

If an agent unregisters while ForEachAgent is mid-loop with RUNNING, `runningAgentIndex` may point to a now-nonexistent agent. This is acceptable — the next tick's bounds check uses the updated `_agentCount`, and the loop will either skip the invalid index or hit bounds and complete. The composite does not crash.

### 6. SelectAgent inside ForEachAgent

```
ForEachAgent
└── SelectAgent(someID)      ← sets agentOffset to someID
    └── Action: ...          ← acts on someID, not the ForEachAgent's agentIndex
```

SelectAgent overwrites `currentAgentOffset` set by the outer ForEachAgent. When SelectAgent returns, the outer ForEachAgent's next iteration sets the offset to the correct `agentIndex`. This nested targeting is a valid pattern (e.g. "for each agent, find its nearest ally and face it").

---

## Implementation TODO

### Core Changes

- [ ] **`BlackBoard.currentAgentOffset`** — add transient `[NonSerialized] public int` field, default 0
- [ ] **`ManagedBlackboardStorage.GetBoxed`/`SetBoxed`** — add offset-aware variants or modify existing to accept offset from parent BlackBoard
  - Need a way for storage to read `currentAgentOffset` from its owning `BlackBoard`. Currently storage has no back-reference. Options:
    - (A) `BlackBoard` applies offset at its wrapper level before calling storage — storage API unchanged
    - (B) Storage holds a `WeakReference<BlackBoard>` or is passed offset explicitly
    - (A) is simpler: `BlackBoard.GetBoxed(int index)` computes `index + currentAgentOffset`, passes to storage. Requires adding an offset-aware `IBlackBoardAccess` method or computing in the wrapper.
- [ ] **`BlackBoard.GetBoxed`/`SetBoxed` wrapper** — apply `currentAgentOffset` before delegating to storage. Skip offset for `IBlackBoardAccess` calls? No — `IBlackBoardAccess` is what FieldBinding uses, so it must also apply the offset.
  - Actually: all `GetBoxed`/`SetBoxed` calls during commander tree evaluation should apply offset. The simplest is to compute `slot + currentAgentOffset` in the `BlackBoard` wrapper methods, then call storage with the final slot.

### `TickContext` Changes

- [ ] **`TickContext.runningAgentIndex`** — add `int[]` array (size = nodeDatas.Length), default 0, to persist ForEachAgent loop position across RUNNING frames. Cleared on SUCCESS/FAILURE exit.

### Composite Method Classes

- [ ] **`Core/ForEachAgentMethod.cs`** — `CompositeMethod` with `[NodeMethod("ForEachAgent")]`
  - Fields: `agentCountSlot` (SharedVar int)
  - Execute: loops agentIndex 0..agentCount-1, sets offset, ticks children sequentially
  - RUNNING: saves `runningAgentIndex[nodeIndex]` and `activeChildIndex[nodeIndex]`
  - FAILURE from child: continues to next agent (not terminal)
- [ ] **`Core/ForEachRoleMethod.cs`** — `CompositeMethod` with `[NodeMethod("ForEachRole")]`
  - Fields: `agentCountSlot`, `agentRoleSlot` (SharedVar int), `targetRole` (constant TacticalRole)
  - Execute: same loop, filtered by `AgentRole[agentIndex] == targetRole`
- [ ] **`Core/SelectAgentMethod.cs`** — `CompositeMethod` with `[NodeMethod("SelectAgent")]`
  - Fields: `targetAgentIDSlot`, `agentCountSlot` (SharedVar int)
  - Execute: reads agentID, guard bounds, sets offset, ticks children, clears offset
  - Invalid ID → returns FAILURE

### Macro Method Classes

- [ ] **`Core/GetLowestAgentMethod.cs`** — `CompositeMethod` with `[NodeMethod("GetLowestAgent")]`
  - Fields: `variableSlot` (SharedVar float/int — the squad-data variable to compare)
  - Internal: ForEachAgent scan → write `_targetAgentID` + `_lowestValue` → SelectAgent → tick children
  - Returns FAILURE if no match found
- [ ] **`Core/GetHighestAgentMethod.cs`** — same, highest instead of lowest
- [ ] **`Core/GetNearestAgentMethod.cs`** — `CompositeMethod` with `[NodeMethod("GetNearestAgent")]`
  - Fields: `agentPositionSlot` (SharedVar Vector3), `referencePositionSlot` (SharedVar Vector3)
  - Internal: ForEachAgent scan → compute distance → write `_targetAgentID` + `_nearestDistance` → SelectAgent → tick children

### Editor

- [ ] **Composite node registration** — ensure new composite types appear in the node creation menu
- [ ] **ForEachRole target role picker** — enum dropdown for `TacticalRole` in the node inspector
- [ ] **Macro variable pickers** — show variable dropdowns filtered to appropriate types (float/int for GetLowestAgent, Vector3 for GetNearestAgent)

### Baker Changes

- [ ] **Bake-time validation** — warn if a node inside ForEachAgent/SelectAgent subtree reads a stride=1 variable (non-squad data used in agent-offset context)
- [ ] **Transient variable auto-creation** — optionally auto-add `_targetAgentID`, `_lowestValue`, etc. to commander BB def when a macro or two-phase pattern is added

### CommanderTreeRunner Changes

- [ ] **`_agentCount` variable maintenance** — set BB `_agentCount` to `registeredAgents.Count` each tick
- [ ] **Initialize transient vars** — ensure `_targetAgentID = -1` and other transient vars are initialized on BB init

### Tests

- [ ] **ForEachAgent loops all agents** — create tree with 3 agents, verify children execute 3 times
- [ ] **ForEachAgent RUNNING resume** — child returns RUNNING, verify same agentIndex resumed next tick
- [ ] **ForEachRole filters correctly** — 2 flankers, 1 scout, verify children execute only for flankers
- [ ] **SelectAgent reads correct agent's data** — set agentID=2, verify child reads from slot[base+2]
- [ ] **SelectAgent invalid ID guard** — agentID=-1, verify returns FAILURE without ticking children
- [ ] **GetLowestAgent finds correct agent** — agent 0 health=100, agent 1 health=30, agent 2 health=60, verify target=agent 1
- [ ] **GetHighestAgent finds correct agent** — same setup, verify target=agent 0
- [ ] **GetNearestAgent finds closest** — agents at distances [10, 5, 20], reference at origin, verify target=agent 1
- [ ] **Nested composite offset reset** — ForEachAgent inside ForEachAgent, verify inner resets correctly
- [ ] **Dynamic agent count** — unregister an agent, verify next tick's ForEachAgent uses updated count
- [ ] **Offset bounds** — offset + slot out of range, verify no crash (returns null/default)
- [ ] **`currentAgentOffset` reset on composite exit** — verify offset is 0 after ForEachAgent returns SUCCESS
- [ ] **Macro returns FAILURE on no match** — all agents dead → GetLowestAgent returns FAILURE

---

## Files to Create

| File | Assembly | Purpose |
|---|---|---|
| `Core/ForEachAgentMethod.cs` | Core | ForEachAgent composite method |
| `Core/ForEachRoleMethod.cs` | Core | ForEachRole composite method |
| `Core/SelectAgentMethod.cs` | Core | SelectAgent composite method |
| `Core/GetLowestAgentMethod.cs` | Core | GetLowestAgent macro |
| `Core/GetHighestAgentMethod.cs` | Core | GetHighestAgent macro |
| `Core/GetNearestAgentMethod.cs` | Core | GetNearestAgent macro |
| `Editor/Tests/CommanderCompositeTests.cs` | Editor.Tests | NUnit tests for all composites |

## Files to Modify

| File | Change |
|---|---|
| `Core/BlackBoard.cs` | Add `[NonSerialized] public int currentAgentOffset`. Apply offset in `GetBoxed`/`SetBoxed` wrappers before calling storage. |
| `Runtime/TickContext.cs` | Add `public int[] runningAgentIndex` field for ForEachAgent RUNNING state persistence. |
| `Runtime/TreeEvaluator.cs` | Allocate `runningAgentIndex` array in constructor (same length as nodeDatas). Zero-initialize. |
| `Runtime/CommanderTreeRunner.cs` | Set `_agentCount` BB variable each tick. Initialize transient vars on BB init. |
| `Core/CompositeMethod.cs` | No change needed — `Execute(int nodeIndex, ref TickContext ctx)` already passes full context. |

---

## Design Decisions Log

| Decision | Rationale |
|---|---|
| `currentAgentOffset` on `BlackBoard`, not `TickContext` | Leaf BB access doesn't go through TickContext. Adding to BB avoids changing IBlackBoardAccess and all callers. |
| Normal stride=1 BB variables for transient data | Avoids name-based binding complexity. Visible + debuggable + zero new plumbing. |
| Sequential only, no parallel ForEachAgent | Use case (simultaneous RUNNING) handled by agent's own tree. Parallel variant adds complexity without value. |
| FAILURE in ForEachAgent is non-terminal | Treats failure as "skip this agent, try next." Users can wrap in Sequence if they want stop-on-failure. |
| SelectAgent guards invalid ID → FAILURE | Prevents garbage slot reads. Parent can catch with Selector fallback. |
| No getter for `currentAgentOffset` in leaf nodes via standard BB access | A dedicated `CurrentAgentID` leaf action or intrinsic `_currentAgentID` baked variable handles the case where a leaf needs to write the current agentIndex. |
