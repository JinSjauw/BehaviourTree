# Squad System — Architecture

## Overview

The squad system enables commander trees to orchestrate groups of agents through a shared data hub. Each squad has its own schema (`BlackboardDefinition`), a set of available role names, and a bidirectional variable-binding layer that connects trees to squad data without coupling.

---

## Core Data Types

### SquadDefinition (ScriptableObject)

```
SquadDefinition
├── BlackboardDefinition blackboardDefinition    ← squad's own schema
├── List<string> availableRoles                  ← ["Scout", "Flanker", "Defender"]
└── List<SquadBindingGroup> bindingGroups        ← one per tree that connects
```

### SquadBindingGroup

```
SquadBindingGroup
├── BehaviourTreeAssetBase treeAsset             ← which tree this group maps
└── List<VariableBinding> bindings
```

### VariableBinding

```
VariableBinding
├── string treeVariableName                      ← variable in the tree's BB def
├── string squadVariableName                     ← variable in squad's BB def
└── BindingDirection direction                   ← In, Out, Both
```

### BindingDirection

```csharp
public enum BindingDirection
{
    ToSquad,      // tree → squad
    FromSquad,    // squad → tree
    Both          // bidirectional
}
```

---

## SquadConnection (on tree assets)

Each tree asset (`BehaviourTreeAsset`, `CommanderTreeAsset`) holds:

```
List<SquadConnection> squadConnections
    └── SquadConnection
        ├── SquadDefinition squad
        ├── string assignedRole                   ← agent only, picked from squad.availableRoles
        └── (bindings live on SquadDefinition, not here)
```

The tree references the squad. The bindings live on the squad definition, keyed by `treeAsset`. Both the squad inspector and the tree's squads tab edit the same data.

---

## Dynamic Stride (Commander BB)

### isSquadData

`BlackboardVariableBase` gains:

```csharp
public bool isSquadData;   // if true, stride is runtime-managed
```

Commander BB variables marked `isSquadData` get resized dynamically when agents register/unregister. Non-squad-data variables keep their authored stride.

### Agent ID

Assigned by commander on registration: `agentID = registeredAgents.Count - 1`. Equals the slot offset into commander's strided arrays (`commanderBaseSlot + agentID`).

### Compaction

When an agent leaves:
1. Remaining agents with higher IDs are shifted down
2. Their BB slots are copied to new positions
3. Commander BB storage is resized to new count
4. `agent.bridge.agentID` is updated

---

## Runtime Components

### SquadInstance (MonoBehaviour)

```
SquadInstance
├── SquadDefinition definition
├── BlackBoard blackBoard                          ← initialized from definition.blackboardDefinition
├── Dictionary<BlackboardDefinition, int[]> copyToCache    ← [treeSlot, squadSlot]
├── Dictionary<BlackboardDefinition, int[]> copyFromCache  ← [squadSlot, treeSlot]
│
├── void Initialize(SquadDefinition def)
├── void EnsureResolved(BlackboardDefinition treeDef)     ← lazy, idempotent
├── void CopyToBB(BlackBoard treeBB, BlackboardDefinition treeDef)   ← squad → tree
├── void CopyFromBB(BlackBoard treeBB, BlackboardDefinition treeDef) ← tree → squad
```

`EnsureResolved` finds the `SquadBindingGroup` where `treeAsset` matches the tree's definition owner. Resolves variable names to slot indices, caches flat `[srcSlot, dstSlot]` arrays.

`CopyToBB` and `CopyFromBB` iterate cached arrays, calling `GetBoxed`/`SetBoxed`. Direction-agnostic — delegates to binding direction control what gets copied.

---

## Per-Frame Tick Order

### CommanderTreeRunner.Update()

