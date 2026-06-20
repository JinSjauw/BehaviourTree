# Behaviour Tree Editor — Technical Specification

## Project Overview

A Unity behaviour tree editor for orchestrating groups of AI agents through a squad/commander system. Built on Unity's `UI Toolkit` (`UIElements`), `ScriptableObject`-based assets, and a custom flat-storage blackboard system with stride-aware per-agent arrays.

**Unity Version:** 6000.x (UI Toolkit with UXML/USS, `[UxmlElement]` source generators)

---

## Assembly Structure

```
BehaviourTree.Utility
    └── no references, unsafe code enabled
        │
BehaviourTree.Core
    └── no asmdef references (implicit Unity), unsafe code enabled
        │
BehaviourTree.Runtime
    └── depends on Core + Utility
        │
BehaviourTree.Editor
    └── depends on Core + Runtime, Editor-only
        │
BehaviourTree.Editor.Tests
    └── depends on Core + Runtime + Editor + NUnit, Editor-only
BehaviourTree.Runtime.Tests
    └── depends on Core + Runtime + UnityEngine.TestRunner + NUnit
```

---

## Assembly: BehaviourTree.Core

**File count:** 32 `.cs`  
**Role:** Data model, asset definitions, storage abstractions. No Unity dependency beyond `ScriptableObject`, `MonoBehaviour`, `Debug`.

### Type Hierarchy

#### Node Model

```
ScriptableObject
└── BehaviourNode (abstract)          — Name, children[], guid, position, comment, runtimeIndex
    ├── RootNode                     — ROOT type, no extra fields
    ├── CompositeNode                — COMPOSITE type, methodName, fieldEntries, abortType
    ├── LeafNode                     — ACTION/CONDITION type, methodName, fieldEntries, BlackBoardTypeID
    ├── DecoratorNode                — DECORATOR type, methodName, fieldEntries, BlackBoardTypeID
    └── SubtreeNode                  — SUBTREE type, subTreeAsset ref, bindings[]
```

#### BehaviourNodeType enum

| Value | Purpose |
|---|---|
| `ROOT` | Entry point, single child |
| `COMPOSITE` | Controls child execution order (Sequence, Selector, etc.) |
| `CONDITION` | Leaf that returns SUCCESS/FAILURE |
| `ACTION` | Leaf that performs work, may return RUNNING |
| `DECORATOR` | Wraps single child, transforms result |
| `SUBTREE` | Inline expansion of another tree asset |

#### NodeState enum

| Value | Meaning |
|---|---|
| `NONE` | Uninitialized |
| `FAILURE` | Node failed |
| `SUCCESS` | Node succeeded |
| `RUNNING` | Node still executing, frame continuation |
| `INACTIVE` | Node skipped (conditional abort, not yet reached) |

#### AbortType enum

| Value | Behavior |
|---|---|
| `None` | No conditional abort |
| `Self` | Composite re-checks own condition; if false → abort and restart from child 0 |
| `LowerPriority` | Running node's condition re-evaluated; if false → abort, composite continues to next child |
| `Both` | Self + LowerPriority |

### Blackboard System

#### Definition Layer (Authoring)

```
ScriptableObject
├── BlackboardDefinition          — [SerializeReference] List<BlackboardVariableBase> sharedVariables
└── CommanderBlackboardDefinition — inherits BlackboardDefinition, adds maxSize (int, default 8)
```

#### Variable Model

```
[Serializable]
BlackboardVariableBase (abstract)  — Name, Stride, TypeName, isSquadData, IsArray
    └── BlackboardVariable<T> : BlackboardVariableBase  — singleValue (T), arrayValues (List<T>)
```

**Key properties:**

| Property | Purpose |
|---|---|
| `Name` | Variable identifier, used for binding resolution |
| `Stride` | Number of consecutive storage slots. 1 = scalar, N = per-agent array |
| `TypeName` | Assembly-qualified type name for reflection-based dispatch |
| `isSquadData` | If true, stride managed dynamically by CommanderTreeRunner at runtime |
| `IsArray` | Editor hint: shows array editing UI |

