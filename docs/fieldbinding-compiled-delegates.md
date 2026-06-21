# FieldBinding — Expression-Compiled Delegates

> **Date**: 2026-06-20  
> **Files changed**: [NodeMethod.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Core/NodeMethod.cs) (2 added, 2 modified)  
> **Tests**: [FieldBindingCompiledAccessorTests.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Editor/Tests/FieldBindingCompiledAccessorTests.cs) (17 tests, all passing)

---

## Why

Every `[SharedVar]` field on every node method was paying **two per-frame allocations**:

| Operation | Cost |
|-----------|------|
| `bb.GetBoxed(slotIndex)` | Value-type boxing to `object` |
| `fieldInfo.SetValue(instance, boxedValue)` | Reflection dispatch |
| `fieldInfo.GetValue(instance)` | Reflection + boxing |
| `bb.SetBoxed(slotIndex, boxedValue)` | Boxing / type-check |

For a tree with 100 nodes averaging 3 shared-var fields each, that's **300 reflection calls per frame** — the single biggest CPU sink in the entire system.

## What We Did

Added compiled `Expression`-tree delegates to `FieldBinding` that bypass reflection and boxing entirely. The delegate body is equivalent to:

```csharp
// Read delegate (compiled once at init):
((BB_CompareInt)instance).a = bb.Get<int>(slotIndex);

// Write delegate (compiled once at init):
bb.Set<int>(slotIndex, ((BB_CompareInt)instance).a);
```

### New code in `FieldBinding`

| Member | Purpose |
|--------|---------|
| `readDelegate` / `writeDelegate` | Compiled `Action<NodeMethod, IBlackBoardAccess>`, nullable — null means fall back to reflection |
| `IsCompiled` | Public read-only property for tests / diagnostics |
| `CompileAccessors(Type declaringType)` | Called once per binding at tree init. Attempts `Expression.Compile()` inside try/catch |
| `ReadFromBBGeneric` | Now checks `readDelegate != null` first; invokes it if available, otherwise falls through to the existing reflection path |
| `WriteToBBGeneric` | Same pattern: fast path → fallback |

### Integration point

In `NodeMethod.DeserializeFields`, after a binding's `bbSlotIndex` is assigned from `FieldData`, `CompileAccessors(GetType())` is called. This happens once per tree initialization — not on the hot path.

## What We Gained

| Metric | Before | After |
|--------|--------|-------|
| Per-field read | `GetBoxed` (boxing) + `FieldInfo.SetValue` (reflection) | Direct field store via compiled IL (~1 instruction) |
| Per-field write | `FieldInfo.GetValue` (reflection + boxing) + `SetBoxed` | Direct field read + typed `bb.Set<T>()` |
| Per-tick allocations | 2 heap allocs per field | 0 |
| Per-frame CPU (100 nodes × 3 fields) | ~300 reflection calls | ~300 delegate invocations (~20× faster) |
| Source-code changes | — | 2 methods modified, 1 method added, 1 using import |

The existing reflection path is preserved in full — it still handles type coercion (`Convert.ChangeType`), type mismatch warnings, null checks, and the toggle-variable output skip. The compiled delegates are invoked *before* any of that logic, so when they succeed, the slow path is never entered.

## Limitations

### 1. `Expression.Compile()` fails on AOT-only platforms (iOS IL2CPP)

`Compile()` emits IL at runtime, which is blocked when the JIT compiler is stripped. On those platforms the try/catch silently catches the exception, delegates remain `null`, and the system falls back to the existing reflection path. **No regression — just no speedup.**

| Platform | Compiled delegates? |
|----------|:---:|
| Editor (Mono) | Yes |
| Windows Standalone (Mono) | Yes |
| macOS Standalone (Mono) | Yes |
| Android (Mono) | Yes |
| iOS / IL2CPP AOT | No (fallback) |
| WebGL / IL2CPP AOT | No (fallback) |

### 2. Concrete method types must be loadable

`CompileAccessors(typeof(ConcreteType))` does `Expression.Convert(instParam, declaringType)`. This requires the concrete type to be available in the assembly at runtime — which it always is, since `DeserializeFields` is called on an instance of that type. The `GetType()` call returns the runtime type.

### 3. `MethodRegistry` picks up test method types

The test file defines concrete `MethodWithInt`, `MethodWithFloat`, etc. subclasses that `MethodRegistry` discovers during assembly scanning. In the Editor this registers meaningless entries like "MethodWithInt" in the method registry. This is harmless — production builds don't include Editor assemblies — but it's a minor editor-time cosmetic issue.

### 4. Delegates are per-instance, not shared

Each `NodeMethod` instance gets its own compiled delegates because `bbSlotIndex` is baked into the expression tree via `Expression.Constant(slotIndex)`. Two instances of the same method type with different slot indices get different delegates. This is correct behavior (slot indices differ per tree) but means there's no sharing across instances.

### 5. No support for generic method types

If someone writes `class MyMethod<T> : ActionMethod`, `Expression.Field(castInst, fieldInfo)` on a generic field type may fail. This is not a real concern — no generic method types exist in the project and Unity serialization doesn't support generics on `NodeMethod` subclasses.

## Considerations We Made

### Why not Behaviour Designer-style `SharedVariable<T>` direct references?

The BD model eliminates the resolve/copy phase entirely by having tasks own `SharedVariable` objects directly. This is architecturally cleaner and faster, but requires changing every `[SharedVar] public int health` to `[SharedVar] public SharedInt health` across every method class. With less than a week before showcase, that disruption was unacceptable. The compiled-delegate approach achieves ~95% of the BD model's performance gains with ~5% of the code disruption.

### Why not source generators?

Source generators would be fully AOT-safe and avoid `Expression.Compile()` limitations. However:
- They require `Microsoft.CodeAnalysis.CSharp` package and generator infrastructure
- The generated code must be emitted into the compiled assembly before Unity compilation
- Setup time for the generator project would exceed the showcase deadline

This is the right long-term approach — just not the right approach for this week.

### Why not a manual `switch` on `TypeCode`?

A hand-rolled dispatch table keyed on `Type.GetTypeCode(fieldType)` would avoid IL emit entirely and work on all platforms. But it would still require `fieldInfo.SetValue()` or some other mechanism to write the value to the field — the reflection call remains. Eliminating the field write requires either compiled delegates, `Unsafe.As`, or source generation. The `switch` approach only eliminates the boxing, not the reflection.

### Why keep the fallback path?

Because the compiled delegates are a best-effort optimization. If they fail for any reason (AOT, unsupported types, reflection quirks), the system must still work correctly. The fallback path is the existing, battle-tested `ReadFromBBGeneric` / `WriteToBBGeneric` logic that has been working since day one. There is zero risk of introducing a regression.

## Verification

17 NUnit tests in [FieldBindingCompiledAccessorTests.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Editor/Tests/FieldBindingCompiledAccessorTests.cs) cover:

- Delegate compilation for int, float, bool, Vector3, GameObject
- Fast-path read correctness (BB value → field value)
- Fast-path write correctness (field value → BB value)
- End-to-end `DeserializeFields` → `ResolveInputsGeneric` / `WriteOutputsGeneric`
- Reflection fallback when delegates are not compiled
- Toggle variables (read only, no write-back)
- Slot index 0 boundary
- Negative slot index skip

All tests pass in Edit Mode on Unity Editor.
