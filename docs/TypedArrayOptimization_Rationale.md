# Typed Array Blackboard Optimization — Rationale & Design

## Problem

The blackboard runtime storage (`ManagedBlackboardStorage`) uses a single `object[] values` array to store all variable values — both reference types (GameObject, Transform) and value types (int, float, bool, Vector3, enums, custom structs).

**Every `Set<int/float/bool/...>` implicitly boxes the value onto the managed heap.** In a commander tree ticking N agents per frame with M value-type variables, this produces N × M heap allocations per frame, creating constant GC pressure. On high-agent-count scenarios (stride-based squads), this becomes the dominant performance bottleneck.

Additionally, on AOT platforms (iOS, consoles, WebGL), the compiled delegate fast path (`Expression.Compile()`) is unavailable. All field reads/writes fall through to a reflection path (`ReadFromBBGeneric`/`WriteToBBGeneric`) that calls `GetBoxed`/`SetBoxed` on every access.

## Design Goals

1. **Eliminate boxing** on the hot path for value-type variables
2. **Zero runtime allocations** per frame after initialization
3. **Preserve the existing architecture** — slot offset system, bake pipeline, stride, commander/squad
4. **No impact on editor iteration speed** — edit-and-play must remain instant
5. **AOT-safe** — iOS and console builds must have the same zero-alloc path as Mono
6. **Extensible** — custom user-defined value types can participate
7. **Incremental** — no big-bang rewrite; changes are confined to 2-3 files

## Non-Goals

- Changing the serialization format (`.asset` files)
- Changing the editor UI (BlackBoardView, BlackBoardEditor, node inspectors)
- Changing how node methods are authored
- DOTS integration (this is a prerequisite for DOTS, not the DOTS work itself)

## Solution Overview

Replace `object[] values` with **one typed array per supported value type**, plus a fallback `object[]` for everything else:

```
Before:  object[] values          → values[slot] = boxed int
After:   int[] intValues           → intValues[slot] = raw int
         float[] floatValues       → floatValues[slot] = raw float
         bool[] boolValues         → boolValues[slot] = raw bool
         Vector3[] vector3Values   → vector3Values[slot] = raw Vector3
         Object[] refValues        → refValues[slot] = GameObject/Transform ref
         object[] customValues     → fallback for unregistered types (rare)
```

The slot index is unchanged. The slot's type (already known from `slotTypes[i]`) determines which array to use.

## Considerations

### Iteration Speed vs AOT Safety

The current system uses `Expression.Compile()` to produce fast delegates. This works on Mono (Editor, standalone Windows) but is unavailable on AOT platforms (iOS, consoles, WebGL), which silently fall back to reflection.

**Decision**: Use two parallel execution paths, switched at compile time:

| | Editor (Development) | Build (Shipping) |
|---|---|---|
| **Codegen** | None (instant iteration) | Generator produces typed dispatch |
| **Field access** | `Expression.Compile()` delegates (existing) | Generated switch-based dispatch (AOT-safe) |
| **Boxing** | Eliminated by typed arrays | Eliminated by typed arrays + typed dispatch |

The generated file is `#if !UNITY_EDITOR` — dead code during development. When a new method is added, the Editor path works instantly (Expression). A pre-build hook regenerates the file before shipping.

### Custom Types

Users define custom structs as blackboard variables. Rather than scanning all definitions to discover types, the system uses an explicit registry:

```csharp
public static class BlackboardTypedArrayRegistry
{
    public static readonly IReadOnlyList<Type> SupportedTypes = new Type[]
    {
        typeof(int), typeof(float), typeof(bool),
        typeof(Vector2), typeof(Vector3),
        typeof(GameObject), typeof(Transform),
        // ── user adds custom types here ──
        typeof(MyStruct), typeof(DamageInfo),
    };
}
```

Built-in types (int, float, bool, Vector*) are hand-written in `ManagedBlackboardStorage`. Custom types get typed arrays generated from the registry. Types not in the registry continue to use `object[]` with boxing — they still work, just slower. This is an explicit opt-in for perf-critical types.

### SquadInstance Copy Path

`SquadInstance.CopyToBB` / `CopyFromBB` currently uses `GetBoxed`/`SetBoxed` to move pre-boxed references between slots. With typed arrays, value types are no longer pre-boxed.

**Decision**: Generate typed copy methods for the squad boundary. The copy loop dispatches per slot type, calling the correct typed copy for each pair. Types not in the registry fall through to the `object[]` path with on-demand boxing.

### Per-Component Overrides

Override values are stored as typed fields in `BlackboardValueOverride` (serialized via Unity). During `Initialize()`, they're applied to storage via `SetBoxed`. With typed arrays, this path unboxes the override value once — acceptable since it's init-only.

### Stride Resizing

Currently `ResizeFromVariables` allocates one `object[newSize]`. With typed arrays, it calls `Array.Resize` on every typed array. This is a one-time cost on agent join/leave, not per-frame, so the overhead is negligible.

## What Does Not Change

| Component | Status |
|---|---|
| `BlackboardDefinition` / `BlackboardVariable<T>` | Unchanged |
| `TreeBaker` — bake pipeline, slot offset math | Unchanged |
| `FieldData` — packed struct | Unchanged |
| `FieldReader` — already calls typed methods (`GetInt`, `GetFloat`) | Unchanged |
| `NodeMethod` subclasses — field declarations, logic | Unchanged |
| `BlackBoardView`, `BlackBoardEditor` — editor UI | Unchanged |
| `.asset` serialization format | Unchanged |
| Commander stride system | Unchanged |

## What Changes

| File | Change |
|---|---|
| `ManagedBlackboardStorage.cs` | Typed arrays replace `object[]` for registered types; fallback `object[]` for rest |
| `NodeMethod.cs` | Remove `Expression.Compile()`, add `#if` split between Editor delegates and Build generated dispatch |
| `SquadInstance.cs` | Typed copy methods for squad BB copy |
| (new) `ManagedBlackboardStorage.CustomArrays.g.cs` | Generated typed array partial class |
| (new) `NodeMethodDispatch.g.cs` | Generated typed dispatch for Build |
| `BlackboardEnumGenerator.cs` | Extended to also produce the two generated files above |

## Alternatives Considered

### Full DOTS / Burst Conversion
Rejected for now. Too large a scope. Typed arrays are a prerequisite that must be done first regardless.

### Behaviour Designer SharedVariable Model
Rejected. Would require rewriting ~74 files, and BD's model lacks native stride support. See `SharedVariable_Migration_Assessment.md`.

### Source Generators for Method Dispatch
Rejected. Would slow down editor iteration. The `#if !UNITY_EDITOR` split keeps iteration instant.

### Per-Type Dynamic Arrays via Reflection
Rejected. `Array.CreateInstance` + `GetValue`/`SetValue` still boxes internally. Generated typed arrays are the only way to achieve true zero-boxing for custom types.
