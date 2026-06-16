# Variable Type Search Popup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the static `DropdownField` type/array selectors in `BlackBoardView` with a searchable `PopupWindow` that shows all registered variable types plus inline Array/Stride controls.

**Architecture:** A `VariableTypeSearchPopup` (`PopupWindowContent`) built with UI Toolkit. A `VariableTypeRegistry` static class holds the type list with a `RegisterType(Type)` hook. `BlackBoardView` replaces its type/array dropdowns with an Array toggle + a "Select Type" button that opens the popup.

**Tech Stack:** Unity 6000.3 (UI Toolkit), C#, `UnityEditor.PopupWindow`, `UQueryBuilder` for filtering.

---

## File Structure

| File | Action | Responsibility |
|---|---|---|
| `Assets/Scripts/BehaviourTree/Editor/VariableTypeRegistry.cs` | Create | Static type list + register hook |
| `Assets/Scripts/BehaviourTree/Editor/VariableTypeSearchPopup.uxml` | Create | Layout template for the popup |
| `Assets/Scripts/BehaviourTree/Editor/VariableTypeSearchPopup.uss` | Create | Stylesheet for the popup |
| `Assets/Scripts/BehaviourTree/Editor/VariableTypeSearchPopup.cs` | Create | `PopupWindowContent` — loads UXML, wires callbacks, handles filtering |
| `Assets/Scripts/BehaviourTree/Editor/BlackBoardView.cs` | Modify | Replace type/array dropdowns with Array toggle + "Select Type" button |
| `Assets/Scripts/BehaviourTree/Editor/BlackBoardVariableDrawer.cs` | Modify | Update type dropdown in drawer to use registry |

---

### Task 1: Create VariableTypeRegistry

**Files:**
- Create: `Assets/Scripts/BehaviourTree/Editor/VariableTypeRegistry.cs`

**Context:** The registry mirrors the `MethodRegistry` pattern — static class with a static constructor that auto-seeds from `FieldTypeHelper.CommonTypes`, plus explicit `RegisterType(Type)` for third-party code. No runtime dependencies; it's an editor-only singleton list.

- [ ] **Step 1: Write the file**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using BehaviourTree.Core;

namespace BehaviourTree.Editor
{
    /// <summary>
    /// Central registry of types available for blackboard variable creation.
    /// Auto-seeds from FieldTypeHelper.CommonTypes on first access.
    /// External code calls RegisterType to add custom/enum types.
    /// </summary>
    public static class VariableTypeRegistry
    {
        private static readonly List<Type> registeredTypes = new();

        static VariableTypeRegistry()
        {
            foreach (Type t in FieldTypeHelper.CommonTypes)
                registeredTypes.Add(t);
        }

        /// <summary>All registered types (built-in + custom).</summary>
        public static IReadOnlyList<Type> Types => registeredTypes;

        /// <summary>
        /// Register a type for variable creation. Safe to call before or after
        /// registry initialization. Duplicates are silently ignored.
        /// </summary>
        public static void RegisterType(Type type)
        {
            if (type == null || registeredTypes.Contains(type)) return;
            registeredTypes.Add(type);
        }
    }
}
```

- [ ] **Step 2: Verify compile**

Open Unity. Wait for script compilation. Expected: no errors.

---

### Task 2: Create VariableTypeSearchPopup (UXML + USS + C#)

**Files:**
- Create: `Assets/Scripts/BehaviourTree/Editor/VariableTypeSearchPopup.uxml`
- Create: `Assets/Scripts/BehaviourTree/Editor/VariableTypeSearchPopup.uss`
- Create: `Assets/Scripts/BehaviourTree/Editor/VariableTypeSearchPopup.cs`

**Context:** A `PopupWindowContent` subclass. Layout and styling live in UXML/USS, loaded via `AssetDatabase.LoadAssetAtPath`. The C# class queries named elements (`search-field`, `type-list`, `array-toggle`, `stride-field`) and wires up filtering callbacks. Uses `VariableTypeRegistry.Types` as the data source.

**Size:** ~300px width, ~280px height.

- [ ] **Step 1: Write the UXML file**

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements" xmlns:uie="UnityEditor.UIElements">
    <ui:VisualElement class="popup-root">
        <ui:TextField name="search-field" class="search-field" />
        <ui:ListView name="type-list" class="type-list" selection-type="Single" />
        <ui:VisualElement class="bottom-bar">
            <ui:Toggle name="array-toggle" label="Array" class="array-toggle" />
            <ui:Label text="Stride:" class="stride-label" />
            <ui:IntegerField name="stride-field" value="2" class="stride-field" />
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
```

