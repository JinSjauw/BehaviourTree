# Tier 1 File Split Plans

Detailed extraction plans for the 4 Tier-1 files. Each plan lists exact methods to move, line ranges, and the new file they go into. All splits use **partial classes** — zero risk, no API changes.

---

## 1. BlackBoard.cs → BlackBoard.cs + BlackBoard.Layout.cs + BlackBoard.Overrides.cs

**Current file**: [BlackBoard.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Core/BlackBoard.cs) — 597 lines  
**After split**: 3 files — ~230 + ~280 + ~85 lines

### BlackBoard.cs (core — stays)

Retains the class skeleton: enum, serialized fields, `Initialize`, runtime accessors, `IBlackBoardAccess` explicit implementations.

| Lines | Content |
|---|---|
| 1–6 | `using` statements + namespace open |
| 7–11 | `BlackBoardType` enum |
| 13–47 | Class declaration, all `[SerializeField]`/`[NonSerialized]` fields, `Definition`/`Storage` properties |
| 49–137 | `Initialize()` — BB setup, storage init, reference sync, value-override sync |
| 389–403 | `GetTotalSlotCount()` |
| 405–423 | `FindVariableIndex()` |
| 426–444 | `Set<T>(string)`, `Get<T>(string)` |
| 446–489 | `Get<T>(int)`, `Set<T>(int)` — the main typed accessors with `currentAgentOffset` and `serializedReferences` sync |
| 491–531 | `GetBoxed(int)`, `SetBoxed(int)`, `IBlackBoardAccess` explicit interface members |
| 596–597 | Closing brace + namespace close |

**Change**: Class keyword becomes `public partial class BlackBoard`.

---

### BlackBoard.Layout.cs (extract)

Encapsulates the serialized-reference layout management subsystem: snapshot comparison, name-based save/restore, rename detection, and reference-slot override tracking.

| Lines | Content |
|---|---|
| 1–6 | `using System; using System.Collections.Generic; using UnityEngine;` + namespace open |
| — | `public partial class BlackBoard` declaration |
| 139–212 | `BuildSerializedReferences()` — detects layout change, saves by snapshot, remaps renames, rebuilds serializedReferences |
| 214–221 | `ClearSerializedReferences()` |
| 223–237 | `HasSameVariableLayout()` — compares variable list against stored snapshot |
| 239–271 | `SaveReferencesByName()` — saves ref values keyed by variable name (from definition) |
| 273–300 | `SaveReferencesByNameFromSnapshot()` — saves ref values from last-built snapshot (pre-mutation) |
| 302–356 | `RemapRenamedReferences()` — detects renames, remaps dictionary keys old→new |
| 358–387 | `RestoreReferencesByName()` — writes saved refs back at new slot positions |
| 580–594 | `IsReferenceSlotOverridden()`, `SetReferenceSlotOverridden()`, `ClearReferenceSlotOverridden()` |
| — | Closing brace + namespace close |

**Dependencies**: Accesses `definition`, `serializedReferences`, `lastBuiltVarNames`, `lastBuiltVarStrides`, `overriddenReferenceSlots` — all fields, no methods from the main file.

---

### BlackBoard.Overrides.cs (extract)

Encapsulates the per-component value-type override CRUD.

| Lines | Content |
|---|---|
| 1–3 | `using System.Collections.Generic;` + namespace open |
| — | `public partial class BlackBoard` declaration |
| 533–535 | Section comment + blank |
| 536–545 | `GetValueOverride()` |
| 547–565 | `SetValueOverride()` |
| 567–578 | `ClearValueOverride()` |
| — | Closing brace + namespace close |

**Dependencies**: Only accesses `valueOverrides` field and `BlackboardValueOverride` type.

---

## 2. SquadDefinitionEditor.cs → SquadDefinitionEditor.cs + SquadDefinitionEditor.Roles.cs + SquadDefinitionEditor.BindingGroups.cs

**Current file**: [SquadDefinitionEditor.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Editor/SquadDefinitionEditor.cs) — 611 lines  
**After split**: 3 files — ~390 + ~150 + ~80 lines

### SquadDefinitionEditor.cs (core — stays)

Retains window lifecycle, CreateGUI, squad bar menu, scroll utilities, LoadSquad/RefreshUI/ClearUI.

