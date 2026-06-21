# Typed Array Blackboard Optimization — Implementation Plan

## Overview

Replace the single `object[] values` array in [ManagedBlackboardStorage.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Core/ManagedBlackboardStorage.cs) with one typed array per supported value type. Eliminates per-frame boxing allocations. Use a code generator to produce typed arrays for custom user types and a typed dispatch path for AOT-safe builds.

## Files Involved

| File | Action | LOC estimate |
|---|---|---|
| `BlackboardTypedArrayRegistry.cs` (new) | Create — explicit registry of supported types | ~20 |
| `ManagedBlackboardStorage.cs` | Modify — swap `object[]` for typed arrays + fallback | ~180 → ~350 |
| `NodeMethod.cs` | Modify — add `#if !UNITY_EDITOR` generated dispatch fallback | ~350 → ~390 |
| `ManagedBlackboardStorage.CustomArrays.g.cs` (new) | Generate — typed array partial class | ~80 generated |
| `NodeMethodDispatch.g.cs` (new) | Generate — typed dispatch for build | ~400 generated |
| `BlackboardEnumGenerator.cs` | Extend — produce the two new generated files | ~160 → ~280 |
| `SquadInstance.cs` | Modify — typed copy methods for squad BB copy | ~170 → ~220 |

---

## Phase 1: Type Registry

### Task 1.1 — Create `BlackboardTypedArrayRegistry.cs`

**Path**: `Assets/Scripts/BehaviourTree/Core/BlackboardTypedArrayRegistry.cs`  
**Assembly**: `BehaviourTree.Core`

```csharp
namespace BehaviourTree.Core
{
    /// <summary>
    /// Explicit registry of types for which typed arrays are generated.
    /// Add a type here → generator produces zero-boxing storage + dispatch.
    /// Types not listed fall through to object[] with boxing — they still work.
    /// </summary>
    public static class BlackboardTypedArrayRegistry
    {
        public static readonly IReadOnlyList<Type> SupportedTypes = new Type[]
        {
            // ── Built-in (hand-written in ManagedBlackboardStorage) ──
            typeof(int),
            typeof(float),
            typeof(bool),
            typeof(Vector2),
            typeof(Vector3),
            typeof(Vector4),
            typeof(Color),
            typeof(Vector2Int),
            typeof(Vector3Int),
            typeof(GameObject),
            typeof(Transform),

            // ── Custom (add your types here → regenerator produces typed arrays) ──
            // typeof(MyStruct),
            // typeof(DamageInfo),
        };
    }
}
```

**Verification**: Compiles in Core assembly. No runtime logic — just a data list.

---

## Phase 2: Typed Array Storage

### Task 2.1 — Modify `ManagedBlackboardStorage.cs`

Replace the single `object[] values` with:

```csharp
// ── Typed arrays — one per supported value type ──
private int[] intValues;
private float[] floatValues;
private bool[] boolValues;
private Vector2[] vector2Values;
private Vector3[] vector3Values;
private Vector4[] vector4Values;
private Color[] colorValues;
private Vector2Int[] vector2IntValues;
private Vector3Int[] vector3IntValues;
private UnityEngine.Object[] refValues;     // GameObject, Transform

// ── Fallback — types not in the registry ──
private object[] customValues;

// Slot count (same for all arrays)
private int slotCount;
```

**`InitializeFromVariables`** — allocate all typed arrays at once, copy default values by slot type:

```csharp
private void InitializeFromVariables(IReadOnlyList<BlackboardVariableBase> variables)
{
    // ... compute totalSlotCount (unchanged) ...

    slotCount = totalSlotCount;
    AllocateTypedArrays(slotCount);
    CopyDefaultsToTypedArrays(variables);
}

private void AllocateTypedArrays(int count)
{
    intValues = new int[count];
    floatValues = new float[count];
    boolValues = new bool[count];
    vector2Values = new Vector2[count];
    vector3Values = new Vector3[count];
    vector4Values = new Vector4[count];
    colorValues = new Color[count];
    vector2IntValues = new Vector2Int[count];
    vector3IntValues = new Vector3Int[count];
    refValues = new UnityEngine.Object[count];
    customValues = new object[count];
}
```

**`Get<T>`** — dispatch to the correct typed array:

```csharp
public T Get<T>(int index)
{
    if (index < 0 || index >= slotCount) return default;
    Type t = typeof(T);

    if (t == typeof(int))       return (T)(object)intValues[index];
    if (t == typeof(float))     return (T)(object)floatValues[index];
    if (t == typeof(bool))      return (T)(object)boolValues[index];
    if (t == typeof(Vector2))   return (T)(object)vector2Values[index];
    if (t == typeof(Vector3))   return (T)(object)vector3Values[index];
    if (t == typeof(Vector4))   return (T)(object)vector4Values[index];
    if (t == typeof(Color))     return (T)(object)colorValues[index];
    if (t == typeof(Vector2Int)) return (T)(object)vector2IntValues[index];
    if (t == typeof(Vector3Int)) return (T)(object)vector3IntValues[index];
    if (t == typeof(GameObject) || t == typeof(Transform))
        return (T)(object)refValues[index];

    // Generated custom type path (see Phase 4)
    if (TryGetCustomArray<T>(out Array arr)) return ((T[])arr)[index];

    // Fallback
    object val = customValues[index];
    if (val is T tVal) return tVal;
    return default;
}
```

**`Set<T>`** — same dispatch, set instead of read. For reference types, set on `refValues[index]`. For value types, set on the typed array.

**Hot-path methods** — add explicit non-generic methods for `FieldReader` (already calls typed methods):

```csharp
public int GetInt(int index) => intValues[index];
public void SetInt(int index, int value) => intValues[index] = value;
public float GetFloat(int index) => floatValues[index];
public void SetFloat(int index, float value) => floatValues[index] = value;
public bool GetBool(int index) => boolValues[index];
public void SetBool(int index, bool value) => boolValues[index] = value;
public Vector3 GetVector3(int index) => vector3Values[index];
public void SetVector3(int index, Vector3 value) => vector3Values[index] = value;
public GameObject GetGameObject(int index) => (GameObject)refValues[index];
public void SetGameObject(int index, GameObject value) => refValues[index] = value;
// ... Vector2, Color, Vector2Int, Vector3Int, Vector4, Transform
```

**`GetBoxed`** / **`SetBoxed`** — now boxes/unboxes on demand (used by SquadInstance, init, overrides):

```csharp
public object GetBoxed(int index)
{
    if (index < 0 || index >= slotCount) return null;
    int kind = slotKinds[index] == BlackboardSlotKind.Reference ? 0 : GetBuiltInKind(index);
    return kind switch
    {
        1 => intValues[index],
        2 => floatValues[index],
        3 => boolValues[index],
        4 => vector2Values[index],
        5 => vector3Values[index],
        // ... etc ...
        _ => refValues[index] ?? customValues[index],
    };
}
```

**`ResizeFromVariables`** — `Array.Resize` on every typed array instead of one `object[]`:

```csharp
Array.Resize(ref intValues, newSlotCount);
Array.Resize(ref floatValues, newSlotCount);
Array.Resize(ref boolValues, newSlotCount);
// ... all typed arrays ...
Array.Resize(ref customValues, newSlotCount);
```

The data-copy loop copies per slot type instead of via `object[]` reference copying.

### Task 2.2 — Add `GetSlotKind` cache logic

The slot kind array `BlackboardSlotKind[] slotKinds` is already populated during init. Use it to route access. For typed arrays, the slot kind can be a compact `byte` encoding (0=int, 1=float, 2=bool, ...) instead of the current `Value`/`Reference` enum. Or keep the existing enum and add a helper:

```csharp
private static bool IsValueTypeKind(BlackboardSlotKind kind) => kind == BlackboardSlotKind.Value;
```

### Task 2.3 — Update `IBlackboardStorage` (if needed)

The interface stays the same. Only the implementation changes. No breakage for consumers.

**Verification**: Existing unit tests pass. Manual test: create a commander tree with squad, run in play mode, verify variable values are correct via debug inspector.

---

## Phase 3: AOT-Safe Method Dispatch

### Task 3.1 — Modify `NodeMethod.cs`

Add a `#if` split in `ResolveInputsGeneric` and `WriteOutputsGeneric`:

```csharp
public void ResolveInputsGeneric(IBlackBoardAccess bb)
{
    bbAccess = bb;
    FieldBinding[] b = bindings;
    if (b == null) return;

#if !UNITY_EDITOR
    // Build path: generated typed dispatch (AOT-safe, zero boxing)
    ResolveInputs_Build(bb);
#else
    // Editor path: compiled delegates (instant iteration, no codegen needed)
    for (int i = 0; i < b.Length; i++)
    {
        if (b[i] != null && b[i].bbSlotIndex >= 0)
            b[i].ReadFromBBGeneric(this, bb);
    }
#endif
}

public void WriteOutputsGeneric(IBlackBoardAccess bb)
{
#if !UNITY_EDITOR
    WriteOutputs_Build(bb);
#else
    FieldBinding[] b = bindings;
    if (b == null) return;
    for (int i = 0; i < b.Length; i++)
        b[i]?.WriteToBBGeneric(this, bb);
#endif
}
```