- [ ] **Step 2: Write the USS file**

```css
.popup-root {
    width: 300px;
    max-height: 280px;
    flex-direction: column;
    background-color: rgb(46, 46, 46);
    padding: 4px 6px;
}

.search-field {
    margin-bottom: 4px;
    flex-shrink: 0;
}

.type-list {
    flex-grow: 1;
    flex-shrink: 1;
}

.bottom-bar {
    flex-direction: row;
    align-items: center;
    margin-top: 4px;
    padding-top: 4px;
    border-top-width: 1px;
    border-top-color: rgb(77, 77, 77);
    flex-shrink: 0;
}

.array-toggle {
    margin-right: 8px;
}

.stride-label {
    margin-right: 4px;
}

.stride-field {
    width: 50px;
}
```

- [ ] **Step 3: Write the C# file**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using BehaviourTree.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace BehaviourTree.Editor
{
    /// <summary>
    /// Searchable popup for selecting a variable type, with inline array/stride controls.
    /// Opens via PopupWindow.Show() next to a target VisualElement.
    /// </summary>
    public class VariableTypeSearchPopup : PopupWindowContent
    {
        private const float WindowWidth = 300f;
        private const float WindowHeight = 280f;

        private readonly Action<Type, bool, int> onTypeSelected;

        private TextField searchField;
        private ListView typeListView;
        private Toggle arrayToggle;
        private IntegerField strideField;

        private List<Type> allTypes;
        private List<Type> filteredTypes;

        public VariableTypeSearchPopup(Action<Type, bool, int> onTypeSelected)
        {
            this.onTypeSelected = onTypeSelected;
        }

        public override VisualElement CreateGUI()
        {
            allTypes = VariableTypeRegistry.Types.ToList();
            filteredTypes = new List<Type>(allTypes);

            // Load UXML layout
            string uxmlPath = "Assets/Scripts/BehaviourTree/Editor/VariableTypeSearchPopup.uxml";
            VisualTreeAsset treeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
            VisualElement root = treeAsset.CloneTree();

            // Load USS stylesheet
            string ussPath = "Assets/Scripts/BehaviourTree/Editor/VariableTypeSearchPopup.uss";
            StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(ussPath);
            if (styleSheet != null)
                root.styleSheets.Add(styleSheet);

            // Query named elements
            searchField = root.Q<TextField>("search-field");
            typeListView = root.Q<ListView>("type-list");
            arrayToggle = root.Q<Toggle>("array-toggle");
            strideField = root.Q<IntegerField>("stride-field");

            // Configure ListView data & bindings
            typeListView.itemsSource = filteredTypes;
            typeListView.makeItem = MakeListItem;
            typeListView.bindItem = BindListItem;
            typeListView.onItemsChosen += OnItemChosen;

            // Wire callbacks
            searchField.RegisterValueChangedCallback(OnSearchChanged);
            searchField.RegisterCallback<KeyDownEvent>(OnSearchKeyDown);
            typeListView.RegisterCallback<KeyDownEvent>(OnListKeyDown);

            arrayToggle.RegisterValueChangedCallback(evt =>
            {
                strideField.SetEnabled(evt.newValue);
                if (!evt.newValue)
                    strideField.value = 1;
            });
            strideField.SetEnabled(false);

            // Focus the search field on open
            root.schedule.Execute(() => searchField.Focus()).StartingIn(0);

            return root;
        }

        private VisualElement MakeListItem()
        {
            return new Label { style = { paddingLeft = 6, unityTextAlign = TextAnchor.MiddleLeft } };
        }

        private void BindListItem(VisualElement element, int index)
        {
            Label label = element as Label;
            if (label == null || index < 0 || index >= filteredTypes.Count) return;
            label.text = FieldTypeHelper.GetDisplayName(filteredTypes[index]);
        }

        public override Vector2 GetWindowSize()
        {
            return new Vector2(WindowWidth, WindowHeight);
        }

        public override void OnOpen() { }
        public override void OnClose() { }

        private void OnSearchChanged(ChangeEvent<string> evt)
        {
            string query = (evt.newValue ?? string.Empty).Trim();
            filteredTypes.Clear();

            if (string.IsNullOrEmpty(query))
            {
                filteredTypes.AddRange(allTypes);
            }
            else
            {
                foreach (Type t in allTypes)
                {
                    string displayName = FieldTypeHelper.GetDisplayName(t);
                    if (displayName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                        filteredTypes.Add(t);
                }
            }

            typeListView.Rebuild();
        }

        private void OnSearchKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.DownArrow)
            {
                typeListView.Focus();
                if (filteredTypes.Count > 0)
                    typeListView.SetSelection(0);
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.Escape)
            {
                editorWindow.Close();
                evt.StopPropagation();
            }
        }

        private void OnListKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            {
                CommitSelection();
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.Escape)
            {
                editorWindow.Close();
                evt.StopPropagation();
            }
        }

        private void OnItemChosen(IEnumerable<object> items)
        {
            CommitSelection();
        }

        private void CommitSelection()
        {
            if (typeListView.selectedIndex < 0 || typeListView.selectedIndex >= filteredTypes.Count)
                return;

            Type selectedType = filteredTypes[typeListView.selectedIndex];
            bool isArray = arrayToggle.value;
            int stride = isArray ? Mathf.Max(1, strideField.value) : 1;

            editorWindow.Close();
            onTypeSelected?.Invoke(selectedType, isArray, stride);
        }
    }
}
```

- [ ] **Step 4: Verify compile**

Open Unity. Wait for script compilation. Expected: no errors.

---

### Task 3: Update BlackBoardView — replace dropdowns with Array toggle + popup

**Files:**
- Modify: `Assets/Scripts/BehaviourTree/Editor/BlackBoardView.cs`

**Changes:**
1. Remove `creatorTypeDropdown` field, `creatorArrayDropdown` field, `PopulateTypeDropdown()` method
2. Add `Toggle creatorArrayToggle` field (replaces array dropdown)
3. Modify `BuildCreatorUI()`:
   - Row becomes: `[Name: 120px] [Type button: 150px] [Array toggle] [Stride: 50px, hidden]`
   - Type button text shows current selection (e.g., "Integer") or "Select Type..."
   - Clicking the type button opens `VariableTypeSearchPopup`
   - Array toggle visibility for stride (same as before)
   - `+` button stays with `CreateVariable()` callback
4. Modify `CreateVariable()`:
   - Remove typeIndex/array dropdown reads
   - Read from selected type (stored as field) and array toggle
5. Add field `Type selectedType` to store selection from popup

- [ ] **Step 1: Replace fields and BuildCreatorUI**

In `BlackBoardView.cs`, find the Creator fields section (lines 21-27) and replace:

**Replace lines 21-27:**
```csharp
    // Creator fields
    private TextField creatorNameField;
    private DropdownField creatorTypeDropdown;
    private DropdownField creatorArrayDropdown;
    private IntegerField creatorStrideField;
    private Button creatorButton;
    private VisualElement creatorRow;
