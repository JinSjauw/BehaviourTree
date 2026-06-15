# 1. Problems

Three related code-quality issues across `TreeBaker.cs`, `FieldReader.cs`, and `MethodMetadataCache.cs`:

- `TreeBaker` has three copy-pasted `CountFieldDataForNode` overloads with identical logic.
- `FieldReader.Get<T>()` has a dead conditional where both branches execute the same code.
- `MethodMetadataCache` never invalidates its cache after `MethodRegistry.Rebuild()`, unlike other consumers of the same event.

## 1.1. **TreeBaker: triplicated CountFieldDataForNode with identical logic**

In `Assets/Scripts/BehaviourTree/Runtime/TreeBaker.cs:494-525`, three overloads of `CountFieldDataForNode` exist — one per node type — but all three contain the same body:

```csharp
private static int CountFieldDataForNode(LeafNode node, BlackboardDefinition runtimeBbDef)
{
    if (node.fieldEntries == null) return 0;
    int count = 0;
    for (int i = 0; i < node.fieldEntries.Count; i++)
        count