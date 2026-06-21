# Base Channels — Design

## Overview

Base channels are communication-contract variables that must exist on every blackboard
participating in the squad system. They form a consistent naming convention across
commander, squad, and agent blackboards, enabling the standard data flow:

**Commander writes orders → squad stores them → agent receives them.**

All base channel variables are marked `isSystemVariable = true` and are **locked**
in the BB editor (name, type, and stride disabled; delete button hidden).

---

## Variable Matrix

| Variable | Commander BB | Squad BB | Agent BB | Editable |
|---|---|---|---|---|
| `AgentRoles` | `int`, strided, `isSquadData` | `int`, strided, `isSquadData` | — | — |
| `AgentOrders` | `int`, strided, `isSquadData` | `int`, strided, `isSquadData` | — | — |
| `AgentAssignedRole` | — | — | `int`, stride=1 | **No** — set via `SquadConnection.assignedRoles` on squad registration |
| `AgentReceivedOrder` | — | — | `int`, stride=1 | Yes |

### Why plural on commander/squad, singular on agent

- Commander and squad deal with **all agents** — each slot `[agentID]` holds that agent's value.
- Agent only cares about **itself** — a single scalar.

### `AgentAssignedRole` — not editable in BB

The agent's role is assigned through `SquadConnection.assignedRoles` on the agent's tree
asset, not by editing the BB value directly. When the agent registers with a squad at runtime,
the spawn manager reads `assignedRoles` and writes the role index into `agent.BB.AgentAssignedRole`.

Because it is auto-managed, the BB value editor for this variable should be disabled
in the inspector (but the value is still readable).

---

## Data Flow Per Frame

```
┌─────────────────────────────────────────────────────────────────┐
│  Commander.TickAgents():                                        │
│                                                                 │
│  1. CopySquadsToTree(agent):                                    │
│     squad.BB.AgentRoles[agentID]  ──→  agent.BB.AgentAssignedRole       │
│     squad.BB.AgentOrders[agentID] ──→  agent.BB.AgentReceivedOrder│
│                                                                 │
│  2. agent.Evaluate():                                           │
│     agent reads AgentAssignedRole, AgentReceivedOrder from own BB       │
│                                                                 │
│  3. CopySquadsFromTree(agent):                                  │
│     (agent writes back to squad if needed)                      │
│                                                                 │
├─────────────────────────────────────────────────────────────────┤
│  Commander.EvaluateCommander():                                 │
│                                                                 │
│  1. CopySquadsToTree(commander):                                │
│     squad.BB → commander.BB  (e.g. AgentRoles for filtering)    │
│                                                                 │
│  2. evaluator.Evaluate(commanderBB):                            │
│     commander writes AgentOrders[agentID] = ...                 │
│                                                                 │
│  3. CopySquadsFromTree(commander):                              │
│     commander.BB.AgentOrders[N] → squad.BB.AgentOrders[N]       │
└─────────────────────────────────────────────────────────────────┘
```

---

## Auto-Binding in Squad Definition

When `SquadDefinition.OnValidate()` detects that `blackboardDefinition` is assigned,
it ensures the base channel variables exist **and** that bindings are created connecting
them to each connected tree.

### Squad-side auto-created variables

| Variable | Type | Stride | isSquadData |
|---|---|---|---|
| `AgentRoles` | `int` | 1 (runtime-managed) | true |
| `AgentOrders` | `int` | 1 (runtime-managed) | true |

### Auto-bindings created per connected tree

For each `SquadBindingGroup` in `bindingGroups`, `OnValidate()` ensures:

| Squad Var | Tree Var | Direction |
|---|---|---|
| `AgentRoles` | `AgentRoles` (commander) / `AgentAssignedRole` (agent) | `FromSquad` |
| `AgentOrders` | `AgentOrders` (commander) / `AgentReceivedOrder` (agent) | `Both` |

The direction differs per tree type:
- **Commander**: `AgentOrders` is `Both` (commander writes orders, reads current state).
  `AgentRoles` is `FromSquad` (commander reads roles for filtering).
- **Agent**: `AgentOrders` → `AgentReceivedOrder` is `FromSquad` (agent only receives).
  `AgentRoles` → `AgentAssignedRole` is `FromSquad` (agent only reads its role).

### Distinguishing commander vs agent trees

