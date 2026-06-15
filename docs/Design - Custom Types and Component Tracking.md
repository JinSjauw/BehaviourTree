# Custom Type Editors & Component Variable Tracking

## 1. CustomValueFieldAttribute — Multi-Field Height

`GetValueFieldHeight` in [BlackBoardVariableDrawer.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Editor/BlackBoardVariableDrawer.cs#L162-L171) hardcodes which types need 2 lines. Custom structs have no way to declare their line height.

### Attribute

```csharp
[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class)]
public class CustomValueFieldAttribute : Attribute
{
    public int LineCount { get; }
    public CustomValueFieldAttribute(int lineCount) => LineCount = lineCount;
}
```

Usage: `[CustomValueField(LineCount = 3)]` on any struct/class. Updated `GetValueFieldHeight` checks `type.GetCustomAttribute<CustomValueFieldAttribute>()` before the built-in type chain. Zero allocation — `GetCustomAttribute` on `Type` is cached by the runtime.

---

## 2. IBlackboardValueDrawer — Inline Custom Type Editing

`DrawValueField` in [BlackBoardVariableDrawer.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Editor/BlackBoardVariableDrawer.cs#L173-L206) is a hardcoded if/else chain. Custom types get a read-only label `(type: X)`.

Node Canvas solves this with an `ObjectDrawer<T>` registry that takes a raw value and returns a modified copy — no `SerializedProperty` indirection.

### Interface + Attribute

```csharp
public interface IBlackboardValueDrawer
{
    object DrawValueGUI(Rect rect, string label, object currentValue);
    int LineCount { get; }
}

[AttributeUsage(AttributeTargets.Class)]
public class CustomBlackboardValueEditorAttribute : Attribute
{
    public Type TargetType { get; }
    public CustomBlackboardValueEditorAttribute(Type targetType) => TargetType = targetType;
}
```

### Registry (editor-only)

`BlackboardValueDrawerRegistry` scans assemblies for `IBlackboardValueDrawer` types marked with `[CustomBlackboardValueEditor(typeof(T))]` and builds a `Dictionary<Type, IBlackboardValueDrawer>` at startup.

### Example drawer

```csharp
[CustomBlackboardValueEditor(typeof(CombatStats))]
public class CombatStatsDrawer : IBlackboardValueDrawer
{
    public int LineCount => 3;
    public object DrawValueGUI(Rect rect, string label, object currentValue)
    {
        CombatStats s = currentValue is CombatStats cs ? cs : default;
        float lh = EditorGUIUtility.singleLineHeight;
        s.health = EditorGUI.FloatField(new Rect(rect.x, rect.y,           rect.width, lh), "Health", s.health);
        s.armor  = EditorGUI.FloatField(new Rect(rect.x, rect.y + lh + 2,  rect.width, lh), "Armor",  s.armor);
        s.speed  = EditorGUI.FloatField(new Rect(rect.x, rect.y + lh*2+4,  rect.width, lh), "Speed",  s.speed);
        return s;
    }
}
```

Updated `DrawValueField` fallback checks `BlackboardValueDrawerRegistry.TryGetDrawer()` before the read-only label.

---

## 3. Component Variable Tracking — [BlackboardTrack]

### The Goal

Bind a component field/property (e.g. `HealthComponent.health`) to a blackboard variable so it stays in sync every frame — without manual `IBlackboardDataProvider` boilerplate per component.

### How others do it

| | Behaviour Designer | Node Canvas | This codebase |
|---|---|---|---|
| Mechanism | `SharedVariable` SO refs + reflection property mapping | On-demand `agent.GetComponent<T>()` pull | `IBlackboardDataProvider.ProvideData()` |
| Update | `BehaviorManager.Update()` polls all active behaviors | Nodes read directly at evaluation time | `TreeRunner.Update()` → `PushDataProviders()` |
| Coupling | Component must hold `SharedVariable` field | Component knows nothing about BB | Component must implement interface |

### Proposal: [BlackboardTrack] attribute + AutoTrackProvider

A single `AutoTrackProvider` component scans all components for annotated fields and pushes values. User code is just an attribute:

```csharp
public class HealthComponent : MonoBehaviour
{
    [BlackboardTrack]               public float health;
    [BlackboardTrack("currentAmmo")] private int ammo;
}
```

### Attribute

```csharp
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class BlackboardTrackAttribute : Attribute
{
    public string VariableName { get; }
    public BlackboardTrackAttribute() { }
    public BlackboardTrackAttribute(string variableName) => VariableName = variableName;
}
```

### AutoTrackProvider (runtime)

```csharp
public class AutoTrackProvider : MonoBehaviour, IBlackboardDataProvider
{
    private struct TrackedMember
    {
        public FieldInfo fieldInfo;
        public PropertyInfo propInfo;
        public Component component;
        public int variableIndex; // resolved once in Awake
    }

    private List<TrackedMember> tracked = new List<TrackedMember>();
    private bool initialized;

    // ── Called once ──
    private void Awake()
    {
        Component[] components = GetComponentsInChildren<Component>(includeInactive: false);
        BlackBoard bb = GetComponent<BlackBoard>(); // resolve once
        BlackboardDefinition def = bb?.Definition;

        foreach (Component comp in components)
        {
            if (comp == null) continue;
            Type t = comp.GetType();

            foreach (FieldInfo fi in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                var attr = fi.GetCustomAttribute<BlackboardTrackAttribute>();
                if (attr == null) continue;
                string varName = attr.VariableName ?? fi.Name;
                int varIdx = ResolveIndex(def, varName);
                tracked.Add(new TrackedMember { fieldInfo = fi, component = comp, variableIndex = varIdx });
            }

            foreach (PropertyInfo pi in t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                var attr = pi.GetCustomAttribute<BlackboardTrackAttribute>();
                if (attr == null || !pi.CanRead) continue;
                string varName = attr.VariableName ?? pi.Name;
                int varIdx = ResolveIndex(def, varName);
                tracked.Add(new TrackedMember { propInfo = pi, component = comp, variableIndex = varIdx });
            }
        }
        initialized = true;
    }

    private static int ResolveIndex(BlackboardDefinition def, string name)
    {
        if (def == null) return -1;
        var allVars = def.GetAllVariables();
        for (int i = 0; i < allVars.Count; i++)
            if (allVars[i].Name == name) return i;
        return -1;
    }

    // ── Called every frame ──
    public void ProvideData(BlackBoard selfBB)
    {
        if (!initialized) return;
        for (int i = 0; i < tracked.Count; i++)
        {
            var tm = tracked[i];
            if (tm.variableIndex < 0) continue;
            object value = tm.fieldInfo != null
                ? tm.fieldInfo.GetValue(tm.component)
                : tm.propInfo.GetValue(tm.component);
            selfBB.Set(tm.variableIndex, value);
        }
    }
}
```

### Performance Analysis

| Phase | What happens | Cost |
|---|---|---|
| `Awake()` | `GetComponentsInChildren`, `GetFields/GetProperties`, `GetCustomAttribute`, one `O(n)` variable name scan per tracked member | One-time startup cost |
| `ProvideData()` | Cached `FieldInfo.GetValue` / `PropertyInfo.GetValue` + `bb.Set(index, value)` | Per-frame per tracked member |

**Per-frame cost per tracked member:**
- `FieldInfo.GetValue` on a reference type: ~15-30ns (direct read after JIT)
- `FieldInfo.GetValue` on a value type: boxes the struct, ~30-60ns
- `PropertyInfo.GetValue`: ~30-80ns
- `BlackBoard.Set<T>(index, value)`: array write + one `Contains` check on `overriddenReferenceSlots`, ~10ns
- **Total per member: ~50-150ns**

**For a realistic scenario:**
- 5 tracked members per agent × 100 agents = 500 tracked values/frame
- 500 × 150ns = **~0.075ms per frame**

This is well within acceptable bounds. The key optimization is that `variableIndex` is resolved once in `Awake()` — there is no per-frame `FindVariableIndex` string scan.

**Compared to manual `IBlackboardDataProvider`:**
- Manual: `blackboard.Set("health", currentHealth)` → calls `FindVariableIndex("health")` which is O(n) string scan every frame
- Auto: `blackboard.Set(cachedIndex, value)` → direct array write, O(1)

So the auto-tracked version is actually **faster** than the naive manual version, since it resolves the variable name to an index once.

### When to use which

| Approach | Use when |
|---|---|
| `[BlackboardTrack]` | One field = one variable, same type, no computation |
| `IBlackboardDataProvider` | Multiple variables, computation needed, conditional writes |

---

## Files to Create

| File | Purpose |
|---|---|
| `Core/CustomValueFieldAttribute.cs` | Line count attribute for custom types |
| `Core/IBlackboardValueDrawer.cs` | Interface for inline custom type editing |
| `Core/CustomBlackboardValueEditorAttribute.cs` | Registers a drawer for a type |
| `Editor/BlackboardValueDrawerRegistry.cs` | Reflection-based drawer registry |
| `Core/BlackboardTrackAttribute.cs` | Auto-track attribute |
| `Runtime/AutoTrackProvider.cs` | Scans for `[BlackboardTrack]` and pushes each frame |

---

## 4. DOTS Baking — Blittable Runtime Assets

### What breaks in the current bake output

The existing `RuntimeBehaviourTreeAsset` has three managed dependencies that block Burst/ECS:

| Field | Problem | Why |
|---|---|---|
| `NodeData.methodName` (string) | Managed | Strings can't live in `BlobAsset` or `NativeArray` |
| `object[] boxedConstants` | Managed | `object` arrays box every value, GC-tracked |
| `BlackboardDefinition blackboardDefinition` | Managed | `ScriptableObject` with `List<BlackboardVariableBase>` |
| `NodeMethod[] methodInstances` (in TreeEvaluator) | Managed | Virtual dispatch, GC allocations |

However, `NodeData` (4 ints + 1 string) and `FieldData` (byte + int, `[StructLayout.Explicit]`) are almost blittable already. The core tree topology (firstChildIndex, lastChildIndex, fieldDataStartIndex) **is already flat and job-friendly**.

### What a DOTS bake produces

A single `BlobAssetReference<BehaviourTreeBlob>` containing everything the evaluator needs — zero managed allocations at runtime:

```
BlobAssetReference<BehaviourTreeBlob>
├── BlobArray<NodeDataBlob>   nodes        // tree topology (no strings)
├── BlobArray<FieldData>      fields       // unchanged — already blittable
├── BlobArray<byte>           constants    // raw bytes for Vector3/Color/custom types
├── BlobArray<ConstantLayout> constantMeta // type + offset + size per boxed constant
├── BlobArray<MethodHash>     methods      // fixed-size int hash per node
├── BlobArray<SlotLayout>     slots        // blackboard slot layout
├── int                       totalSlotCount
└── int                       maxTreeDepth
```

### Step-by-step transformation during bake

**Step 1 — Strip `methodName` strings, replace with FixedString32 or int hash.**

```csharp
// NodeDataBlob — blittable version of NodeData
public struct NodeDataBlob
{
    public BehaviourNodeType nodeType;  // enum → int
    public int firstChildIndex;
    public int lastChildIndex;
    public int fieldDataStartIndex;
    public int fieldDataCount;
    public int methodHash;              // replaces string methodName
}
```

The baker computes `hash = methodName.GetHashCode()` and stores the int. The method dispatch table in the ECS system maps `hash → function pointer`.

**Step 2 — Stretch `object[] boxedConstants` into a flat `BlobArray<byte>`.**

Instead of `object[]` with boxed `Vector3`, `Color`, `Matrix4x4`, etc., the baker writes each constant's raw bytes sequentially and records its byte offset and type in a parallel `ConstantLayout` array:

```csharp
public struct ConstantLayout
{
    public int byteOffset;  // where in the byte blob this constant starts
    public int byteSize;    // sizeof(T)
    public int typeHash;    // typeof(T).GetHashCode() for debug / type checking
}
```

At runtime, the evaluator reads via `UnsafeUtility.As<T>(ref blobBytes[layout.byteOffset])`. No boxing, no allocation, Burst-compatible.

**Step 3 — Bake blackboard variable definitions into slot layouts.**

The `BlackboardDefinition` with its `List<BlackboardVariableBase>` (polymorphic, `[SerializeReference]`) is editor-only. The baker collapses it into:

```csharp
public struct SlotLayout
{
    public int byteOffset;   // offset into the per-agent value buffer
    public int byteSize;     // sizeof(T) for this slot
    public int typeHash;     // for runtime type verification
    public int stride;       // 1 for scalars, >1 for arrays
    public BlackboardSlotKind kind; // Value or Reference
}
```

The per-agent runtime storage becomes a `DynamicBuffer<byte>` attached to the agent entity. `slotLayout[3].byteOffset = 48` means "slot 3 lives at byte offset 48 in the agent's byte buffer." The evaluator reads:

```csharp
// In a Burst job:
var bytes = agentBuffers[entity];
int offset = layouts[slotIndex].byteOffset;
float value = UnsafeUtility.ReadArrayElement<float>(bytes.GetUnsafePtr(), offset);
```

This replaces `ManagedBlackboardStorage.object[] values` with a single flat `DynamicBuffer<byte>` per agent.

**Step 4 — Function pointer dispatch table.**

`TickDispatcher` currently does `methodInstances[nodeIndex]?.Execute(ref ctx)` via virtual calls on `NodeMethod`. For DOTS, the bake assigns each node a `methodHash`. The system builds a dispatch table:

```csharp
// Built once, used every tick
private delegate NodeState TickFunc(int nodeIndex, ref TickContextBlob ctx);
private Dictionary<int, TickFunc> methodDispatch;
```

Or, for maximum Burst performance, a drop-through switch:

```csharp
[BurstCompile]
static NodeState DispatchMethod(int hash, int nodeIndex, ref TickContext ctx)
{
    switch (hash)
    {
        case 0x3A4B5C6D: return MyCustomLeaf.Tick(nodeIndex, ref ctx);
        case 0x1A2B3C4D: return WaitNode.Tick(nodeIndex, ref ctx);
        // ... generated per bake
    }
}
```

### Runtime architecture (ECS)

```
[Agent Entity]                     [BlobAsset (shared)]
├── DynamicBuffer<byte> BBValues   ← slot data mapped via blob layouts
├── DynamicBuffer<int> ActiveChild
├── DynamicBuffer<NodeState> States
└── BehaviourTreeBlobRef shared    → BlobAssetReference<BehaviourTreeBlob>

BehaviourTreeEvaluateSystem (Burst ISystem):
  OnUpdate:
    foreach agent:
      byte[] bb = agent.BBValues
      int[] child = agent.ActiveChild
      NodeState[] states = agent.States
      ref Blob = agent.BlobRef.Value
      TickNode(0, ref ctx) // recursive, Burst-compiled
```

### What the baker adds to TreeBaker

`TreeBaker.BakeTree()` already iterates the authoring tree and produces `NodeData[]` + `FieldData[]` + `object[] boxedConstants`. The DOTS path adds three post-processing passes:

1. **Method hash pass** — Replace `node.methodName` with `hash`. Build the constant byte blob with `UnsafeUtility.SizeOf<T>()` per boxed value.
2. **Slot layout pass** — Walk `BlackboardDefinition.sharedVariables`, compute per-slot `byteOffset` and `byteSize`. Output `BlobArray<SlotLayout>`.
3. **Blob assembly** — `BlobBuilder` writes all arrays into a single `BlobAssetReference<BehaviourTreeBlob>`.

### Migration path

| Phase | What changes |
|---|---|
| Phase 1 | Add `NodeDataBlob`, `SlotLayout`, `ConstantLayout` structs to `Core/` |
| Phase 2 | Add DOTS bake methods to `TreeBaker` (outputs `BlobAssetReference`) |
| Phase 3 | Write `BehaviourTreeEvaluateSystem` using `ISystem` + `BlobAssetReference` |
| Phase 4 | Write `AutoTrackSystem` that replaces `AutoTrackProvider` for DOTS agents |

The managed `TreeRunner` and DOTS `BehaviourTreeEvaluateSystem` share the same bake pipeline — `TreeBaker` just outputs two formats: `RuntimeBehaviourTreeAsset` (managed) OR `BlobAssetReference<BehaviourTreeBlob>` (DOTS).