#### Runtime Layer

```
MonoBehaviour
└── BlackBoard : IBlackBoardAccess       — Runtime BB component
    ├── serializedReferences: List<UnityEngine.Object>     — reference-type variable storage
    ├── valueOverrides: List<BlackboardValueOverride>      — per-component value-type overrides
    ├── overriddenReferenceSlots: List<int>                — persisted override tracking
    ├── GetBoxed(int slot) / SetBoxed(int slot, object val) — unified access
    ├── GetTotalSlotCount()           — sum of all variable strides
    ├── SetValueOverride() / ClearValueOverride()
    ├── IsReferenceSlotOverridden()
    └── SetReferenceSlotOverridden() / ClearReferenceSlotOverridden()

IBlackboardStorage (interface)
└── ManagedBlackboardStorage (sealed)  — Flat object[] storage
    ├── GetVariableSlotRange(int varIndex, out baseSlot, out stride)
    ├── GetBoxed(slot) / SetBoxed(slot, val) / CanWriteBoxed(slot, val)
    └── ResizeFromVariables(def)       — stride-aware reallocation

[Serializable]
BlackboardValueOverride                — variableName + elementIndex + boxedValue

BlackBoardType enum                    — SELF, SQUAD
```

#### Data Providers

```
IBlackboardDataProvider (interface)
    └── ProvideData(BlackBoard bb)     — push component data to BB before evaluation
```

### Squad System (Core Models)

```
ScriptableObject
└── SquadDefinition                       — [CreateAssetMenu]
    ├── blackboardDefinition: BlackboardDefinition   — squad schema
    ├── availableRoles: List<string>                 — e.g. ["Scout", "Flanker"]
    └── bindingGroups: List<SquadBindingGroup>       — per-tree bindings

[Serializable]
SquadBindingGroup
    ├── treeAsset: BehaviourTreeAssetBase
    └── bindings: List<VariableBinding>

[Serializable]
VariableBinding
    ├── treeVariableName: string
    ├── squadVariableName: string
    └── direction: BindingDirection

[Serializable]
SquadConnection
    ├── squad: SquadDefinition
    └── assignedRole: string

BindingDirection enum          — ToSquad, FromSquad, Both
```

### Tree Asset

```
ScriptableObject
└── BehaviourTreeAssetBase
    ├── root: RootNode
    ├── blackboardDefinition: BlackboardDefinition
    ├── commanderBlackboardDefinition: CommanderBlackboardDefinition
    └── squadConnections: List<SquadConnection>
```

### Method / Field Infrastructure

```
[AttributeUsage(Class)]
NodeMethodAttribute              — methodName string key for registration
    └── Allows multiple per class (multiple method name aliases)

[AttributeUsage(Field)]
SharedVarAttribute               — Marks a field as a BB variable (isToggleVariable)
SharedArrayAttribute             — Marks a field as a strided BB array variable

FieldTypeHelper (static)         — CommonTypes array, GetDisplayName(), IsCommonType()

[Serializable][StructLayout(Explicit)]
FieldData                        — Packed 5-byte union: mode byte + 4-byte value
    ├── Mode 0 (Constant): value = numeric constant
    ├── Mode 1 (BB Variable): value = slot offset in BB storage
    └── Mode 2 (Boxed Constant): value = index into boxedConstants array

[Serializable]
NodeData                         — Runtime node descriptor
    ├── type: BehaviourNodeType
    ├── firstChildIndex / lastChildIndex: short
    ├── methodName: string
    ├── firstFieldDataIndex / fieldDataCount: short
    └── abortType: AbortType

[Serializable]
NodeFieldEntry                   — Editor-side union: constant value OR BB variable name

class FieldBinding (sealed)      — Compiled binding: FieldInfo, bbSlotIndex, isOutput
    ├── ReadFromBBGeneric(IBlackBoardAccess bb)
    └── WriteToBBGeneric(IBlackBoardAccess bb, object value)
```

---

## Assembly: BehaviourTree.Runtime