```
TickAgents():
  for each agent:
    1. squad.CopyToBB(agent.selfBB, agentDef)        ← squad data → agent
    2. bridge.CopyCommanderToAgent()                  ← commander[agentID] → agent
    3. agent.Evaluate()                               ← agent tree runs
    4. bridge.CopyAgentToCommander()                  ← agent → commander[agentID]
    5. squad.CopyFromBB(agent.selfBB, agentDef)       ← agent → squad

EvaluateCommander():
  squad.CopyToBB(commander.commanderBB, cmdrDef)      ← squad → commander
  evaluator.Evaluate(commanderBB)                     ← commander tree runs
  squad.CopyFromBB(commander.commanderBB, cmdrDef)    ← commander → squad
```

---

## Commander Tree Composites

### ForEachRole

Iterates over `squadDefinition.availableRoles`. Sets `CurrentRole` BB variable. Evaluates children once per role.

### ForEachAgent

Iterates `agentID = 0..agentCount-1`. Sets BB access offset so children's `[SharedVar]` fields resolve to `baseSlot + agentID`.

### ForEachAgentWithRole

Iterates agents where `AgentRole[i] == CurrentRole`. Sets BB access offset to the matching index.

### ForEachAgentByIndex

Explicit index input (constant or BB variable). Sets BB access offset to that index.

All four composites set the same BB offset. Children use `[SharedVar]` fields transparently — they read/write `baseSlot + offset`.

---

## BB Access Offset

The `TreeEvaluator` gains an optional `agentOffset` field. When set by an iteration composite, all subsequent node BB access uses `bbSlotIndex + agentOffset` for fields on squad-data variables. The offset resets when exiting the composite.

---

## Role System

### Flow

```
SquadDefinition.availableRoles       ["Scout", "Flanker", "Defender"]
          │
          ▼ (designer picks per tree)
SquadConnection.assignedRole         "Flanker"
          │
          ▼ (manager sets at runtime)
Commander.BB.AgentRole[agentID]      1 (index of "Flanker")
Squad.BB.AgentRole[agentID]          1
          │
          ▼ (ForEachAgentWithRole filters)
Matches agents where AgentRole[i] == CurrentRole
```

---

## Grand Commander (Optional Extension)

A tree with no agents that coordinates sub-commanders.

### Differences from Regular Commander

| | Commander | Grand Commander |
|---|---|---|
| Agents | Owns N agents | None |
| Squad | One SquadDefinition | No SquadDefinition |
| BB | Strided + scalar | Scalar only |
| Composites | ForEachRole, ForEachAgent | Sequence, Priority, etc. |
| Connects to | Agents + Squad | Sub-commanders (via squad binding) |

### Communication

Sub-commanders treat the GrandCommander's BB as a squad:

```
Sub-commander BB ←→ GrandCommander BB (scalar only, no stride)
```

Same binding UI, same `CopyToBB`/`CopyFromBB` mechanism.

### Tick Order

```
1. All sub-commanders tick agents + evaluate
2. Sub-commanders copy results → GrandCommander BB
3. GrandCommander evaluates, writes orders
4. GrandCommander copies orders → sub-commander BB
5. (Next frame) sub-commanders read orders, distribute to agents
```

One-frame lag. Acceptable for tree→tree BB communication.

---

## Editor UI

### SquadDefinition Inspector

```
┌─ SquadDefinition ────────────────────────────┐
│  Schema: [BlackboardDefinition ▼]             │
│  ┌─ Variables ────────────────────────────┐  │
│  │  ← reuses BlackBoardView variable list  │  │
│  └────────────────────────────────────────┘  │
│                                               │
│  Available Roles:                             │
│  ["Scout"] ["Flanker"] ["Defender"] [+ Add]   │
│                                               │
│  ┌─ Bindings for: [AgentTree ▼] ──────────┐  │
│  │  TreeVar   →  SquadVar         Dir     │  │
│  │  Health    →  AgentHealth[8]   Out     │  │
│  │  MoveOrder ←  OrderMove[8]     In      │  │
│  │  [+ Add]                                │  │
│  └─────────────────────────────────────────┘  │
│                                               │
│  ┌─ Bindings for: [CmdrTree ▼] ───────────┐  │
│  │  CmdrVar    → SquadVar          Dir     │  │
│  │  MoveOrder  → AgentMoveOrder[8] Out     │  │
│  │  AgentHealth ← AgentHealth[8]   In      │  │
│  │  [+ Add]                                │  │
│  └─────────────────────────────────────────┘  │
│                                               │
│  [+ Add Tree Binding Group]                   │
└───────────────────────────────────────────────┘
```

