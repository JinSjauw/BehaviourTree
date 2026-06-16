using System;
using System.Collections.Generic;
using System.Linq;
using BehaviourTree.Core;
using BehaviourTree.Editor;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;

[UxmlElement("BlackBoardView")]
public partial class BlackBoardView : VisualElement
{
    private VisualElement blackBoardViewContainer;
    private SerializedObject cachedSerializedObject;
    private BlackboardDefinition cachedDefinition;
    private HashSet<string> previousVariableNames = new();
    private Dictionary<string, string> previousVariableTypes = new();
    private bool typeSyncDone;
    private Vector2 scrollPos;

    // Creator fields
    private TextField creatorNameField;
    private Button creatorButton;
    private VisualElement creatorRow;

    private ReorderableList reorderableList;

    public BlackBoardView()
    {
        style.flexGrow = 1;
        style.paddingLeft = 8;
        style.paddingRight = 8;
        style.paddingTop = 8;
        style.paddingBottom = 8;
        style.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 1f);

        blackBoardViewContainer = new VisualElement { style = { flexGrow = 1 } };
        Add(blackBoardViewContainer);

        var placeholder = new Label("Add a blackboard definition")
        {
            style =
            {
                color = Color.grey,
                unityTextAlign = TextAnchor.MiddleCenter,
                marginTop = 40,
                fontSize = 13
            }
        };
        blackBoardViewContainer.Add(placeholder);
    }

    public void BuildBlackboardView(BlackboardDefinition blackboardDefinition)
    {
        cachedDefinition = blackboardDefinition;
        previousVariableNames.Clear();
        previousVariableTypes.Clear();
        typeSyncDone = false;
        blackBoardViewContainer.Clear();

        if (cachedSerializedObject == null || cachedSerializedObject.targetObject != blackboardDefinition)
        {
            cachedSerializedObject?.Dispose();
            cachedSerializedObject = blackboardDefinition != null ? new SerializedObject(blackboardDefinition) : null;
        }

        // Clean up any pre-existing null holes in the [SerializeReference] list
        RemoveNullHoles();

        if (cachedDefinition != null)
        {
            BuildCreatorUI();

            // Separator
            VisualElement separator = new VisualElement
            {
                style =
                {
                    height = 1,
                    backgroundColor = new Color(0.4f, 0.4f, 0.4f, 0.6f),
                    marginTop = 6,
                    marginBottom = 6,
                    flexShrink = 0
                }
            };
            blackBoardViewContainer.Add(separator);
        }

        IMGUIContainer imgui = new IMGUIContainer(() =>
        {
            SerializedObject so = cachedSerializedObject;
            if (so == null) return;
            so.Update();
            SerializedProperty varsProp = so.FindProperty("sharedVariables");

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            if (reorderableList == null)
            {
                reorderableList = new ReorderableList(varsProp.serializedObject, varsProp, true, false, false, false)
                {
                    drawHeaderCallback = null,
                    elementHeightCallback = index =>
                    {
                        SerializedProperty sp = reorderableList.serializedProperty;
                        if (index < 0 || index >= sp.arraySize) return EditorGUIUtility.singleLineHeight + 10f;
                        return EditorGUI.GetPropertyHeight(sp.GetArrayElementAtIndex(index), true) + 10f;
                    },
                    drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
                    {
                        SerializedProperty sp = reorderableList.serializedProperty;
                        if (index < 0 || index >= sp.arraySize) return;
                        SerializedProperty element = sp.GetArrayElementAtIndex(index);

                        Rect contentRect = new Rect(rect.x, rect.y + 4f, rect.width - 28f, rect.height - 8f);
                        EditorGUI.PropertyField(contentRect, element, GUIContent.none, true);

                        Rect buttonRect = new Rect(rect.x + rect.width - 24f, rect.y + 4f, 22f, 18f);
                        Color prevColor = GUI.color;
                        GUI.color = Color.softRed;
                        if (GUI.Button(buttonRect, "X"))
                        {
                            Undo.RecordObject(sp.serializedObject.targetObject, "Remove Variable");
                            sp.DeleteArrayElementAtIndex(index);
                            sp.serializedObject.ApplyModifiedProperties();
                        }
                        GUI.color = prevColor;
                    },
                    drawElementBackgroundCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
                    {
                        if (Event.current.type == EventType.Repaint)
                        {
                            Rect bgRect = new Rect(rect.x, rect.y + 1f, rect.width, rect.height - 3f);
                            EditorGUI.DrawRect(bgRect, new Color(0.16f, 0.16f, 0.16f, 1f));
                        }
                    },
                    footerHeight = 0f,
                };
            }
            reorderableList.serializedProperty = varsProp;

            reorderableList.DoLayoutList();
            EditorGUILayout.EndScrollView();

            // Capture before ApplyModifiedProperties (which resets the flag).
            bool hasChanges = so.hasModifiedProperties;
            so.ApplyModifiedProperties();

            if (hasChanges)
            {
                HandleRenames(varsProp);
                HandleTypeChanges(varsProp);
            }
        });

        imgui.style.flexGrow = 1;
        imgui.style.flexShrink = 1;
        imgui.style.overflow = Overflow.Hidden;
        blackBoardViewContainer.Add(imgui);
    }

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

        // ── Row: Name │ Add ───────────────────────────────────────────
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

        creatorNameField = new TextField { style = { flexGrow = 1, marginRight = 4 }, value = "newVariable" };

        creatorButton = new Button(() => OpenVariableTypePopup())
        {
            text = "Add",
            style =
            {
                width = 60,
                height = 21,
                flexShrink = 0
            }
        };

        creatorRow.Add(creatorNameField);
        creatorRow.Add(creatorButton);

        blackBoardViewContainer.Add(creatorRow);
    }

    /// <summary>
    /// Removes any null entries from the [SerializeReference] sharedVariables list.
    /// Null holes can exist from legacy deletion code that only called DeleteArrayElementAtIndex once.
    /// </summary>
    private void RemoveNullHoles()
    {
        if (cachedDefinition?.sharedVariables == null) return;

        List<BlackboardVariableBase> list = cachedDefinition.sharedVariables;
        bool foundNull = false;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (list[i] == null)
            {
                list.RemoveAt(i);
                foundNull = true;
            }
        }

        if (foundNull)
        {
            EditorUtility.SetDirty(cachedDefinition);
            cachedSerializedObject?.Dispose();
            cachedSerializedObject = new SerializedObject(cachedDefinition);
        }
    }

    private void OpenVariableTypePopup()
    {
        // worldBound returns window-space coordinates including the window header
        // — PopupWindow.Show accepts them directly, per Unity docs.
        VariableTypeSearchPopup popup = new VariableTypeSearchPopup((Type type, bool isArray, int stride) =>
        {
            CreateVariable(type, isArray, stride);
        });
        UnityEditor.PopupWindow.Show(creatorButton.worldBound, popup);
    }

    private void CreateVariable(Type selectedType, bool isArray, int stride)
    {
        if (cachedDefinition == null) return;

        string name = creatorNameField.value?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            Debug.LogWarning("[BlackBoardView] Variable name cannot be empty.");
            return;
        }

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

    private void HandleRenames(SerializedProperty varsProp)
    {
        HashSet<string> currentNames = SnapshotNameSet(varsProp);

        // Only propagate renames if the sets actually differ (not just a reorder)
        if (!currentNames.SetEquals(previousVariableNames))
        {
            List<string> removed = previousVariableNames.Except(currentNames).ToList();
            List<string> added = currentNames.Except(previousVariableNames).ToList();

            // Pair up removed and added names — these are likely renames
            int pairCount = Math.Min(removed.Count, added.Count);
            for (int i = 0; i < pairCount; i++)
            {
                PropagateRename(removed[i], added[i]);
            }
        }

        previousVariableNames = currentNames;
    }

    private static HashSet<string> SnapshotNameSet(SerializedProperty varsProp)
    {
        var names = new HashSet<string>();
        for (int i = 0; i < varsProp.arraySize; i++)
        {
            SerializedProperty varProp = varsProp.GetArrayElementAtIndex(i);
            SerializedProperty nameProp = varProp.FindPropertyRelative("variableName");
            if (nameProp != null && !string.IsNullOrEmpty(nameProp.stringValue))
                names.Add(nameProp.stringValue);
        }
        return names;
    }

    private void PropagateRename(string oldName, string newName)
    {
        string[] guids = AssetDatabase.FindAssets("t:BehaviourTreeAssetBase");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            BehaviourTreeAssetBase tree = AssetDatabase.LoadAssetAtPath<BehaviourTreeAssetBase>(path);
            if (tree == null || tree.BlackboardDefinition != cachedDefinition) continue;

            UpdateTreeNodes(tree, oldName, newName);
        }
    }

    private static void UpdateTreeNodes(BehaviourTreeAssetBase treeAsset, string oldName, string newName)
    {
        string assetPath = AssetDatabase.GetAssetPath(treeAsset);
        if (string.IsNullOrEmpty(assetPath)) return;

        UnityEngine.Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);

        foreach (var obj in subAssets)
        {
            bool changed = false;

            if (obj is LeafNode leaf && leaf.fieldEntries != null)
            {
                Undo.RecordObject(leaf, "Rename Blackboard Variable");
                for (int i = 0; i < leaf.fieldEntries.Count; i++)
                {
                    NodeFieldEntry entry = leaf.fieldEntries[i];
                    if (entry.variableName == oldName)
                    {
                        entry.variableName = newName;
                        leaf.fieldEntries[i] = entry;
                        changed = true;
                    }
                }
            }
            else if (obj is DecoratorNode decorator && decorator.fieldEntries != null)
            {
                Undo.RecordObject(decorator, "Rename Blackboard Variable");
                for (int i = 0; i < decorator.fieldEntries.Count; i++)
                {
                    NodeFieldEntry entry = decorator.fieldEntries[i];
                    if (entry.variableName == oldName)
                    {
                        entry.variableName = newName;
                        decorator.fieldEntries[i] = entry;
                        changed = true;
                    }
                }
            }
            else if (obj is SubtreeNode subtree && subtree.bindings != null)
            {
                Undo.RecordObject(subtree, "Rename Blackboard Variable");
                for (int i = 0; i < subtree.bindings.Count; i++)
                {
                    SubtreeBinding binding = subtree.bindings[i];
                    if (binding.parentVariableName == oldName)
                    {
                        binding.parentVariableName = newName;
                        subtree.bindings[i] = binding;
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                EditorUtility.SetDirty(obj);
            }
        }
    }

    private void HandleTypeChanges(SerializedProperty varsProp)
    {
        Dictionary<string, string> currentTypes = SnapshotTypeMap(varsProp);

        // First time opening — do a full sync of all variable types to catch stale fieldEntry types
        if (!typeSyncDone && currentTypes.Count > 0)
        {
            typeSyncDone = true;
            foreach (var kvp in currentTypes)
                PropagateTypeChange(kvp.Key, kvp.Value);
        }
        else
        {
            // Name-based comparison — immune to reordering
            foreach (var kvp in currentTypes)
            {
                string name = kvp.Key;
                string newType = kvp.Value;
                if (previousVariableTypes.TryGetValue(name, out string oldType) && oldType != newType)
                    PropagateTypeChange(name, newType);
            }
        }

        previousVariableTypes = currentTypes;
    }

    private static Dictionary<string, string> SnapshotTypeMap(SerializedProperty varsProp)
    {
        var types = new Dictionary<string, string>();
        for (int i = 0; i < varsProp.arraySize; i++)
        {
            SerializedProperty varProp = varsProp.GetArrayElementAtIndex(i);
            SerializedProperty nameProp = varProp.FindPropertyRelative("variableName");
            SerializedProperty typeProp = varProp.FindPropertyRelative("variableTypeName");
            if (nameProp != null && typeProp != null && !string.IsNullOrEmpty(nameProp.stringValue))
                types[nameProp.stringValue] = typeProp.stringValue;
        }
        return types;
    }

    private void PropagateTypeChange(string variableName, string newTypeName)
    {
        string[] guids = AssetDatabase.FindAssets("t:BehaviourTreeAssetBase");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            BehaviourTreeAssetBase tree = AssetDatabase.LoadAssetAtPath<BehaviourTreeAssetBase>(path);
            if (tree == null || tree.BlackboardDefinition != cachedDefinition) continue;

            UpdateTreeNodesType(tree, variableName, newTypeName);
        }
    }

    private static void UpdateTreeNodesType(BehaviourTreeAssetBase treeAsset, string variableName, string newTypeName)
    {
        string assetPath = AssetDatabase.GetAssetPath(treeAsset);
        if (string.IsNullOrEmpty(assetPath)) return;

        UnityEngine.Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);

        foreach (var obj in subAssets)
        {
            bool changed = false;

            if (obj is LeafNode leaf && leaf.fieldEntries != null)
            {
                Undo.RecordObject(leaf, "Update Variable Type");
                for (int i = 0; i < leaf.fieldEntries.Count; i++)
                {
                    NodeFieldEntry entry = leaf.fieldEntries[i];
                    if (entry.variableName == variableName && entry.isVariable)
                    {
                        entry.fieldTypeName = newTypeName;
                        leaf.fieldEntries[i] = entry;
                        changed = true;
                    }
                }
            }
            else if (obj is DecoratorNode decorator && decorator.fieldEntries != null)
            {
                Undo.RecordObject(decorator, "Update Variable Type");
                for (int i = 0; i < decorator.fieldEntries.Count; i++)
                {
                    NodeFieldEntry entry = decorator.fieldEntries[i];
                    if (entry.variableName == variableName && entry.isVariable)
                    {
                        entry.fieldTypeName = newTypeName;
                        decorator.fieldEntries[i] = entry;
                        changed = true;
                    }
                }
            }
            else if (obj is CompositeNode composite && composite.fieldEntries != null)
            {
                Undo.RecordObject(composite, "Update Variable Type");
                for (int i = 0; i < composite.fieldEntries.Count; i++)
                {
                    NodeFieldEntry entry = composite.fieldEntries[i];
                    if (entry.variableName == variableName && entry.isVariable)
                    {
                        entry.fieldTypeName = newTypeName;
                        composite.fieldEntries[i] = entry;
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                EditorUtility.SetDirty(obj);
            }
        }
    }
}
