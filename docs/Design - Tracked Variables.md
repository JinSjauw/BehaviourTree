# Tracked Variables Tab — Design

## Overview

A new "Tracked" tab in the Graph Inspector shows per-component field/property → blackboard variable bindings. Bindings are persisted on the `TreeRunner` component. At runtime, `TreeRunner` pushes bound component values into the blackboard each frame.

---

## 1. Data Model

### TrackedBinding (Runtime, serializable)

```csharp
[Serializable]
public class TrackedBinding
{
    public Component targetComponent;
    public string memberName;              // field or property name
    public string blackboardVariableName;  // target BB variable
    public bool isProperty;                // true = PropertyInfo, false = FieldInfo

    // Transient (not serialized), resolved once at Start():
    public FieldInfo cachedFieldInfo;
    public PropertyInfo cachedPropertyInfo;
    public int variableIndex;
}
```

### On TreeRunner

```csharp
public List<TrackedBinding> trackedBindings = new List<TrackedBinding>();
```

Bindings are serialized on the `TreeRunner` component in the scene/prefab. The list is user-curated — no auto-discovery from attributes.

---

## 2. UI Layout

```
┌────────────────────────────────────────────────────────────────────┐
│  Additional GameObject:  [none  ⊙]                                  │
│                                                                      │
│  #   Component / Member               →  BB Variable           ✕     │
│  ────────────────────────────────────────────────────────────────  │
│  1  [HealthComponent.health       ▼]  →  [currentHealth     ▼]   ✕  │
│  2  [WeaponComponent.ammo         ▼]  →  [ammoCount         ▼]   ✕  │
│  3  [AiSensor.detectionRange      ▼]  →  [detectionRange    ▼]   ✕  │
│                                                                      │
│  [+ Add Binding]     [+ Add Binding From Scene]                      │
│  ──────────────────────────────────────────────────────────────────  │
│  Scene GameObject: [none  ⊙]  [Add]                                  │
│  (opens SearchWindow scoped to this GO instead of TreeRunner)        │
└────────────────────────────────────────────────────────────────────┘
```

The "Add Binding From Scene" row is a separate section: an `ObjectField<GameObject>` + an `[Add]` button. When clicked, the button opens the same `SearchWindow` but the source is the dragged scene GameObject (and its children), not the TreeRunner's hierarchy. This handles components on unrelated GameObjects without adding them to the TreeRunner's children.

### Columns

| Column | Widget | Source |
|---|---|---|
| Component / Member (combined) | Clickable label → `SearchWindow` | Reflection over `runner.gameObject` (+ children + additional GO) |
| BB Variable | Clickable label → `PopupWindowContent` | `BlackboardDefinition` variables, singular/array filter |
| Remove | ✕ button | — |

### Empty State

When `currentRunner` is null (asset opened standalone):
> "Select a GameObject with a TreeRunner in the scene."

When `currentRunner` has a valid blackboard definition but no bindings:
> "No tracked bindings. Click [+ Add Binding] to create one."

---

## 3. Component / Member Picker

Uses `SearchWindow` with `ISearchWindowProvider` — same pattern as `NodeSearchProvider` and `TreeSearchProvider`.

### Tree Structure

```
Select Component Member                    (SearchWindow)
├── Agent                         (SearchTreeGroupEntry, lv0)
│   ├── HealthComponent           (SearchTreeGroupEntry, lv1)
│   │   ├── health    float       (SearchTreeEntry, lv2)
│   │   └── armor     float       (SearchTreeEntry, lv2)
│   └── WeaponComponent           (SearchTreeGroupEntry, lv1)
│       └── ammo      int         (SearchTreeEntry, lv2)
├── Child GameObject              (SearchTreeGroupEntry, lv0)
│   └── AiSensor                  (SearchTreeGroupEntry, lv1)
│       └── detectionRange  float (SearchTreeEntry, lv2)
└── Additional GO (if set)        (SearchTreeGroupEntry, lv0)
    └── ExternalComponent         (SearchTreeGroupEntry, lv1)
        └── someField     float   (SearchTreeEntry, lv2)
```

Three levels deep. Selecting a leaf (lv2) commits both the component and the member to the binding row in one action.

### Filtering

| Filter | Rule |
|---|---|
| Components | Only user assemblies — skip `UnityEngine.*`, `UnityEditor.*` |
| Members | Only types in `VariableTypeRegistry.Types` (BB-compatible) |
| Visibility | Public fields and properties only |
| Properties | Must have `CanRead` |