**File count:** 33 `.cs`  
**Role:** Runtime evaluation, baking, runner lifecycle, squad data copying, standard BT methods.

### Runner Hierarchy

```
MonoBehaviour
└── BehaviourTreeRunnerBase (abstract)  — [RequireComponent(BlackBoard)]
    ├── Bake + init BB
    ├── Create TreeEvaluator
    ├── RuntimeDebugProvider registration
    └── public BlackBoard BlackBoard { get; }

    └── AgentTreeRunner                     — [RequireComponent(BlackBoard)]
        ├── RunIndependently: bool           — when false, commander drives evaluation
        ├── registeredSquads: List<SquadInstance>
        ├── Start(): Initialize (bake + init BB), RegisterSquad
        ├── Update(): if RunIndependently → PushDataProviders + Evaluate
        ├── Evaluate(): Evaluate(BlackBoard) → push callbacks
        └── RegisterSquad/UnregisterSquad

    └── CommanderTreeRunner                 — [RequireComponent(BlackBoard)]
        ├── registeredAgents: List<AgentTreeRunner>
        ├── bindingBridges: List<CommanderBindingBridge>
        ├── squadInstances: List<SquadInstance>
        ├── Start(): Initialize commander BB + bake, init all agents, resolve bridges
        ├── Update(): TickAgents loop → EvaluateCommander
        ├── RegisterAgent/UnregisterAgent: dynamic stride resize
        └── ResizeSquadDataStrides(n) / CompactSquadDataAfterRemoval

MonoBehaviour
└── CommanderBindingBridge
    ├── ResolveBindings()            — matches variable names, computes triplet [(selfSlot, commanderSlot, stride)]
    ├── CopyCommanderToAgent()       — commander BB → agent merged BB (agentID offset)
    ├── CopyAgentToCommander()       — agent merged BB → commander BB (agentID offset)
    └── PushDataProviders()          — IBlackboardDataProvider.ProvideData(self BB)

MonoBehaviour
└── SquadInstance                    — [RequireComponent(BlackBoard)]
    ├── Initialize(squadDef, treeDef)    — find matching SquadBindingGroup, resolve variable pairs
    ├── CopyToBB()                       — bound tree variables → squad BB (filtered by direction)
    ├── CopyFromBB()                     — squad BB → bound tree variables (filtered by direction)
    └── EnsureResolved()                 — lazy resolution guard

MonoBehaviour
└── RuntimeDebugProvider             — Exposes currentNodeStates[], currentNodeGuids[] to editor
```

### Data Flow Per Frame (CommanderTreeRunner)

```
CommanderTreeRunner.Update():
    for each agent:
        bridge.PushDataProviders()       ← IBlackboardDataProvider → self BB
        bridge.CopyCommanderToAgent()    ← commander BB → agent merged BB
        agent.Evaluate()                 ← agent tree reads merged BB
        bridge.CopyAgentToCommander()    ← agent merged BB → commander BB
    
    for each squad:
        squad.CopyFromBB(commanderBB)    ← commander BB → squad BB
        squad.CopyToBB(commanderBB)      ← squad BB → commander BB
    
    evaluator.Evaluate(commanderBB)      ← commander tree runs
```

### Evaluation System

