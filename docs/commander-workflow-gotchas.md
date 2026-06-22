# Commander Workflow — Common Gotchas

Things that silently go wrong in the Commander workflow and how to diagnose them.

---

## 1. Binding direction set to BOTH on self-variables

**Symptom:** Tracked binding pushes the correct value (e.g., `testInt = 20`), but tree nodes read `0`.

**Cause:** The variable is bound in the Squad Binding editor with direction `BOTH` (or `FromSquad`). During `CopySquadsToTree`, the commander BB (which has `0` for that slot) overwrites the agent BB after tracked bindings pushed the correct value.

**Per-frame order in `TickAgents()`:**
```
agent.PushTrackedBindings()   → writes self data to agent BB
CopySquadsToTree(agent, i)    → overwrites squad-data slots from commander BB
agent.Evaluate()              → reads BB
```

If the variable has a squad binding set to `BOTH` or `FromSquad`, `CopySquadsToTree` will overwrite it with whatever is in the commander BB (usually `0`).

**Fix:** Self-only variables (tracked bindings, per-agent state) should use direction `ToSquad` or have **no squad binding at all**. Only squad-shared data (tactical roles, formations, orders) should use `BOTH` or `FromSquad`.

---

## 2. Variable exists in tracked bindings but not in baked BB definition

**Symptom:** `ResolveTrackedBindings` silently skips a binding. `variableIndex` = `-1`.

**Cause:** The variable name in the tracked binding doesn't match any variable in the baked `runtimeAsset.blackboardDefinition`. This can happen after:
- Renaming a blackboard variable (the tracked binding still references the old name)
- Deleting a variable that a tracked binding points to
- The definition not being initialized yet when `ResolveTrackedBindings` runs

**Fix:** Re-select the variable in the TrackedVariables tab. The `TrackedBindingsPropagationHandler` should auto-update on rename, but if the rename happened outside the editor or during a stale session, the binding goes stale.

---

## 3. Squad-data variable stride mismatch

**Symptom:** Commander reads correct data for agent 0, garbage for agents 1+.

**Cause:** The `maxAgents` (stride) on the SquadDefinition doesn't match the actual number of registered agents. If stride is 8 but only 3 agents exist, slots for agents 4-7 are uninitialized (default values). If stride is 3 but 5 agents exist, agents 3 and 4 write out of bounds.

**Per-agent access pattern:**
```
baseSlot = sum of strides of all preceding variables
agentSlot = baseSlot + agentIndex   (where agentIndex < stride)
```

**Fix:** Set `maxAgents` to at least the maximum number of agents expected. `CommanderTreeRunner.RegisterAgent()` checks capacity at registration time.

---

## 4. Agent.Initialize() not called before ResolveTrackedBindings()

**Symptom:** `ResolveTrackedBindings` finds no groups or incorrect groups.

**Cause:** `ResolveTrackedBindings` uses `runtimeAsset?.sourceTreeGuid` to match the correct `TrackedBindingGroup`. If the runtime asset hasn't been baked yet (agent not initialized), `sourceTreeGuid` is null and only fallback matching is used — which might pick the wrong group.

**Correct init order** (enforced by `CommanderTreeRunner.Start()`):
```
1. Bake commander tree → runtimeAsset
2. Init commander BB
3. For each agent:
   a. agent.Initialize()     → bakes agent tree, sets runtimeAsset + sourceTreeGuid
   b. ResolveTrackedBindings() → now has correct sourceTreeGuid
```

---

## 5. currentAgentOffset not reset between Evaluate calls

**Symptom:** Nodes inside `ForEachAgent` read the wrong agent's data when the composite finishes.

**Cause:** `BlackBoard.currentAgentOffset` is set by `ForEachAgent` during iteration but is a public non-serialized field with no automatic reset. If a tree aborts mid-composite or a custom node sets it and doesn't restore it, subsequent nodes use the wrong offset.

**How ForEachAgent handles it (correctly):**
```csharp
int savedOffset = BB.currentAgentOffset;
for (each agent) {
    BB.currentAgentOffset = agentIndex;
    Execute();
}
BB.currentAgentOffset = savedOffset;
```

**Fix:** Any custom code that sets `currentAgentOffset` must save and restore it.

---

## 6. Tracked binding reads stale value due to caching

**Symptom:** Tracked binding pushes an old value even though the component field changed.

**Cause:** `ResolveTrackedBindings` caches `FieldInfo`/`PropertyInfo` via reflection. If the component is replaced or reloaded (domain reload, scene change), the cached info points to a stale object. `ResolveTrackedBindings` is called once during `OnPostInitialize` and never refreshed.

**Fix:** The `targetComponent` reference is checked every frame (`binding.targetComponent == null`), but the cached reflection info is not refreshed. If the component type changes, `ResolveTrackedBindings` must be called again.

---

## 7. Undo/redo loses tracked binding state on blackboard variable rename

**Symptom:** After renaming a blackboard variable, tracked bindings become stale (variable name mismatch).

**Mitigation:** The `TrackedBindingsPropagationHandler` subscribes to `VariableChangePropagator.ChangesFlushed` and updates tracked binding variable names on rename. However, this only works if:
- The propagation system is active (editor-only)
- The rename happens through the BlackBoard inspector (which triggers the propagator)

Direct .asset file edits or runtime changes won't trigger this.

---

## Debug Checklist

When Commander data flow is suspect, check in order:

1. Console: `[BB.Initialize]` logs (only in Editor) — verify definition name and variable slot layout
2. TrackedVariables tab — check binding directions (should not be `BOTH` for self-data)
3. SquadDefinition — check `maxAgents` matches agent count
4. CommanderTreeRunner.TickAgents() call stack — verify `PushTrackedBindings` → `CopySquadsToTree` → `Evaluate` order
5. `runtimeAsset.sourceTreeGuid` — must be set before `ResolveTrackedBindings` runs