The `ResolveInputs_Build` and `WriteOutputs_Build` methods are generated (see Phase 5). They are `partial` methods that compile out to nothing when the generated file is absent — the compilation degrades gracefully to the reflection fallback.

**Also**: Remove `CompileAccessors` calls from `DeserializeFields` in the build path (add `#if UNITY_EDITOR` guard). On Build, there's no delegate compilation — the generated dispatch replaces it.

### Task 3.2 — Optional: Remove `Expression.Compile` references entirely

Add `#if UNITY_EDITOR` around the entire `CompileAccessors` method body and the `readDelegate`/`writeDelegate` fields, or keep them and just never call them on Build. Keeping them is simpler and they're dead-stripped by the linker.

**Verification**: Build for iOS/WebGL. Run the same test tree. Verify via profiler that `ReadFromBBGeneric` reflection calls are not hit.

---

## Phase 4: Custom Type Array Generator

### Task 4.1 — Extend `BlackboardEnumGenerator.cs`

Add two new generation methods:

```csharp
// Called by the same menu item or a separate one
[MenuItem("BehaviourTree/Generate/Generate Typed Arrays", priority = 32)]
public static void GenerateTypedArrays()
{
    GenerateCustomArraysPartial();
    GenerateNodeMethodDispatch();
    AssetDatabase.Refresh();
}
```

### Task 4.2 — Generate `ManagedBlackboardStorage.CustomArrays.g.cs`

**Path**: `Assets/Scripts/BehaviourTree/Core/Generated/ManagedBlackboardStorage.CustomArrays.g.cs`  

Read `BlackboardTypedArrayRegistry.SupportedTypes`. For each type that is a custom type (not in the built-in set):

```csharp
// ══════════════════════════════════════════════════════════════
// AUTO-GENERATED. Do not edit manually.
// Regenerate via Tools → BehaviourTree → Generate Typed Arrays.
// ══════════════════════════════════════════════════════════════

namespace BehaviourTree.Core
{
    partial class ManagedBlackboardStorage
    {
        // ── Custom typed arrays ──
        private MyStruct[] __MyStruct_values;
        private DamageInfo[] __DamageInfo_values;

        partial void AllocateCustomArrays()
        {
            __MyStruct_values = new MyStruct[slotCount];
            __DamageInfo_values = new DamageInfo[slotCount];
        }

        partial void ResizeCustomArrays()
        {
            Array.Resize(ref __MyStruct_values, newSlotCount);
            Array.Resize(ref __DamageInfo_values, newSlotCount);
        }

        partial bool TryGetCustomArray<T>(out Array array)
        {
            Type t = typeof(T);
            if (t == typeof(MyStruct))   { array = __MyStruct_values; return true; }
            if (t == typeof(DamageInfo)) { array = __DamageInfo_values; return true; }
            array = null; return false;
        }

        partial void CopySlotCustom(int srcIndex, int dstIndex, Type slotType)
        {
            if (slotType == typeof(MyStruct))
                __MyStruct_values[dstIndex] = __MyStruct_values[srcIndex];
            else if (slotType == typeof(DamageInfo))
                __DamageInfo_values[dstIndex] = __DamageInfo_values[srcIndex];
        }

        partial object GetBoxedCustom(int index, Type slotType)
        {
            if (slotType == typeof(MyStruct))   return __MyStruct_values[index];
            if (slotType == typeof(DamageInfo)) return __DamageInfo_values[index];
            return null;
        }

        partial void SetBoxedCustom(int index, object value, Type slotType)
        {
            if (slotType == typeof(MyStruct))
                { __MyStruct_values[index] = (MyStruct)value; return; }
            if (slotType == typeof(DamageInfo))
                { __DamageInfo_values[index] = (DamageInfo)value; return; }
        }
    }
}
```

**Hand-written partial declarations** in `ManagedBlackboardStorage.cs`:

```csharp
partial void AllocateCustomArrays();
partial void ResizeCustomArrays();
partial bool TryGetCustomArray<T>(out Array array);
partial void CopySlotCustom(int srcIndex, int dstIndex, Type slotType);
partial object GetBoxedCustom(int index, Type slotType);
partial void SetBoxedCustom(int index, object value, Type slotType);
```

When the generated file doesn't exist (or has no custom types), these `partial` methods are no-ops that return `false`/`null`. The compiler removes them.