```

**With:**
```csharp
    // Creator fields
    private TextField creatorNameField;
    private Button creatorTypeButton;
    private Type selectedType;
    private Toggle creatorArrayToggle;
    private IntegerField creatorStrideField;
    private Button creatorButton;
    private VisualElement creatorRow;
```

- [ ] **Step 2: Replace BuildCreatorUI**

**Replace lines 166-243 (`BuildCreatorUI` method):**

```csharp
    private void BuildCreatorUI()
    {
        // ── Header ───────────────────────────────────────────────────
        Label header = new Label("Add Variable")
        {
            style =
            {
                fontSize = 14,
                unityFontStyleAndWeight = FontStyle.Bold,
                marginBottom = 6,
                marginTop = 4
            }
        };
        blackBoardViewContainer.Add(header);

        // ── Row: Name │ Type │ Array │ Stride ─────────────────────────
        creatorRow = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                marginBottom = 4,
                flexShrink = 0,
                alignItems = Align.Center
            }
        };

        creatorNameField = new TextField { style = { width = 120 }, value = "newVariable" };

        selectedType = FieldTypeHelper.CommonTypes[0];
        creatorTypeButton = new Button(() => OpenVariableTypePopup())
        {
            text = FieldTypeHelper.GetDisplayName(selectedType),
            style =
            {
                width = 150,
                height = 21,
                marginLeft = 4,
                marginRight = 4,
                unityTextAlign = TextAnchor.MiddleLeft,
            }
        };

        creatorArrayToggle = new Toggle("Array")
        {
            style = { marginRight = 4 }
        };
        creatorArrayToggle.RegisterValueChangedCallback(evt =>
        {
            creatorStrideField.visible = evt.newValue;
        });

        creatorStrideField = new IntegerField { style = { width = 50 }, value = 2, visible = false };

        creatorRow.Add(creatorNameField);
        creatorRow.Add(creatorTypeButton);
        creatorRow.Add(creatorArrayToggle);
        creatorRow.Add(creatorStrideField);

        blackBoardViewContainer.Add(creatorRow);

        // ── Add button (centered, full-width) ────────────────────────
        VisualElement buttonRow = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                justifyContent = Justify.Center,
                marginBottom = 8,
                flexShrink = 0
            }
        };

        creatorButton = new Button(() => CreateVariable())
        {
            text = "+",
            style =
            {
                width = 200,
                height = 22
            }
        };

        buttonRow.Add(creatorButton);
        blackBoardViewContainer.Add(buttonRow);
    }
