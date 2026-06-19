# Implementation Plan: Agent/Commander Split

## Phase 1 — Asset Hierarchy: Extract `BaseEditorTreeAsset`

### Current

```
BehaviourTreeAssetBase (Core)
├── root, blackboardDefinition, commanderBlackboardDefinition
│
├── BehaviourTreeAsset (Editor)        ← agent tree
│   ├── nodesList, editorNotes
│   ├── Initialize(), CreateBlackBoard(), CreateNode(), RegisterNode(), ...
│   │
│   └── CommanderTreeAsset (Editor)    ← inherits from agent → semantically wrong
│       └── CreateCommanderBlackboard()
```

### Target

```
BehaviourTreeAssetBase (Core)                                        ← unchanged
├── root, blackboardDefinition, commanderBlackboardDefinition
│
├── BaseEditorTreeAsset (Editor, abstract)                           ← NEW
│   ├── nodesList, editorNotes
│   ├── Initialize(), CreateNode(), RegisterNode(), DeleteNode(), AddChild(), RemoveChild()
│   ├── SyncNodesListFromAssets(), NeedsNodesListSync(), ClearNodes()
│   ├── abstract void CreateBlackBoard()  (or virtual with default)
│   │
│   ├── AgentTreeAsset (Editor)                                      ← renamed from BehaviourTreeAsset
│   │   └── override CreateBlackBoard() → BlackboardDefinition
│   │
│   └── CommanderTreeAsset (Editor)                                  ← now sibling of Agent
│       └── override CreateBlackBoard() → CommanderBlackboardDefinition
```

### Tasks

1. Create `BaseEditorTreeAsset.cs` in `Assets/Scripts/BehaviourTree/Editor/`
   - `abstract class BaseEditorTreeAsset : BehaviourTreeAssetBase`
   - Move `nodesList`, `editorNotes` fields
   - Move all node management methods: `Initialize()`, `CreateNode()`, `RegisterNode()`, `DeleteNode()`, `AddChild()`, `RemoveChild()`, `ClearNodes()`, `SyncNodesListFromAssets()`, `NeedsNodesListSync()`
   - Add `public abstract void CreateBlackBoard();`

2. Rename `BehaviourTreeAsset` → `AgentTreeAsset` in `BehaviourTreeAsset.cs`
   - Change base class from `BehaviourTreeAssetBase` to `BaseEditorTreeAsset`
   - Remove fields + methods that are now on the base
   - Keep `override CreateBlackBoard()` (creates `BlackboardDefinition`)

3. Update `CommanderTreeAsset.cs`
   - Change `: BehaviourTreeAsset` → `: BaseEditorTreeAsset`
   - Change `CreateCommanderBlackboard()` → `override CreateBlackBoard()` (creates `CommanderBlackboardDefinition`)

4. Update all code references (see Rename Impact Inventory below)
   - Replace `BehaviourTreeAsset` with `AgentTreeAsset` or `BaseEditorTreeAsset` where appropriate
   - Replace `t:BehaviourTreeAsset` with `t:AgentTreeAsset t:CommanderTreeAsset` in `AssetDatabase.FindAssets` calls
   - Replace `CreateInstance<BehaviourTreeAsset>()` with `CreateInstance<AgentTreeAsset>()`

5. Update `CommanderTreeRunner.cs`
   - Change `List<TreeRunner>` → `List<AgentTreeRunner>` for `registeredAgents`

---

## Phase 2 — Runner Hierarchy: Extract `BehaviourTreeRunnerBase`

### Current

```
MonoBehaviour
├── TreeRunner                    ← agent, standalone
└── CommanderTreeRunner           ← commander, standalone
```
Both duplicate: `blackBoard`, `runtimeAsset`, `evaluator`, `debugProvider`, `initialized`, `GetOrBake()`, `OnDestroy()`, `OnDisable()`, `OnValidate()`, `GetSourceTree()`.

### Target

```
MonoBehaviour
└── BehaviourTreeRunnerBase (abstract)                               ← NEW
    ├── blackBoard, runtimeAsset, authoringAsset (#if EDITOR)
    ├── evaluator, debugProvider, initialized
    ├── public virtual void Initialize()    — bake, init BB, create evaluator, OnPostInitialize
    ├── protected virtual void OnPostInitialize()  — subclass hook
    ├── public void Evaluate()              — evaluator.Evaluate(blackBoard)
    ├── protected virtual void OnDestroy/Destroy/OnValidate()
    ├── public BehaviourTreeAssetBase GetSourceTree()
    │
    ├── AgentTreeRunner                                              ← renamed from TreeRunner
    │   ├── runIndependently, trackedBindingGroups, dataProviders
    │   ├── override OnPostInitialize(), Update()
    │   ├── PushDataProviders(), ResolveTrackedBindings(), PushTrackedBindings()
    │   │
    │   └── CommanderTreeRunner                                      ← sibling
    │       ├── registeredAgents, cachedBridges
    │       ├── override OnPostInitialize(), Update()
    │       ├── TickAgents(), EvaluateCommander()
```