| Lines | Content |
|---|---|
| 1–11 | `using` statements + namespace open + class declaration |
| 13–38 | All fields (`currentSquad`, UI element references, template references, foldout state sets) |
| 39–57 | `OpenWindow()`, `OnOpenAsset()` — menu/entry points |
| 59–128 | `CreateGUI()` — UI construction, template loading, element queries, event wiring, `BuildSquadBarMenu()` call |
| 129–211 | `RegisterNestedScrollHandling()`, `TryForwardScrollTo()`, `IsDescendantOf()` — scroll nesting utilities |
| 213–239 | `OnEnable()`, `OnDisable()`, `OnProjectChanged()`, `OnBindingsExternallyChanged()` |
| 241–292 | `OnSelectionChange()`, `LoadSquad()`, `RefreshUI()`, `ClearUI()` |
| 294–362 | `BuildSquadBarMenu()`, `CreateNewSquad()`, `BrowseOpenSquad()` |
| 364–365 | Blank (before roles section — removed) |
| 611 | Closing brace + namespace close |

**Change**: The class header becomes `public partial class SquadDefinitionEditor : EditorWindow`.

---

### SquadDefinitionEditor.Roles.cs (extract)

Encapsulates roles management: creation, UI building, colour/maxAmount/fallback editing, removal.

| Lines | Content |
|---|---|
| 1–10 | `using` statements + namespace open |
| — | `public partial class SquadDefinitionEditor` declaration |
| 381–383 | Roles section comment |
| 385–409 | `OnCreateNewSquadNavClicked()`, `OnBrowseSquadClicked()` |
| 411–418 | Blank (before OnAddRoleClicked — removed) |
| 420–445 | `OnAddRoleClicked()` |
| 447–529 | `BuildRolesUI()` — foldout state preservation, role row template instantiation, colour picker, max amount, fallback toggle, remove button |
| — | Closing brace + namespace close |

**Dependencies**: Uses `currentSquad`, `newRoleField`, `rolesList`, `roleRowTemplate`, `browseSquadButton`, `expandedRoleFoldouts` — all fields in core partial.

---

### SquadDefinitionEditor.BindingGroups.cs (extract)

Encapsulates binding group management: add via tree search, UI building with foldout state preservation.

| Lines | Content |
|---|---|
| 1–8 | `using` statements + namespace open |
| — | `public partial class SquadDefinitionEditor` declaration |
| 531–533 | Binding groups section comment |
| 535–553 | `OnAddBindingGroupClicked()` |
| 555–610 | `BuildBindingGroupsUI()` — foldout state preservation, `BindingGroupEditor` instantiation, placeholder for empty state |
| — | Closing brace + namespace close |

**Dependencies**: Uses `currentSquad`, `addBindingGroupButton`, `bindingGroupsScroll`, `bindingRowTemplate`, `bindingGroupFoldoutTemplate`, `expandedBindingGroupFoldouts`, `TreeAssetSearchProvider` — all accessible via partial class.

---

## 3. TreeBaker.cs → TreeBaker.cs + TreeBaker.FieldPacking.cs

**Current file**: [TreeBaker.cs](file:///d:/Dev/BehaviourTreeEditor/Assets/Scripts/BehaviourTree/Runtime/TreeBaker.cs) — 641 lines  
**After split**: 2 files — ~480 + ~160 lines

### TreeBaker.cs (core — stays)

Retains the main `BakeTree` orchestration, instance graph construction, scope management, node data filling, and variable resolution.

| Lines | Content |
|---|---|
| 1–9 | `using` statements + namespace open + static class declaration |
| 11–87 | `BakeTree()` — full entry point, orchestrates all subsystems |
| 89–101 | `BakedNodeInstance` struct |
| 103–108 | `GetEffectiveRoot(BehaviourNode)` |
| 110–114 | `GetEffectiveRoot(BehaviourTreeAssetBase)` |
| 116–128 | `EnsureInstance()` |
| 130–227 | `ProcessChildren()` — recursive tree walk, subtree expansion, child index assignment |
| 229–256 | `EnsureScopeMapping()` — per-subtree variable index mapping |
| 258–316 | `ResolveSubtreeVarIndex()` — binding resolution or scoped clone creation |
| 318–412 | `FillNodeData()` — builds `NodeData[]` from baked instances, calls `PackFieldEntryWithArray` |
| 414–421 | `GetScopeMap()` |
| 423–463 | `CopyGenericVariables()` |
| 465–482 | `ResolveSlotOffset()` |
| 641 | Closing brace + namespace close |

**Note**: `FillNodeData` calls `PackFieldEntryWithArray` and `CountFieldDataForNode` which live in the extracted file. This is fine — both are `private static`, in the same assembly, via partial class.

---

### TreeBaker.FieldPacking.cs (extract)

Encapsulates the field-entry-to-FieldData compilation subsystem: counting, array detection, packing with or without stride, type resolution, constant value extraction.

| Lines | Content |
|---|---|
| 1–5 | `using` statements + namespace open |
| — | `public static partial class TreeBaker` declaration |
| 484–492 | `ResolveFieldType()` |
| 494–503 | `CountFieldDataForNode()` |
| 505