### Task 4.3 — Wire Custom Arrays into Storage

In `AllocateTypedArrays`, call `AllocateCustomArrays()` after the built-in allocations. In `Get<T>`, try `TryGetCustomArray<T>` after the built-in checks, before the `object[]` fallback. In `GetBoxed`/`SetBoxed`, try the custom path before `customValues`.

---

## Phase 5: AOT Dispatch Generator

### Task 5.1 — Generate `NodeMethodDispatch.g.cs`

**Path**: `Assets/Scripts/BehaviourTree/Runtime/Generated/NodeMethodDispatch.g.cs`

Read all `NodeMethod` subclasses (like `BlackboardEnumGenerator` scans by type). For each method that has bindings with known field types in the registry, produce:

```csharp
// ══════════════════════════════════════════════════════════════
// AUTO-GENERATED. Do not edit manually.
// ══════════════════════════════════════════════════════════════

#if !UNITY_EDITOR

using BehaviourTree.Core;

namespace BehaviourTree.Runtime
{
    partial class NodeMethod
    {
        partial void ResolveInputs_Build(IBlackBoardAccess bb)
        {
            if (this is BB_CompareInt m0)
            {
                m0.compareValueA = bb.Get<int>(bindings[0].bbSlotIndex);
                m0.compareValueB = bb.Get<int>(bindings[1].bbSlotIndex);
                m0.compareOp = ((BlackboardCompareOps)bb.Get<int>(bindings[2].bbSlotIndex));
                return;
            }
            if (this is BB_CompareFloat m1)
            {
                m1.compareValueA = bb.Get<float>(bindings[0].bbSlotIndex);
                m1.compareValueB = bb.Get<float>(bindings[1].bbSlotIndex);
                m1.compareOp = ((BlackboardCompareOps)bb.Get<int>(bindings[2].bbSlotIndex));
                return;
            }
            if (this is BB_SetInt m2)
            {
                m2.value = bb.Get<int>(bindings[0].bbSlotIndex);
                return;
            }
            if (this is BB_SetFloat m3)
            {
                m3.value = bb.Get<float>(bindings[0].bbSlotIndex);
                return;
            }
            if (this is BB_GetPosition m4)
            {
                m4.target = bb.Get<Transform>(bindings[0].bbSlotIndex);
                return;
            }
            // ... one entry per method ...

            // Fallback — methods not yet generated still work:
            ResolveInputs_Reflection(bb);
        }

        partial void WriteOutputs_Build(IBlackBoardAccess bb)
        {
            if (this is BB_CompareInt m0)
            {
                // No outputs (isOutput is configured per field)
                return;
            }
            if (this is BB_SetInt m2)
            {
                // Write-back handled
                return;
            }
            // ... one entry per method ...

            WriteOutputs_Reflection(bb);
        }

        // Reflection fallback — identical logic to the current ReadFromBBGeneric loop
        private void ResolveInputs_Reflection(IBlackBoardAccess bb)
        {
            FieldBinding[] b = bindings;
            if (b == null) return;
            for (int i = 0; i < b.Length; i++)
            {
                if (b[i] != null && b[i].bbSlotIndex >= 0)
                    b[i].ReadFromBBGeneric(this, bb);
            }
        }

        private void WriteOutputs_Reflection(IBlackBoardAccess bb)
        {
            FieldBinding[] b = bindings;
            if (b == null) return;
            for (int i = 0; i < b.Length; i++)
                b[i]?.WriteToBBGeneric(this, bb);
        }
    }
}

#endif
```

**Hand-written partial declarations** in `NodeMethod.cs`:

```csharp
#if !UNITY_EDITOR
partial void ResolveInputs_Build(IBlackBoardAccess bb);
partial void WriteOutputs_Build(IBlackBoardAccess bb);
#endif
```

The generator scans all `NodeMethod` subclasses via `TypeCache.GetTypesDerivedFrom<NodeMethod>()`. For each, it reads the `MethodRegistry` cached `FieldBinding[]` to know field names, types, and `isOutput` flags. It emits typed access for every field whose type is in `BlackboardTypedArrayRegistry.SupportedTypes`.

---

## Phase 6: SquadInstance Typed Copy

### Task 6.1 — Modify `SquadInstance.cs`

Currently `CopyToBB` / `CopyFromBB` use `GetBoxed`/`SetBoxed` in a loop. With typed arrays, this path creates ephemeral boxes. Add a typed copy layer:

```csharp
public void CopyToBB(BlackBoard treeBB, BlackboardDefinition treeDef)
{
    if (!copyToCache.TryGetValue(treeDef, out int[] pairs) || pairs.Length == 0)
        return;

    IBlackboardStorage squadStorage = blackBoard.Storage;
    IBlackboardStorage treeStorage = treeBB.Storage;

    for (int i = 0; i < pairs.Length; i += 2)
    {
        int squadSlot = pairs[i];
        int treeSlot = pairs[i + 1];
        BlackboardSlotKind kind = squadStorage.GetSlotKind(squadSlot);

        if (kind == BlackboardSlotKind.Reference)
            treeStorage.SetBoxed(treeSlot, squadStorage.GetBoxed(squadSlot));
        else
            CopyValueSlot(squadStorage, squadSlot, treeStorage, treeSlot);
    }
}

private static void CopyValueSlot(
    IBlackboardStorage src, int srcIndex,
    IBlackboardStorage dst, int dstIndex)
{
    // Use typed access to avoid boxing
    Type slotType = GetSlotType(src, srcIndex);
    if (slotType == typeof(int))      dst.Set(srcIndex, src.Get<int>(srcIndex));
    else if (slotType == typeof(float)) dst.Set(srcIndex, src.Get<float>(srcIndex));
    else if (slotType == typeof(bool))  dst.Set(srcIndex, src.Get<bool>(srcIndex));
    else if (slotType == typeof(Vector3)) dst.Set(srcIndex, src.Get<Vector3>(srcIndex));
    // ... built-in types ...
    else
        dst.SetBoxed(dstIndex, src.GetBoxed(srcIndex)); // fallback: single box per pair
}
```

**Alternative — simpler**: keep `GetBoxed`/`SetBoxed` and accept the ephemeral boxing at the squad copy boundary. Squad copy is once per frame per squad, not per node per tick. The boxing cost is orders of magnitude lower than the current all-nodes-all-ticks path. Acceptable to defer typed squad copy to a follow-up.

---

## Phase 7: Pre-Build Hook

### Task 7.1 — Add `IPreprocessBuildWithReport` handler

```csharp
#if UNITY_EDITOR
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

class TypedArrayBuildHook : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        BlackboardEnumGenerator.GenerateTypedArrays();
    }
}
#endif
```

Ensures generated files are always fresh before a build.

---

## Phase 8: Verification

### Task 8.1 — Editor smoke test

Create a tree with all supported variable types (int, float, bool, Vector2, Vector3, GameObject, Transform). Run in play mode. Verify values are correct via debug inspector.

### Task 8.2 — Commander stride test

Create a commander tree with squad-data variables. Add/remove agents at runtime. Verify stride resize works correctly (no index corruption) and agent-specific values are isolated.

### Task 8.3 — Squad copy test

Create a squad with variables. Connect to a tree. Verify squad→tree and tree→squad copies work correctly.

### Task 8.4 — AOT verification

Build for iOS or WebGL. Run the same test tree. Verify all node methods execute correctly — no incorrect values, no crashes.

### Task 8.5 — Profiler check

Run a commander tree with 10 agents, 20 value-type variables. Profile per-frame allocations. Before: N×M boxing allocations. After: zero (except any custom types not in registry).

---

## Risk Mitigation

| Risk | Mitigation |
|---|---|
| Build dispatch diverges from Editor path | Generated dispatch falls through to reflection for ungenerated methods. Both paths use the same typed storage — value semantics are identical. |
| Custom type not in registry ships | Types not in registry fall through to `object[]` — still work, just box. Build still correct. |
| Stride resize corrupts per-type arrays | Copy loop copies per type. Add assert: all typed arrays have same `.Length` after resize. |
| Generated file merge conflicts | Generated files are committed. Conflicts resolve by regenerating (the entire file is a single codegen run — no hand edits to merge). |
| Partial method compilation errors on missing generated file | Partial methods with no implementation body are removed by the compiler (C# spec). Graceful degradation. |

---

## Execution Order

```
Phase 1 (Type Registry)           ← 30 min, no dependencies
Phase 2 (Typed Array Storage)     ← 2-3 hours, depends on Phase 1
Phase 3 (AOT Dispatch skeleton)   ← 1 hour, depends on Phase 2
Phase 4 (Custom Array Generator)  ← 2 hours, depends on Phase 1+2
Phase 5 (AOT Dispatch Generator)  ← 2 hours, depends on Phase 3
Phase 6 (SquadInstance copy)      ← 1 hour, depends on Phase 2
Phase 7 (Pre-build hook)          ← 30 min, depends on Phase 4+5
Phase 8 (Verification)            ← 2 hours, depends on all
```

Total: ~12 hours estimated.