```
TickContext (struct)                     — Per-frame snapshot
    ├── nodeDatas: NodeData[]
    ├── methodInstances: NodeMethod[]
    ├── nodeStates: NodeState[]
    ├── activeChildIndex: int[]
    ├── lastConditionResult: bool[]
    ├── lastChildIndex: int[]
    ├── blackBoard: BlackBoard
    ├── startTime: float
    ├── deltaTime: float
    └── runningAgentIndex: int[]  (Phase 8 — planned)
    └── Initialize(nodeDataArray, fieldDataArray, methodArray, bb)
    └── ResetStates()

TickDispatcher (internal static)         — Delegate table: NodeType → TickHandler
    └── TickNode(int nodeIndex, ref TickContext ctx) → NodeState

TickFunctions (partial class)            — Static tick implementations
    ├── TickLeaf()            — ResolveInputsGeneric → ActionMethod/ConditionMethod.Execute
    ├── TickComposite()       — CompositeMethod.Execute(nodeIndex, ref ctx)
    ├── TickDecorator()       — Tick child → DecoratorMethod.Execute
    ├── TickSubtree()         — Tick expanded first child
    └── CheckConditionalAbort() — Self/LowerPriority/Both abort logic

TreeEvaluator (class)
    ├── constructor(runtimeAsset, extraMethods) — Builds fixed arrays, fills TickDispatcher
    ├── Evaluate(BlackBoard bb) → NodeState     — Builds TickContext, TickNode(0, ref ctx), broadcasts callbacks
    └── Uses fixed-size arrays for zero GC allocation

NodeMethod (abstract)
    ├── FieldBinding[] fieldBindings
    ├── ResolveInputsGeneric(IBlackBoardAccess bb)
    ├── WriteOutputsGeneric(IBlackBoardAccess bb, object outParams)
    └── DeserializeFields(FieldData[], boxedConstants, blackboardDef) → FieldBinding[]

    └── ActionMethod     — abstract void Execute()
    └── ConditionMethod  — abstract bool Execute()
    └── DecoratorMethod  — abstract NodeState Execute(NodeState childResult)
    └── CompositeMethod  — abstract NodeState Execute(int nodeIndex, ref TickContext ctx)
```

### Method Registry

```
MethodRegistry (static)
    ├── RegisteredMethodData[] registeredMethods   — methodName, type, MethodInfo, fieldInfo[]
    ├── RebuildRegistry()                          — scans assemblies for NodeMethod subclasses
    ├── GetMethodInstance(string name) → NodeMethod — creates instance with compiled FieldBinding[]
    └── event Action OnRegistryRebuilt
```

### Standard Methods

#### Composites

| Method | `[NodeMethod]` | Behavior |
|---|---|---|
| `SequenceMethod` | SEQUENCE | Left→right, stops on FAILURE, resumes on RUNNING |
| `SelectorMethod` | SELECTOR | Left→right, stops on SUCCESS |
| `PriorityMethod` | PRIORITY | Re-evaluates from child 0 every tick (dynamic priority) |
| `ParallelMethod` | PARALLEL | All children ticked every frame, per-child completion |

#### Decorators

| Method | Behavior |
|---|---|
| `Inverter` | SUCCESS → FAILURE, FAILURE → SUCCESS |
| `Repeater` | Loop child N times, always return SUCCESS when done |

#### Time

| Method | Type | Behavior |
|---|---|---|
| `WaitSeconds` | ActionMethod | Returns RUNNING for N seconds |
| `Cooldown` | ConditionMethod | Returns SUCCESS after cooldown interval |

#### Blackboard Comparison (ConditionMethods)

| Method | Compare Ops |
|---|---|
| `BB_CompareInt` | Equal, NotEqual, Less, LessOrEqual, Greater, GreaterOrEqual |
| `BB_CompareFloat` | Same |
| `BB_CompareBool` | Equal, NotEqual |

#### Blackboard Checks (ConditionMethods)

| Method | Ops |
|---|---|
| `BB_CheckBool` | IsTrue, IsFalse |
| `BB_CheckGameObject` | IsNull, IsNotNull, IsActive, IsInactive |

#### Blackboard Setters (ActionMethods)

| Method | Sets |
|---|---|
| `BB_SetInt` | int |
| `BB_SetFloat` | float |
| `BB_SetBool` | bool |
| `BB_SetVector2` | Vector2 |
| `BB_SetVector3` | Vector3 |

#### Blackboard Logging (ActionMethods)

| Method | Logs |
|---|---|
| `BB_LogInt`, `BB_LogFloat`, `BB_LogBool`, `BB_LogVector2` | Debug.Log(variable) |

### Baking Pipeline

```
TreeBaker.BakeTree(RootNode, authoringAsset, 
    out NodeData[], out FieldData[], out boxedConstants, out GUID[], out maxDepth)
    → RuntimeBehaviourTreeAsset
```