### Behaviour Tree Editor — Squads Tab

Same tab on both agent and commander tree editors.

```
┌─ Blackboard │ Inspector │ Squads ─────────┐
│                                             │
│  ┌─ CombatSquad ▸ ──────────────────────┐  │
│  │  (Agent only) Role: [Flanker ▼]       │  │
│  │                                        │  │
│  │  TreeVar    → SquadVar        Dir     │  │
│  │  [Health ▼] → [AgentHealth ▼] Out     │  │
│  │  [Ammo ▼]   → [AgentAmmo ▼]   Out     │  │
│  │  [+ Add Binding]                       │  │
│  │  ↑ edits SquadDefinition directly      │  │
│  └────────────────────────────────────────┘  │
│                                             │
│  [+ Connect Squad]                          │
└─────────────────────────────────────────────┘
```

- **Scope** — each foldout shows only the `SquadBindingGroup` where `treeAsset == currentTree`
- **Role picker** — agent only; filtered to `squad.availableRoles`
- **Variable pickers** — reuse `VariableSearchPopup`, scoped to tree's BB def (left) and squad's BB def (right)
- **Binding table** — reusable `VisualElement` shared between squad inspector and squads tab

---

## Dynamic Spawn Flow

```
Manager.SpawnSquad():
  1. squad = Instantiate(prefab)
  2. squad.Initialize(squadDefinition)        // builds storage from schema

Manager.SpawnCommander():
  3. commander = Instantiate(prefab)
  4. commander.Initialize()
  5. commander.RegisterSquad(squad)           // reads commanderTree.squadConnections
     → squad.EnsureResolved(commander.BB.Definition)

Manager.SpawnAgent():
  6. agent = Instantiate(prefab)
  7. agent.Initialize()
  8. commander.RegisterAgent(agent)           // assigns agentID, resizes strided BB
  9. agent.RegisterSquad(squad)               // reads agentTree.squadConnections
     → squad.EnsureResolved(agent.selfBB.Definition)
     → commander.BB.AgentRole[agentID] = roleIndex
     → squad.BB.AgentRole[agentID] = roleIndex
```

---

## Files to Create

| File | Assembly | Purpose |
|---|---|---|
| `Core/SquadDefinition.cs` | Core | SO + `SquadBindingGroup` + `VariableBinding` + `BindingDirection` |
| `Core/SquadConnection.cs` | Core | Serializable struct on tree assets |
| `Runtime/SquadInstance.cs` | Runtime | Live MonoBehaviour with resolves + copy methods |
| `Editor/SquadDefinitionEditor.cs` | Editor | Custom inspector window |
| `Editor/SquadTabView.cs` | Editor | Squads tab for BehaviourTreeEditor |

## Files to Modify

| File | Change |
|---|---|
| `Core/BlackboardVariableBase.cs` | Add `isSquadData` field |
| `Core/BehaviourTreeAssetBase.cs` | Add `List<SquadConnection> connectedSquads` |
| `Runtime/TreeRunner.cs` | Add `List<SquadInstance> registeredSquads`, `RegisterSquad()`, `UnregisterSquad()` |
| `Runtime/CommanderTreeRunner.cs` | Dynamic stride resize, squad copies in tick loop, agent compaction |
| `Runtime/CommanderBindingBridge.cs` | Remove `IBlackboardDataProvider` dead code |
| `Core/IBlackboardDataProvider.cs` | Delete (unused, tracked bindings cover it) |
| `Editor/BehaviourTreeEditor.cs` | Add squads tab to editor window |
| `Runtime/TreeEvaluator.cs` | Add `agentOffset` for iteration composites |