`OnValidate()` can determine tree type by checking if `treeAsset` is a `CommanderTreeAsset`
(via `AssetDatabase.LoadAssetAtPath` + type check). If the tree asset type cannot be
resolved, log a warning and skip auto-binding for that group.

---

## Auto-Creation & Validation Triggers

| BB Owner | Creation trigger | Validation fallback |
|---|---|---|
| `CommanderTreeAsset` | `CreateBlackBoard()` | `OnValidate()` |
| `SquadDefinition` | `OnValidate()` (when `blackboardDefinition` set) | `OnValidate()` |
| `AgentTreeAsset` | — | `OnValidate()` (when `squadConnections.Count > 0`) |

### `OnValidate()` logic (pseudocode)

```
OnValidate():
    if bbDef is null or sharedVariables is null: return
    
    EnsureBaseChannel<int>(bbDef, "AgentRoles", isSquadData: true)   // all BBs
    EnsureBaseChannel<int>(bbDef, "AgentOrders", isSquadData: true)  // commander + squad
    
    // Agent-only
    if this is AgentTreeAsset:
        EnsureBaseChannel<int>(bbDef, "AgentAssignedRole",  isSquadData: false)
        EnsureBaseChannel<int>(bbDef, "AgentReceivedOrder", isSquadData: false)

EnsureBaseChannel<T>(bbDef, name, isSquadData):
    existing = bbDef.FindVariable(name)
    if existing is not null:
        if existing.GetValueType() != typeof(T):
            log warning "Base channel '{name}' has wrong type"
        if not existing.isSystemVariable:
            existing.isSystemVariable = true         // repair
        return
    
    log warning "Missing base channel '{name}' — auto-creating"
    variable = new BlackboardVariable<T>:
        Name = name
        Stride = 1
        isSquadData = isSquadData
        isSystemVariable = true
    bbDef.sharedVariables.Add(variable)
```

---

## Locking in BlackBoardView

In `BindVariableListItem`, when `variable.isSystemVariable` is true:

| UI Element | Behavior |
|---|---|
| Row background | `style.backgroundColor = GraphEditorTheme.instance.systemVariableRow` |
| Name field (`TextField`) | `SetEnabled(false)` |
| Type dropdown (`DropdownField`) | `SetEnabled(false)` |
| Stride field (`IntegerField`) | `SetEnabled(false)` (when visible) |
| Delete button | `RemoveFromHierarchy()` or `SetEnabled(false)` + hidden |

Value editors remain functional for non-SquadData, non-role variables
(e.g., `AgentReceivedOrder` is editable).

The row tint uses `GraphEditorTheme.systemVariableRow` (`0.70, 0.40, 0.10, 0.30`) —
the same orange-brown as commander composite node headers but semi-transparent
for a subtle row highlight.

---

## `AgentAssignedRole` Value Population at Runtime

The agent's `AgentAssignedRole` value is not edited in the BB inspector. It is populated at
runtime when the spawn manager registers the agent with its squad:

```
Manager.SpawnAgent():
    agent.Initialize()
    commander.RegisterAgent(agent)
    agent.RegisterSquad(squad)
    
    // Read from agent's tree asset SquadConnection
    roleName = agent.treeAsset.squadConnections[0].assignedRoles[0]
    roleIndex = squad.availableRoles.FindIndex(r => r.name == roleName)
    
    agent.BB.AgentAssignedRole = roleIndex
```

The spawn manager is responsible for finding the correct `SquadConnection` for the
agent (matching squad to agent's assigned connection) and resolving the role name
to an index in `squad.availableRoles`.

---

## Implementation Order

1. Add `isSystemVariable` field to `BlackboardVariableBase`
2. Lock system variables in `BlackBoardView.BindVariableListItem`
3. Add `EnsureBaseChannel<T>` helper to `CommanderTreeAsset`
4. Call from `CreateBlackBoard()` + `OnValidate()` in `CommanderTreeAsset`
5. Add `OnValidate()` to `SquadDefinition` — ensure squad BB has channels + auto-bindings
6. Add `OnValidate()` to `AgentTreeAsset` — ensure agent BB has channels
7. Update `ForEachRoleMethod.agentRoleSlot` with `[SharedVar(IsHidden = true, AutoVariableName = "AgentRoles")]`
8. Implement spawn manager role population (separate phase)