1. Flatten the authoring tree depth-first into `NodeData[]`
2. For each node, instantiate its `NodeMethod`, call `DeserializeFields()`
3. `ResolveSlotOffset(varIndex, runtimeBBDef)` — converts variable index to storage slot offset
4. For stride > 1: packs `FieldData.FromVariable(slotOffset)` + `FieldData.FromConstant(stride)`
5. Merges self BB def + commander BB def into merged runtime BB def
6. Commander BB def variables preserved with original stride; self BB def with stride=1

### RuntimeBehaviourTreeAsset

```
ScriptableObject
└── RuntimeBehaviourTreeAsset
    ├── sourceTree: BaseEditorTreeAsset          — back-reference
    ├── blackboardDefinition: BlackboardDefinition  — merged (self + commander)
    ├── runtimeNodeData: NodeData[]
    ├── runtimeFieldData: FieldData[]
    ├── boxedConstants: object[]
    ├── runtimeNodeGuids: string[]
    └── maxTreeDepth: int
```

### Compare Operation Enums

| Enum | Values |
|---|---|
| `NumericCompareOp` | Equal, NotEqual, Less, LessOrEqual, Greater, GreaterOrEqual |
| `BoolCompareOp` | Equal, NotEqual |
| `VectorCompareOp` | Equal, NotEqual, MagnitudeLess/Greater/LessOrEqual/GreaterOrEqual |
| `ObjectCompareOp` | Equal, NotEqual |
| `ObjectCheckOp` | IsNull, IsNotNull, IsActive, IsInactive |
| `BoolCheckOp` | IsTrue, IsFalse |
| `VectorCheckOp` | IsZero, IsNotZero |

### Commander Types

```
TacticalRole enum       — NONE, SCOUT, SUPPRESSOR, FLANKER, ARTILLERY, ASSAULT, COVER
FormationType enum      — LINE, WEDGE, COLUMN, SCATTERED, BOX
TacticPhase enum        — READY, MOVING, HOLD, ENGAGE
```

### Tracked Bindings

```
[Serializable]
TrackedBinding
    ├── componentType: string
    ├── memberName: string
    ├── blackboardVariableName: string
    └── blackboardVariableType: string

[Serializable]
TrackedBindingGroup
    ├── treeAssetGuid: string
    └── bindings: List<TrackedBinding>
```

### FieldReader (ref struct)

```
ref struct FieldReader
    ├── constructor(ReadOnlySpan<FieldData>, ReadOnlySpan<object> boxed, BlackBoard bb, FieldBinding)
    ├── ReadInt(FieldBinding binding)
    ├── ReadFloat()
    ├── ReadBool()
    ├── ReadVector2()
    ├── ReadVector3()
    ├── ReadObject()
    └── ReadEnum<T>()
```

Zero-allocation reader: resolves `SharedVar` fields to BB values, `SharedArray` to BB values with `elementIndex` offset.

---

## Assembly: BehaviourTree.Editor

**File count:** 35 `.cs`  
**Role:** Editor window, graph view, node views, inspectors, search providers, popups, serialization.

### Tree Assets (Editor)

```
BaseEditorTreeAsset (abstract) : BehaviourTreeAssetBase
    ├── nodesList: List<BehaviourNode>
    ├── editorNotes: List<EditorNoteData>
    ├── Initialize()               — instantiate sub-asset nodes
    ├── CreateNode(Type) → BehaviourNode
    ├── RegisterNode(BehaviourNode)
    ├── DeleteNode(BehaviourNode)
    ├── AddChild/RemoveChild
    ├── SyncNodesListFromAssets()  — rebuild from sub-assets on disk
    └── CreateBlackBoard()         — abstract

    ├── AgentTreeAsset : BaseEditorTreeAsset
    │   └── CreateBlackBoard() → BlackboardDefinition sub-asset

    └── CommanderTreeAsset : BaseEditorTreeAsset  — [CreateAssetMenu]
        └── CreateBlackBoard() → CommanderBlackboardDefinition sub-asset
```

### Editor Window

