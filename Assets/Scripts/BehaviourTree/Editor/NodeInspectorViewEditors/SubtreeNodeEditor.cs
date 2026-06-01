using System;
using System.Collections.Generic;
using BehaviourTree.Core;
using UnityEditor;
using UnityEngine;

namespace BehaviourTree.Editor
{
    [CustomEditor(typeof(SubtreeNode))]
    public class SubtreeNodeEditor : UnityEditor.Editor
    {
        private SerializedProperty subTreeAssetProp;
        private SerializedProperty bindingsProp;
        private GUIStyle richLabelStyle;
        private Vector2 scrollPos;
        private bool isCreatingSubtree;
        private List<string> matchingOptions;

        private GUIStyle RichTextLabelStyle
        {
            get
            {
                if (richLabelStyle == null) richLabelStyle = new GUIStyle(EditorStyles.label) { richText = true };
                return richLabelStyle;
            }
        }

        private void OnEnable()
        {
            if (target == null) return;
            
            matchingOptions = new List<string>();

            subTreeAssetProp = serializedObject.FindProperty("subTreeAsset");
            bindingsProp = serializedObject.FindProperty("bindings");
            SubtreeCycleValidator.InvalidateCache();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            EditorGUILayout.BeginVertical("box");

            Rect totalRect = EditorGUILayout.GetControlRect();
            Rect fieldRect = EditorGUI.PrefixLabel(totalRect, new GUIContent("Subtree Asset"));
            UnityEngine.Object current = subTreeAssetProp.objectReferenceValue;
            UnityEngine.Object next = EditorGUI.ObjectField(fieldRect, current, typeof(BehaviourTreeAsset), false);
            if (next != current)
            {
                BehaviourTreeAssetBase candidate = next as BehaviourTreeAssetBase;
                if (candidate != null && SubtreeCycleValidator.WouldCreateCycle(candidate, BehaviourTreeEditor.currentTree))
                {
                    Debug.LogWarning($"[SubtreeNode] Cannot assign '{candidate.DisplayName}' — it would create a cyclical reference.");
                }
                else
                {
                    subTreeAssetProp.objectReferenceValue = next;
                    bindingsProp.ClearArray();
                    SubtreeCycleValidator.InvalidateCache();
                }
            }

            BehaviourTreeAsset subtreeAsset = subTreeAssetProp.objectReferenceValue as BehaviourTreeAsset;

            if (subtreeAsset != null)
            {
                Rect buttonRect = EditorGUILayout.GetControlRect(false, 20);
                buttonRect.x = fieldRect.x;
                buttonRect.width = fieldRect.width;
                if (GUI.Button(buttonRect, "Open Subtree"))
                    Selection.activeObject = subtreeAsset;
            }

            if (subtreeAsset == null)
            {
                if (GUILayout.Button("Create New Subtree Asset"))
                {
                    if (isCreatingSubtree) return;
                    isCreatingSubtree = true;
                    EditorApplication.delayCall += () =>
                    {
                        CreateAndAssignNewSubtreeAsset();
                        serializedObject.ApplyModifiedProperties();
                        isCreatingSubtree = false;
                        Repaint();
                    };
                }
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndScrollView();
                serializedObject.ApplyModifiedProperties();
                return;
            }

            EditorGUILayout.EndVertical();

            if (subtreeAsset.blackboardDefinition == null)
            {
                EditorGUILayout.HelpBox("Subtree asset has no BlackboardDefinition.", MessageType.Warning);
                EditorGUILayout.EndScrollView();
                serializedObject.ApplyModifiedProperties();
                return;
            }

            BlackboardDefinition parentDef = BehaviourTreeEditor.currentBlackboardDef;
            if (parentDef == null)
            {
                EditorGUILayout.HelpBox("No parent BlackboardDefinition available.", MessageType.Info);
                EditorGUILayout.EndScrollView();
                serializedObject.ApplyModifiedProperties();
                return;
            }

            List<BlackboardVariable> subtreeVars = subtreeAsset.blackboardDefinition.sharedVariables ?? new List<BlackboardVariable>();
            EnsureBindingsSize(subtreeVars);

            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.LabelField("<b>Blackboard Bindings</b>", RichTextLabelStyle);
            EditorGUILayout.Space(4);

            for (int i = 0; i < subtreeVars.Count; i++)
            {
                BlackboardVariable subVar = subtreeVars[i];

                SerializedProperty bindingProp = bindingsProp.GetArrayElementAtIndex(i);
                SerializedProperty subtreeNameProp = bindingProp.FindPropertyRelative("subtreeVariableName");
                SerializedProperty parentNameProp = bindingProp.FindPropertyRelative("parentVariableName");

                subtreeNameProp.stringValue = subVar.name;

                string typeLabel = TypeLabel(subVar.typeName);
                EditorGUILayout.LabelField($"<b>{subVar.name}</b> : <color=lightblue>{typeLabel}</color>", RichTextLabelStyle);

                string[] options;
                int selectedIndex;
                bool isMissing;
                BuildParentOptions(parentDef, subVar.typeName, parentNameProp.stringValue, out options, out selectedIndex, out isMissing);

                if (InspectorView.IsRenderingReadOnly)
                {
                    string displayValue = string.IsNullOrEmpty(parentNameProp.stringValue) ? "<Local>" : parentNameProp.stringValue;
                    EditorGUILayout.LabelField("Mapped To", displayValue);
                }
                else
                {
                    Color oldColor = GUI.color;
                    if (isMissing)
                        GUI.color = Color.yellow;

                    int nextIndex = EditorGUILayout.Popup("Mapped To", selectedIndex, options);
                    GUI.color = oldColor;

                    if (isMissing && nextIndex == selectedIndex)
                    {
                        // user didn't change the dropdown — keep the stored value
                    }
                    else if (nextIndex <= 0)
                    {
                        parentNameProp.stringValue = string.Empty;
                    }
                    else
                    {
                        parentNameProp.stringValue = options[nextIndex];
                    }
                }

                EditorGUILayout.Space(2);
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.EndScrollView();
            serializedObject.ApplyModifiedProperties();
        }

        private void EnsureBindingsSize(List<BlackboardVariable> subtreeVars)
        {
            while (bindingsProp.arraySize < subtreeVars.Count)
                bindingsProp.InsertArrayElementAtIndex(bindingsProp.arraySize);
            while (bindingsProp.arraySize > subtreeVars.Count)
                bindingsProp.DeleteArrayElementAtIndex(bindingsProp.arraySize - 1);
        }

        private void BuildParentOptions(BlackboardDefinition parentDef, string subtreeTypeName, string currentParentName, out string[] options, out int selectedIndex, out bool isMissing)
        {
            isMissing = false;
            matchingOptions.Clear();

            if (FieldTypeHelper.TryGetSystemTypeFromName(subtreeTypeName, out Type subtreeType) && subtreeType != null)
            {
                for (int i = 0; i < parentDef.sharedVariables.Count; i++)
                {
                    BlackboardVariable pv = parentDef.sharedVariables[i];
                    if (!FieldTypeHelper.TryGetSystemTypeFromName(pv.typeName, out Type pt) || pt == null) continue;
                    if (pt == subtreeType)
                        matchingOptions.Add(pv.name);
                }
            }

            matchingOptions.Sort(StringComparer.Ordinal);
            matchingOptions.Insert(0, "<Local>");

            if (!string.IsNullOrEmpty(currentParentName) && !matchingOptions.Contains(currentParentName))
            {
                isMissing = true;
                matchingOptions.Add($"<Missing: {currentParentName}>");
            }

            options = matchingOptions.ToArray();

            selectedIndex = 0;
            if (!string.IsNullOrEmpty(currentParentName))
            {
                int idx = matchingOptions.IndexOf(currentParentName);
                if (idx >= 0) selectedIndex = idx;
                else if (isMissing)
                    selectedIndex = matchingOptions.Count - 1;
            }
        }

        private string TypeLabel(string typeName)
        {
            if (FieldTypeHelper.TryGetSystemTypeFromName(typeName, out Type t) && t != null)
                return t.Name;
            return typeName;
        }

        private void CreateAndAssignNewSubtreeAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject("Create Subtree", "NewSubtree", "asset", "Create a new BehaviourTreeAsset for the subtree");
            if (string.IsNullOrEmpty(path)) return;

            BehaviourTreeAsset subtreeAsset = CreateInstance<BehaviourTreeAsset>();
            subtreeAsset.name = System.IO.Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(subtreeAsset, path);

            subtreeAsset.nodesList = new List<BehaviourNode>();
            subtreeAsset.CreateBlackBoard();

            RootNode root = (RootNode)subtreeAsset.CreateNode(typeof(RootNode));
            root.name = "ROOT";
            subtreeAsset.root = root;
            subtreeAsset.RegisterNode(root);

            EditorUtility.SetDirty(subtreeAsset);
            AssetDatabase.SaveAssets();

            subTreeAssetProp.objectReferenceValue = subtreeAsset;
            bindingsProp.ClearArray();
            SubtreeCycleValidator.InvalidateCache();
        }
    }
}