Wait — `CommanderTreeRunner` has `registeredAgents: List<AgentTreeRunner>`. And `CommanderTreeRunner` itself IS an `AgentTreeRunner` in this hierarchy? No, that's wrong. Commander should inherit from `BehaviourTreeRunnerBase` directly, not from `AgentTreeRunner`.

### Corrected Target

```
MonoBehaviour
└── BehaviourTreeRunnerBase (abstract)
    ├── AgentTreeRunner : BehaviourTreeRunnerBase
    └── CommanderTreeRunner : BehaviourTreeRunnerBase
```

### Tasks

1. Create `BehaviourTreeRunnerBase.cs` in `Assets/Scripts/BehaviourTree/Runtime/`
   - `public abstract class BehaviourTreeRunnerBase : MonoBehaviour`
   - Shared fields: `blackBoard`, `runtimeAsset`, `authoringAsset` (#if EDITOR), `evaluator`, `debugProvider`, `initialized`
   - `public virtual void Initialize()` — Template Method:
     ```csharp
     if (initialized) return;
     runtimeAsset = RuntimeAssetHelper.GetOrBake(runtimeAsset, authoringAsset, GetType().Name);
     if (runtimeAsset == null) return;
     blackBoard.Initialize(runtimeAsset.blackboardDefinition);
     evaluator = new TreeEvaluator(...);
     debugProvider = new RuntimeDebugProvider(runtimeAsset);
     OnPostInitialize();
     initialized = true;
     ```
   - `protected virtual void OnPostInitialize() { }` — hook
   - `public void Evaluate()` — `evaluator?.Evaluate(blackBoard)`
   - `public BehaviourTreeAssetBase GetSourceTree()` — returns `authoringAsset`
   - `protected virtual void OnDestroy()` — destroys `runtimeAsset`
   - `protected virtual void OnDisable() { }`
   - `protected virtual void OnValidate()` — `blackBoard?.BuildSerializedReferences(...)`

2. Rename `TreeRunner` → `AgentTreeRunner` in `TreeRunner.cs`
   - Change base: `MonoBehaviour` → `BehaviourTreeRunnerBase`
   - Remove duplicated fields (now on base)
   - Remove duplicated `Initialize()` — override with `public override void Initialize()` that calls `base.Initialize()`
   - Override `OnPostInitialize()` — collect data providers, resolve tracked bindings
   - Override `OnDestroy()` — calls `base.OnDestroy()`
   - Keep agent-specific: `runIndependently`, `trackedBindingGroups`, `dataProviders`, `PushDataProviders()`, `ResolveTrackedBindings()`, `PushTrackedBindings()`
   - `Update()` stays but uses `base.Evaluate()` instead of direct evaluator call

3. Update `CommanderTreeRunner.cs`
   - Change base: `MonoBehaviour` → `BehaviourTreeRunnerBase`
   - Remove duplicated fields (now on base)
   - Remove duplicated `Initialize()` → override with `public override void Initialize()`
   - Override `OnPostInitialize()` — agent init + bridge resolution
   - Override `OnDestroy()` — calls `base.OnDestroy()`
   - `Update()` stays but uses `base.Evaluate()`

4. Update all code references (see Rename Impact Inventory below)

---

## Phase 3 — Editor Split (Conditional Tabs + Node Palette)

### Tab Visibility

Current UXML has 4 tabs always visible: `SharedVariablesTab`, `TrackedVariablesTab`, `SquadsTab`, `CommanderTab`.

**Target:**
| Tab | Agent Editor | Commander Editor |
|---|---|---|
| Shared | visible | visible |
| Tracked | visible | visible |
| Squads | visible | visible |
| Commander | hidden | visible |

Only the Commander tab is exclusive — hidden on agent trees, visible on commander trees. All other tabs (Shared, Tracked, Squads) are visible on both.

**Implementation (Option A — conditional in one editor):**

In `BehaviourTreeEditor.CreateGUI()` or `OnSelectionChange()`, after `currentTree` is set:

```csharp
void ConfigureTabsForTreeType()
{
    if (tabView == null) return;

    Tab commanderTab = tabView.Q<Tab>("CommanderTab");

    if (currentTree is CommanderTreeAsset)
    {
        commanderTab.style.display = DisplayStyle.Flex;
    }
    else
    {
        commanderTab.style.display = DisplayStyle.None;
    }
}
```

Or use `style.display = DisplayStyle.None` to hide without losing the reference for switching later.

### Node Palette

**Current:** `NodeSearchProvider.CreateSearchTree()` builds a flat list of ALL methods from `MethodRegistry`. No filtering by tree type.

**Target:** Commander tree should NOT show agent-implementation nodes. Commander tree should show squad/commander-specific nodes.

**Implementation:**

Option A (simplest, since no commander-specific methods exist yet): Add a filter parameter to `NodeSearchProvider`.

```csharp
// NodeSearchProvider — add:
public BaseEditorTreeAsset currentTreeAsset;  // set before search window opens

// In CreateSearchTree(), after building method lists, filter:
private void BuildMethodLists(BaseEditorTreeAsset treeAsset)
{
    // ... existing logic ...
    // Filter: remove agent-only methods when in commander tree
    if (treeAsset is CommanderTreeAsset)
    {
        actionMethods.RemoveAll(m => IsAgentOnlyMethod(m));
        conditionMethods.RemoveAll(m => IsAgentOnlyMethod(m));
    }
    // Filter: remove commander-only methods when in agent tree
    else
    {
        actionMethods.RemoveAll(m => IsCommanderOnlyMethod(m));
        conditionMethods.RemoveAll(m => IsCommanderOnlyMethod(m));
    }
}
```

Where `IsAgentOnlyMethod` / `IsCommanderOnlyMethod` could check a custom attribute like `[TreeType(AgentOnly)]` or `[TreeType(CommanderOnly)]` on the NodeMethod subclass. For now, with no such attributes, this is a no-op. It lays the groundwork.

**Where to set `currentTreeAsset`:** In `BehaviourTreeEditorGraphView` which already has the `tree` reference:

```csharp
// When opening search window:
nodeSearchProvider.currentTreeAsset = tree;
SearchWindow.Open(new SearchWindowContext(screenPos), nodeSearchProvider);
```

---

## Rename Impact Inventory

### `BehaviourTreeAsset` → `AgentTreeAsset` / `BaseEditorTreeAsset`

#### Script files that compile-reference the type:

| File | Line(s) | Current Usage | New Usage |
|---|---|---|---|
| `BehaviourTreeEditorGraphView.cs` | 20 | `BehaviourTreeAsset tree` field | `BaseEditorTreeAsset tree` |
| `BehaviourTreeEditorGraphView.cs` | 581 | `PopulateView(BehaviourTreeAsset)` param | `PopulateView(BaseEditorTreeAsset)` |
| `BehaviourTreeEditorGraphView.cs` | 623 | `InitTree(BehaviourTreeAsset)` param | `InitTree(BaseEditorTreeAsset)` |
| `BehaviourTreeEditor.cs` | 27 | `public static BehaviourTreeAsset currentTree` | `public static BaseEditorTreeAsset currentTree` |
| `BehaviourTreeEditor.cs` | 42 | `Selection.activeObject is BehaviourTreeAsset` | `is AgentTreeAsset or CommanderTreeAsset` or `is BaseEditorTreeAsset` |
| `BehaviourTreeEditor.cs` | 162 | `FindAssets("t:BehaviourTreeAsset")` | `FindAssets("t:AgentTreeAsset t:CommanderTreeAsset")` |
| `BehaviourTreeEditor.cs` | 168,181 | cast to `BehaviourTreeAsset` | cast to `BaseEditorTreeAsset` |
| `BehaviourTreeEditor.cs` | 213 | `CreateInstance<BehaviourTreeAsset>()` | `CreateInstance<AgentTreeAsset>()` |
| `BehaviourTreeEditor.cs` | 239 | return type `BehaviourTreeAsset` | return type `BaseEditorTreeAsset` |
| `BehaviourTreeEditor.cs` | 248 | `as BehaviourTreeAsset` cast | `as BaseEditorTreeAsset` cast |
| `BehaviourTreeEditor.cs` | 253 | `as BehaviourTreeAsset` cast | `as BaseEditorTreeAsset` cast |
| `BehaviourTreeEditor.cs` | 272 | `CreateInstance<RuntimeBehaviourTreeAsset>` | unchanged |
| `BehaviourTreeEditor.cs` | 295 | local `BehaviourTreeAsset` var | `BaseEditorTreeAsset` var |
| `TreeSearchProvider.cs` | 24 | `FindAssets("t:BehaviourTreeAsset")` | `FindAssets("t:AgentTreeAsset t:CommanderTreeAsset")` |
| `TreeSearchProvider.cs` | 39,55 | cast to `BehaviourTreeAsset` | cast to `BaseEditorTreeAsset` |
| `CopyPasteHandler.cs` | 86 | `PasteNodes(BehaviourTreeAsset)` param | `PasteNodes(BaseEditorTreeAsset)` |
| `CopyPasteHandler.cs` | 173 | `CreateNodeDataFromSerialized(..., BehaviourTreeAsset)` | `BaseEditorTreeAsset` |
| `SubtreeExtractor.cs` | 20 | `CanExtract(..., BehaviourTreeAsset)` | `BaseEditorTreeAsset` |
| `SubtreeExtractor.cs` | 36 | `Extract(..., BehaviourTreeAsset, Action<BehaviourTreeAsset>, ...)` | `BaseEditorTreeAsset` |
| `SubtreeExtractor.cs` | 52 | string literal `"BehaviourTreeAsset"` | `"AgentTreeAsset"` or parameterize |
| `SubtreeExtractor.cs` | 59 | `CreateInstance<BehaviourTreeAsset>()` | `CreateInstance<AgentTreeAsset>()` |
| `SubtreeExtractor.cs` | 165,187 | params `BehaviourTreeAsset` | `BaseEditorTreeAsset` |
| `SubtreeExtractor.cs` | 202-203,241 | params `BehaviourTreeAsset` | `BaseEditorTreeAsset` |
| `SubtreeNodeEditor.cs` | 50 | `typeof(BehaviourTreeAsset)` | `typeof(AgentTreeAsset)` |
| `SubtreeNodeEditor.cs` | 66 | cast `as BehaviourTreeAsset` | `as BaseEditorTreeAsset` |
| `SubtreeNodeEditor.cs` | 235 | string `"BehaviourTreeAsset"` | `"AgentTreeAsset"` |
| `SubtreeNodeEditor.cs` | 238 | `CreateInstance<BehaviourTreeAsset>()` | `CreateInstance<AgentTreeAsset>()` |
| `RuntimeDebugManager.cs` | 24 | `Dictionary<BehaviourTreeAsset, ...>` | `Dictionary<AgentTreeAsset, ...>` or `BaseEditorTreeAsset` |
| `RuntimeDebugManager.cs` | 267 | local casting/usage | update |
| `RuntimeDebugManager.cs` | 320 | `authoring is not BehaviourTreeAsset` | `authoring is not BaseEditorTreeAsset` |
| `BTreeAssetRenameHandler.cs` | 10 | `is BehaviourTreeAsset` | `is AgentTreeAsset` |
| `SubtreeCycleValidator.cs` | 10 | `BehaviourTreeAsset` param | `BaseEditorTreeAsset` param |
| `SubtreeCycleValidator.cs` | 30 | `is not BehaviourTreeAsset` | `is not BaseEditorTreeAsset` |
| `TrackedVariablesView.cs` | 111,140 | already uses `BehaviourTreeAssetBase` | unchanged |
| `CommanderTreeAsset.cs` | 14 | `: BehaviourTreeAsset` | `: BaseEditorTreeAsset` (also method rename) |

#### Serialized `.asset` files (YAML):

| File | Class Identifier |
|---|---|
| `NewSubtree22.asset` | `BehaviourTree.Editor.BehaviourTreeAsset` |
| `NewTree.asset` | `BehaviourTree.Editor.BehaviourTreeAsset` |
| `NewTreeB.asset` | `BehaviourTree.Editor.BehaviourTreeAsset` |
| `TESTSUBTREE.asset` | `BehaviourTree.Editor.BehaviourTreeAsset` |
| `TESTCOPYSUBTREE.asset` | `BehaviourTree.Editor.BehaviourTreeAsset` |

**Risk assessment:** The `.asset` files use Unity's script GUID + fileID to resolve the type, not the class name string. The `m_EditorClassIdentifier` line is metadata. As long as the `.cs` file GUID doesn't change (i.e., we rename the class in the same file, not create a new file), Unity will successfully resolve `AgentTreeAsset` from the same script asset. **No migration needed for existing assets.**

However, the `m_EditorClassIdentifier` comment will be stale — Unity auto-updates it on the next `AssetDatabase.SaveAssets()`.

#### Serialized `.asset` files — `CommanderTreeAsset` change:

`CommanderTreeAsset` currently inherits from `BehaviourTreeAsset`. After refactor, it inherits from `BaseEditorTreeAsset` (new file). No existing commander tree `.asset` files exist, so no migration needed.

### `TreeRunner` → `AgentTreeRunner`

#### Script files:

| File | Current Usage | New Usage |
|---|---|---|
| `CommanderTreeRunner.cs` | `List<TreeRunner>` field | `List<AgentTreeRunner>` |
| `BehaviourTreeEditor.cs` | `static TreeRunner currentRunner` | `static AgentTreeRunner currentRunner` |
| `BehaviourTreeEditor.cs` | `OnSelectTree`: `TryGetComponent(out TreeRunner)` | `out AgentTreeRunner` |
| `TrackedVariablesView.cs` | references `TreeRunner` | `AgentTreeRunner` |
| `RuntimeDebugManager.cs` | references `TreeRunner` | `AgentTreeRunner` |
| `BehaviourTreeEditorGraphView.cs` | debug proxy → `TreeRunner` | `AgentTreeRunner` |
| `BlackBoardEditor.cs` | references `TreeRunner` | `AgentTreeRunner` |
| `CommanderBindingBridge.cs` | references `TreeRunner` | `AgentTreeRunner` |
| `RuntimeAssetHelper.cs` | log context string "TreeRunner" | "AgentTreeRunner" |
| `TrackedBinding.cs` | `BehaviourTreeAssetBase targetTree` | unchanged (already base type) |

#### Serialized scenes/prefabs:

| File | Component |
|---|---|
| `SampleScene.unity` | GameObject with `TreeRunner` component |
| `Commander.prefab` | GameObject with `CommanderTreeRunner` component |

**Risk:** Same as assets — script GUID stays the same (rename in same file). Unity resolves by GUID. The component name in the YAML is metadata. No migration needed.

However, `Commander.prefab` has `registeredAgents` referencing `TreeRunner` instances. After rename, those instances will be `AgentTreeRunner`. The serialized reference uses Unity's instance IDs (local file ID + GUID), not the class name, so this survives.

---

## Phase 4 — Verifying & Cleaning Up

### After all phases:

1. **Compile check** — verify all assemblies build without errors
2. **Open existing trees** — verify `NewTree.asset`, `TESTSUBTREE.asset`, etc. open correctly in the editor
3. **Run play mode** — verify `SampleScene.unity` runs without errors, agents evaluate, commander orchestrates
4. **Create new agent tree** — verify `CreateNewTree` creates an `AgentTreeAsset`
5. **Create new commander tree** — verify `[CreateAssetMenu]` on `CommanderTreeAsset` works
6. **Verify tabs** — agent tree shows Shared + Tracked + Squads; commander tree shows Shared + Tracked + Squads + Commander
7. **Verify node palette** — commander tree node search shows correct nodes (when commander-specific nodes are added later)
8. **Run existing tests** — verify `BehaviourTree.Editor.Tests` assembly still passes

### Things NOT changing in this plan:

- `RuntimeBehaviourTreeAsset` — unchanged, no flag added (per user decision)
- `BehaviourTreeAssetBase` — unchanged
- `TreeBaker`, `TreeEvaluator`, `RuntimeAssetHelper` — unchanged
- `NodeSearchProvider` filtering logic — groundwork laid but no methods are currently agent/commander-specific, so filtering is a no-op for now
- `BehaviourTreeEditorPaths` — unchanged

---

## Order of Execution

```
Phase 1 (Assets):
  1. Create BaseEditorTreeAsset.cs
  2. Rename BehaviourTreeAsset → AgentTreeAsset
  3. Update CommanderTreeAsset.cs
  4. Update all code references

Phase 2 (Runners):
  5. Create BehaviourTreeRunnerBase.cs
  6. Rename TreeRunner → AgentTreeRunner
  7. Update CommanderTreeRunner.cs
  8. Update all code references

Phase 3 (Editor):
  9. Add tab visibility logic to BehaviourTreeEditor
  10. Lay groundwork for node palette filtering in NodeSearchProvider
  11. Wire up currentTreeAsset in graph view

Phase 4 (Verify):
  12. Compile, open existing assets, run play mode, run tests
```