```
EditorWindow
└── BehaviourTreeEditor
    ├── [MenuItem] Open Behaviour Tree Graph
    ├── [OnOpenAsset] double-click .asset
    ├── treeGraphView: BehaviourTreeEditorGraphView
    ├── inspectorView: InspectorView
    ├── blackBoardView: BlackBoardView
    ├── trackedVariablesView: TrackedVariablesView
    ├── squadTabView: SquadTabView
    ├── commanderTabView: CommanderTabView (Phase 8 — planned)
    ├── assetBarMenu: ToolbarMenu
    ├── tabView: TabView (Shared, Tracked, Squads, Commander)
    ├── ConfigureTabsForTreeType()    — shows/hides CommanderTab based on runner type
    ├── PollDebugState()              — play mode node state overlay
    └── BuildAssetBarMenu()           — Create New Tree, Open Tree, Bake, Save, Sync, Open Squad

└── SquadDefinitionEditor
    ├── [MenuItem] Open Squad Editor
    ├── [OnOpenAsset] double-click SquadDefinition
    ├── squadBlackBoardView: BlackBoardView
    ├── rolesList: VisualElement
    ├── bindingGroupsScroll: ScrollView
    ├── LoadSquad(SquadDefinition)
    └── Editable binding groups with PopupField<string> dropdowns
```

### Graph View

```
GraphView
└── BehaviourTreeEditorGraphView (partial)  — [UxmlElement("BTGraphView")]
    ├── PopulateView(BaseEditorTreeAsset)   — creates node views, connects edges
    ├── RefreshDebugVisuals(RunnerBase)     — play mode node state overlay
    ├── DebugProxiesAreSetup: bool
    ├── ClearRuntimeDebugProxies()
    ├── Node creation via NodeSearchProvider
    ├── Copy/Paste via CopyPasteHandler
    ├── Subtree extraction via SubtreeExtractor
    └── Context menu: Delete, Duplicate, Create Subtree

Node (GraphView)
└── BehaviourNodeView
    ├── Title label
    ├── Subtitle (method name)
    ├── Status border (RUNNING=green, FAILURE=red, SUCCESS=blue)
    ├── Abort type icon
    ├── NodeWarning icon (missing fields, unassigned vars)
    ├── Input/output ports
    └── Selection + drag support

Port (GraphView)
└── BehaviourPort            — CSS class toggling for connection state, direction coloring
```

### Visual Elements

```
VisualElement
├── InspectorView (partial)        — [UxmlElement] Node inspector panel
├── BlackBoardView (partial)       — [UxmlElement] Variable list builder
├── TrackedVariablesView (partial) — [UxmlElement] Tracked bindings UI
├── SquadTabView (partial)         — [UxmlElement] Squad connections tab
└── CommanderTabView (partial)     — [UxmlElement] Commander tab (Phase 8 — planned)

TwoPaneSplitView
└── SplitView (partial)            — [UxmlElement] Thin wrapper for UXML compatibility
```

### Search Providers (ISearchWindowProvider)

| Provider | Lists |
|---|---|
| `NodeSearchProvider` | Available method types for node creation (Actions, Conditions, Decorators, Composites) |
| `TreeSearchProvider` | All `AgentTreeAsset` and `CommanderTreeAsset` in project |
| `TreeAssetSearchProvider` | Trees available for squad binding (excludes already-bound) |
| `SquadSearchProvider` | All `SquadDefinition` assets |
| `ComponentMemberSearchProvider` | GameObject → Component → public field/property (for tracked bindings) |

### Popups

| Popup | Type | Purpose |
|---|---|---|
| `VariableTypeSearchPopup` | `PopupWindowContent` | Type picker for new BB variables (singular/array, size) |
| `VariableSearchPopup` | `PopupWindowContent` | Existing BB variable picker (filter by type) |

### Custom Inspectors