### Source GameObjects

| Add Binding flow | Source |
|---|---|
| `[+ Add Binding]` (row pulldown) | `runner.gameObject` + children + Additional GameObject (if set) |
| `[+ Add Binding From Scene]` → `[Add]` | The specific scene GameObject from the ObjectField + its children |

Empty GameObjects and UnityEngine-only GameObjects (no user components) are omitted from the tree.

---

## 4. Variable Picker

Uses `PopupWindowContent` — reuses `VariableTypeSearchPopup.uxml` for layout.

### Filtering

- Text search filters by variable name
- Singular / Array radio buttons filter by `stride == 1` or `stride > 1`
- Type compatibility badge (green = exact match, yellow = implicit convert, red = incompatible)

### Data Source

`BehaviourTreeEditor.currentBlackboardDef` — the shared definition for the currently open tree.

### Layout (from UXML)

```
┌──────────────────────┐
│ Search...            │
│ ○ Singular ● Array   │
│                      │
│ health     float     │
│ ammo       int       │
│ target     Vector3   │
│ ...                  │
└──────────────────────┘
```

---

## 5. Runtime Push

`TreeRunner` handles the push internally — no separate `AutoTrackProvider` component needed.

### Init (Start)

```csharp
void ResolveTrackedBindings()
{
    foreach (TrackedBinding binding in trackedBindings)
    {
        if (binding.targetComponent == null) continue;
        Type type = binding.targetComponent.GetType();

        if (binding.isProperty)
            binding.cachedPropertyInfo = type.GetProperty(binding.memberName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        else
            binding.cachedFieldInfo = type.GetField(binding.memberName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        // Resolve variable index from the baked blackboard definition
        binding.variableIndex = blackboard.Definition.GetVariableIndex(binding.blackboardVariableName);
    }
}
```

### Per-Frame (Update)

```csharp
void PushTrackedBindings()
{
    for (int i = 0; i < trackedBindings.Count; i++)
    {
        TrackedBinding binding = trackedBindings[i];
        if (binding.variableIndex < 0 || binding.targetComponent == null) continue;

        object value = binding.isProperty
            ? binding.cachedPropertyInfo?.GetValue(binding.targetComponent)
            : binding.cachedFieldInfo?.GetValue(binding.targetComponent);

        blackboard.Set(binding.variableIndex, value);
    }
}
```

Called after `PushDataProviders()` in `TreeRunner.Update()`.

---

## 6. Files

| File | Purpose |
|---|---|
| `Runtime/TrackedBinding.cs` | Serializable binding data struct |
| `Runtime/TreeRunner.cs` | +`trackedBindings` list, +`ResolveTrackedBindings()`, +`PushTrackedBindings()` |
| `Editor/TrackedVariablesView.cs` | Tab UI: binding rows, add/remove, ObjectField, popup triggers |
| `Editor/ComponentMemberSearchProvider.cs` | `ISearchWindowProvider` — GameObject → Component → Member tree |
| `Editor/VariableSearchPopup.cs` | `PopupWindowContent` — search + singular/array filter over existing BB variables |

### Modified Files

| File | Change |
|---|---|
| `BehaviourTreeEditor.uxml` | Wire `TrackedVariablesView` into `TrackedVariablesTab` |
| `BehaviourTreeEditor.cs` | Query `TrackedVariablesView` in `CreateGUI`, call `Refresh()` on selection change |
| `BlackboardDefinition.cs` (Core) | Add `GetVariableIndex(string name)` helper |

---

## 7. Integration Points

### BehaviourTreeEditor

```
CreateGUI()
  → trackView = root.Q<TrackedVariablesView>()
  → pass reference to tab for search-window context

OnSelectionChange()
  → trackView.Refresh(currentRunner)
```

### Traceability

- Each binding row is backed by a `TrackedBinding` in `TreeRunner.trackedBindings`
- Modifications call `EditorUtility.SetDirty(currentRunner)` and `Undo.RecordObject`
- Adding/removing bindings is undoable via the Unity undo system

---

## 8. Future Considerations

| Feature | Notes |
|---|---|
| Property setters (bi-directional) | Write computed values back from BB → component field |
| Type mismatch warnings | Warn if member type and BB variable type differ at edit time |
| Live value display | Show current value in a third column during play mode |
| Per-binding disable toggle | Skip specific bindings at runtime without removing them |
| TreeView upgrade | If SearchWindow becomes unwieldy for large hierarchies, switch component picker to TreeView |