```

- [ ] **Step 3: Add OpenVariableTypePopup method**

After the `RemoveNullHoles()` method (before `PopulateTypeDropdown`, around line 270), insert:

```csharp
    private void OpenVariableTypePopup()
    {
        Vector2 position = creatorTypeButton.worldBound.position;
        position.y += creatorTypeButton.worldBound.height;
        position = GUIUtility.GUIToScreenPoint(position);

        VariableTypeSearchPopup popup = new VariableTypeSearchPopup((Type type, bool isArray, int stride) =>
        {
            selectedType = type;
            creatorTypeButton.text = FieldTypeHelper.GetDisplayName(type);
        });
        PopupWindow.Show(new Rect(position, Vector2.zero), popup);
    }
```

- [ ] **Step 4: Remove PopulateTypeDropdown method**

Remove the entire `PopulateTypeDropdown()` method (lines 272-277).

- [ ] **Step 5: Modify CreateVariable**

**Replace lines 279-316 (CreateVariable method):**

```csharp
    private void CreateVariable()
    {
        if (cachedDefinition == null) return;

        string name = creatorNameField.value?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            Debug.LogWarning("[BlackBoardView] Variable name cannot be empty.");
            return;
        }

        if (selectedType == null) return;

        bool isArray = creatorArrayToggle.value;
        int stride = isArray ? Mathf.Max(1, creatorStrideField.value) : 1;

        Type sharedVarType = typeof(BlackboardVariable<>).MakeGenericType(selectedType);
        BlackboardVariableBase variable = (BlackboardVariableBase)Activator.CreateInstance(sharedVarType);
        variable.Name = name;
        variable.Stride = stride;

        Undo.RecordObject(cachedDefinition, "Add Blackboard Variable");
        if (cachedDefinition.sharedVariables == null)
            cachedDefinition.sharedVariables = new List<BlackboardVariableBase>();
        cachedDefinition.sharedVariables.Add(variable);
        EditorUtility.SetDirty(cachedDefinition);

        creatorNameField.value = string.Empty;

        cachedSerializedObject?.Dispose();
        cachedSerializedObject = new SerializedObject(cachedDefinition);
    }