| Inspector | Target | Purpose |
|---|---|---|
| `CustomNodeEditor` | `[CustomEditor(BehaviourNode, true)]` | Generic node inspector: name, method, fields, children, abort type, comment |
| `SubtreeNodeEditor` | `[CustomEditor(SubtreeNode)]` | Subtree picker + cycle validation + binding editor |
| `BlackBoardEditor` | `[CustomEditor(BlackBoard)]` | Reference/value-type override management, layout-change rebuild |

### Editor Infrastructure

| Class | Type | Purpose |
|---|---|---|
| `BehaviourTreeEditorPaths` | static class | Centralized string constants for all UXML/USS paths |
| `CopyPasteHandler` | class | Clipboard: SerializedNodeData snapshot → json → paste with offset |
| `SubtreeExtractor` | class | Extract selected nodes into new subtree asset |
| `SubtreeCycleValidator` | static class | WouldCreateCycle(): detects circular references, memoized |
| `NodeWarningEvaluator` | static class | Evaluates warnings: missing abort conditions, unassigned vars, missing BB |
| `RuntimeDebugManager` | class | Debug proxy: highlights running nodes, expands subtrees in debug |
| `BTreeAssetRenameHandler` | AssetModificationProcessor | Auto-renames BB def sub-asset on tree rename |
| `MethodMetadataCache` | static class | Editor cache of ParamInfo per method, invalidated on registry rebuild |
| `MethodMetadataCache.ParamInfo` | class | fieldName, fieldType, isVariable, isArray, isToggleVariable, index |
| `VariableTypeRegistry` | static class | Type → factory + binder registry for BB variable editing |
| `BlackboardEnumGenerator` | static class | `[MenuItem]` Scans BB defs, generates `BlackboardEnums.cs` per def |
| `TooltipRegistry` | ScriptableObject | Stores tooltip data (NodeTooltipData) per method |
| `EditorNoteData` | `[Serializable]` class | position, size, title, contents, colors for graph notes |

---

## Assembly: BehaviourTree.Editor.Tests

**File count:** 3 `.cs`  
**Role:** Editor-mode NUnit tests.

| Test Fixture | Tests |
|---|---|
| `SquadInstanceTests` | 15 tests: Initialize, EnsureResolved, CopyToBB, CopyFromBB, direction filtering, Commander→Squad→Agent pipeline |
| `GraphNoteCoordinateConversionTests` | Coordinate math for placing GraphNote at mouse position |
| `BlackboardVariableSerializationTests` | `DeleteArrayElementAtIndex` on `[SerializeReference]` lists — no null holes |

---

## Assembly: BehaviourTree.Runtime.Tests

**File count:** 1 `.cs`  
**Role:** Play-mode NUnit tests.

| Test Fixture | Tests |
|---|---|
| `BlackboardGameObjectIntegrationTests` | E2E: BB def → BlackBoard init → serializedRefs → DeserializeFields → ResolveInputs → Execute pipeline for GameObject vars |

---

## Assembly: BehaviourTree.Utility

**File count:** 1 `.cs`  
**Role:** Low-level memory utilities.

| Class | Method |
|---|---|
| `ByteHelper` | `StructToBytes<T>()`, `ByteArrayToFixedBuffer()` — DOTS interop |

---

## UI Documents (UXML)

All in `Assets/Scripts/BehaviourTree/Editor/UIDocuments/`:

| File | Element | Purpose |
|---|---|---|
| `BehaviourTreeEditor.uxml` | Main window | Toolbar, SplitView, TabView (Shared/Tracked/Squads/Commander) |
| `GraphNodeView.uxml` | Node card | Title, subtitle, abort icon, warning icon, ports |
| `GraphNote.uxml` | Note | Movable/resizable note: title, contents, color |
| `BehaviourPort.uxml` | Port | Custom input/output port |
| `BlackboardVariableEntry.uxml` | Variable row | Per-variable list item in BlackBoardView |
| `ArrayElementRow.uxml` | Array element | Per-element row for stride>1 arrays |
| `VariableTypeSearchPopup.uxml` | Popup | Type picker: search, singular/array radio, size, list |
| `TrackedVariablesView.uxml` | Panel | Tracked bindings: list, add/remove, scene picker |
| `TrackedBindingRow.uxml` | Row | Single tracked binding row template |
| `SquadDefinitionEditor.uxml` | Window | Squad editor: toolbar, BB view, roles, bindings |
| `SquadTabView.uxml` | Tab | Squad connections tab: add/remove, binding tables |

