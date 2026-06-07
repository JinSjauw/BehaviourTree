using System;
using System.Collections.Generic;
using BehaviourTree.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[UxmlElement("BlackBoardView")]
public partial class BlackBoardView : VisualElement
{
    private VisualElement blackBoardViewContainer;
    private SerializedObject cachedSerializedObject;
    private BlackboardDefinition cachedDefinition;
    private Dictionary<int, string> previousVariableNames = new();

    private Vector2 scrollPos;

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
        blackBoardViewContainer.Clear();

        if (cachedSerializedObject == null || cachedSerializedObject.targetObject != blackboardDefinition)
        {
            cachedSerializedObject?.Dispose();
            cachedSerializedObject = blackboardDefinition != null ? new SerializedObject(blackboardDefinition) : null;
        }

        IMGUIContainer imgui = new IMGUIContainer(() =>
        {
            SerializedObject so = cachedSerializedObject;
            if (so == null) return;
            so.Update();
            SerializedProperty varsProp = so.FindProperty("sharedVariables");

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            EditorGUILayout.PropertyField(varsProp, includeChildren: true);
            EditorGUILayout.EndScrollView();

            so.ApplyModifiedProperties();

            HandleRenames(varsProp);
        });

        blackBoardViewContainer.Add(imgui);
    }

    private void HandleRenames(SerializedProperty varsProp)
    {
        Dictionary<int, string> currentNames = SnapshotVariableNames(varsProp);

        foreach (var kvp in currentNames)
        {
            int index = kvp.Key;
            string newName = kvp.Value;

            if (previousVariableNames.TryGetValue(index, out string oldName) &&
                oldName != newName &&
                !string.IsNullOrEmpty(oldName) &&
                !string.IsNullOrEmpty(newName))
            {
                PropagateRename(oldName, newName);
            }
        }

        previousVariableNames = currentNames;
    }

    private static Dictionary<int, string> SnapshotVariableNames(SerializedProperty varsProp)
    {
        var names = new Dictionary<int, string>();
        for (int i = 0; i < varsProp.arraySize; i++)
        {
            SerializedProperty varProp = varsProp.GetArrayElementAtIndex(i);
            SerializedProperty nameProp = varProp.FindPropertyRelative("name");
            names[i] = nameProp.stringValue;
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
}