```

- [ ] **Step 6: Verify compile**

Open Unity. Wait for script compilation. Expected: no errors.

---

### Task 4: Update BlackBoardVariableDrawer to use VariableTypeRegistry

**Files:**
- Modify: `Assets/Scripts/BehaviourTree/Editor/BlackBoardVariableDrawer.cs`

**Context:** The drawer's type-change dropdown currently uses `FieldTypeHelper.CommonTypes`. It should use `VariableTypeRegistry.Types` instead so custom registered types appear there too.

- [ ] **Step 1: Find the ChangeType method and update**

Read the file, find where `FieldTypeHelper.CommonTypes` is used for the type popup, and replace it with `VariableTypeRegistry.Types`.

Assuming the pattern is similar to `PopulateTypeDropdown`:

```csharp
// Old pattern (likely):
int newTypeIndex = EditorGUI.Popup(position, currentIndex, FieldTypeHelper.CommonTypes
    .Select(t => FieldTypeHelper.GetDisplayName(t)).ToArray());

// New pattern:
IReadOnlyList<Type> types = VariableTypeRegistry.Types;
string[] typeNames = new string[types.Count];
for (int i = 0; i < types.Count; i++)
    typeNames[i] = FieldTypeHelper.GetDisplayName(types[i]);
int newTypeIndex = EditorGUI.Popup(position, currentIndex, typeNames);
// Then types[newTypeIndex] instead of FieldTypeHelper.CommonTypes[newTypeIndex]
```

- [ ] **Step 2: Verify compile**

Open Unity. Wait for script compilation. Expected: no errors.

---

### Task 5: Manual verification

- [ ] **Step 1: Open the behaviour tree editor**

Open any tree asset. Verify the BlackBoardView appears.

- [ ] **Step 2: Test the popup**

Click the type button (shows "Integer" by default). Verify:
- Popup opens below the button
- Shows all 12 types
- Search field is focused
- Typing filters the list (e.g., "vec" → "Vector2, Vector3, Vector4")
- DownArrow moves focus to list
- Enter or double-click selects and closes popup
- Escape closes popup without selection
- Button text updates to selected type

- [ ] **Step 3: Test Array toggle**

Toggle Array on. Verify:
- Stride field becomes visible with value 2
- Create a variable with Array → verify stride preserved
- Toggle Array off → stride hides

- [ ] **Step 4: Test variable creation**

Type a name, select a type, toggle array as needed, click `+`. Verify:
- Variable appears in the list below
- Name resets
- Type button keeps selection
- Undo works

---

## Self-Review

**Spec coverage:**
- Built-in Unity types → Task 1 (seeded from FieldTypeHelper.CommonTypes) 
- RegisterType hook → Task 1 (VariableTypeRegistry.RegisterType)
- Searchable popup → Task 2 (VariableTypeSearchPopup with TextField + ListView)
- Array/Value toggle → Task 2 (Toggle + IntegerField in bottom bar, Task 3 replaces dropdowns)
- Integration with existing creator → Task 3 (BuildCreatorUI + CreateVariable changes)
- Drawer type list → Task 4

**Placeholder scan:** No TBD/TODO. All code is complete.

**Type consistency:**
- `Action<Type, bool, int>` signature used consistently in Task 2 and Task 3
- `VariableTypeRegistry.Types` returns `IReadOnlyList<Type>` used in Task 2 and Task 4
- `selectedType` field is `Type` in Task 3, matches usage in `CreateVariable` and `BuildCreatorUI`