## Stylesheets (USS)

All in `Assets/Scripts/BehaviourTree/Editor/UIDocuments/`:

| File | Targets |
|---|---|
| `BehaviourTreeEditor.uss` | Toolbar, tabs, panels, scroll views, inspector |
| `GraphNodeViewStyle.uss` | Node cards, type colors, selection, RUNNING state |
| `GraphNote.uss` | Note header, text areas, color pickers |
| `BehaviourPort.uss` | Port connection-state coloring |
| `BlackboardVariableEntry.uss` | Variable row styling |
| `ArrayElementRow.uss` | Array element row styling |
| `VariableTypeSearchPopup.uss` | Popup layout |
| `TrackedVariablesView.uss` | Tracked bindings panel |
| `TrackedBindingRow.uss` | Tracked binding row |

---

## Key Concepts

### Stride-Aware Per-Agent Storage

Commander BB variables marked `isSquadData` have `stride = agentCount`. Storage allocates `stride` consecutive `object[]` slots per variable. The `GetVariableSlotRange(varIndex)` method returns `(baseSlot, stride)`. Reads/writes use `baseSlot + agentOffset` to access a specific agent's data.

### Baked Slot Offsets vs Variable Indices

`TreeBaker.ResolveSlotOffset(variableIndex, bbDef)` converts a definition-level variable index into a storage slot offset, accounting for stride. `FieldBinding.bbSlotIndex` stores the **slot offset**, not the variable index. This is a flat-array optimization: zero indirection at runtime.

### Merged Agent Blackboard

Each agent's runtime BB definition is a merge of:
- Agent's own `BlackboardDefinition` (stride=1)
- Commander's `BlackboardDefinition` (stride=1 copies of commander vars)

The agent sees only its own single slot for each commander variable. The `CommanderBindingBridge` copies commander BB slots (with `agentID` offset) into agent BB slots each frame.

### Commander → Squad → Agent Pipeline

```
CommanderTreeRunner.Update():
    1. Bridge copies commander BB → agent merged BB (per-agent, agentID offset)
    2. Agent.Evaluate() — agent reads merged BB
    3. Bridge copies agent merged BB → commander BB (per-agent, agentID offset)
    4. SquadInstance copies commander BB ↔ squad BB
    5. Commander.Evaluate() — commander reads/writes updated squad data
```

### BB Override System

Per-component overrides allow instance-level edits on the `BlackBoard` MonoBehaviour that diverge from the shared `BlackboardDefinition`:

- **Reference types** (GameObject, Transform): Stored in `serializedReferences` List. Override tracked in `overriddenReferenceSlots` List.
- **Value types** (int, float, bool, Vector*, Color, enum): Stored in `valueOverrides` List of `BlackboardValueOverride`. Keyed by `variableName` + `elementIndex`.

### Static Constructor Initialization

Both `TickDispatcher` and `MethodRegistry` use static constructors. This means they fire on first type access — no explicit `Initialize()` call needed in test contexts or early runtime setup.

---

## File Count Summary

| Assembly | .cs | .asmdef | .uxml | .uss | Key Types |
|---|---|---|---|---|---|
| Core | 32 | 1 | — | — | 4 interfaces, 5 enums, 3 structs, 12 classes |
| Runtime | 33 | 1 | — | — | 7 enums, 1 interface, 3 structs, 18 classes |
| Editor | 35 | 1 | 11 | 9 | 4 EditorWindow, 1 GraphView, 5 SearchProviders, 2 Popups, 3 CustomEditors |
| Editor.Tests | 3 | 1 | — | — | 3 test fixtures |
| Runtime.Tests | 1 | 1 | — | — | 1 test fixture |
| Utility | 1 | 1 | — | — | 1 static class |
| **Total** | **105** | **6** | **11** | **9** | |
