using System;
using System.Collections.Generic;
using BehaviourTree.Core;
using BehaviourTree.Runtime;
using UnityEditor;
using UnityEngine;

namespace BehaviourTree.Editor
{
    [CustomEditor(typeof(BlackBoard))]
    public class BlackBoardEditor : UnityEditor.Editor
    {
        private BlackboardDefinition definition;
        private List<int> refIndices = new();
        private List<string> refNames = new();
        private GUIStyle richStyle;

        private GUIStyle RichStyle
        {
            get
            {
                if (richStyle == null)
                    richStyle = new GUIStyle(EditorStyles.label) { richText = true };
                return richStyle;
            }
        }

        public override void OnInspectorGUI()
        {
            BlackBoard blackboard = (BlackBoard)target;
            SerializedObject so = serializedObject;
            so.Update();

            definition = blackboard.Definition;

            if (definition == null)
            {
                EditorGUILayout.HelpBox("No BlackboardDefinition found. Assign tree asset to TreeRunner", MessageType.Info);
                so.ApplyModifiedProperties();
                return;
            }

            if (definition != null)
            {
                // Keep serializedReferences list size in sync
                blackboard.BuildSerializedReferences(definition);
                EditorUtility.SetDirty(blackboard);
                so.Update();
            }

            // Build filtered list
            refIndices.Clear();
            refNames.Clear();
            List<BlackboardVariable> variables = definition.sharedVariables;
            int unresolvedTypeCount = 0;
            for (int i = 0; i < variables.Count; i++)
            {
                if (!FieldTypeHelper.TryGetSystemTypeFromName(variables[i].typeName, out Type type) || type == null)
                {
                    unresolvedTypeCount++;
                    continue;
                }
                if (type != null && !type.IsValueType)
                {
                    refIndices.Add(i);
                    refNames.Add(variables[i].name);
                }
            }

            if (unresolvedTypeCount > 0)
            {
                EditorGUILayout.HelpBox($"{unresolvedTypeCount} Blackboard variable(s) have an unresolved type name.", MessageType.Warning);
            }

            if (refIndices.Count == 0)
            {
                EditorGUILayout.HelpBox("No reference-type variables (GameObject / Transform) in this definition.", MessageType.Info);
                so.ApplyModifiedProperties();
                return;
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Reference Slots", EditorStyles.boldLabel);

            SerializedProperty serializedRefs = so.FindProperty("serializedReferences");

            // Ensure array size matches definition
            while (serializedRefs.arraySize < variables.Count)
            {
                serializedRefs.InsertArrayElementAtIndex(serializedRefs.arraySize);
            }
            while (serializedRefs.arraySize > variables.Count)
            {
                serializedRefs.DeleteArrayElementAtIndex(serializedRefs.arraySize - 1);
            }
            for (int i = 0; i < refIndices.Count; i++)
            {
                int    fieldIndex = refIndices[i];
                if (!FieldTypeHelper.TryGetSystemTypeFromName(variables[fieldIndex].typeName, out Type expectedType) || expectedType == null)
                    continue;
                
                string fieldName = refNames[i];

                SerializedProperty element = serializedRefs.GetArrayElementAtIndex(fieldIndex);
                UnityEngine.Object currentValue = element.objectReferenceValue;

                // Draw fields

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"<b> {fieldName} </b> : <color=#19E3B1>{expectedType.Name}</color>", RichStyle, GUILayout.ExpandWidth(false));
                EditorGUI.BeginChangeCheck();
                UnityEngine.Object newValue = EditorGUILayout.ObjectField(
                    GUIContent.none,
                    currentValue,
                    expectedType,
                    allowSceneObjects: true,
                    GUILayout.ExpandWidth(true));

                if (EditorGUI.EndChangeCheck())
                {
                    element.objectReferenceValue = newValue;
                    EditorUtility.SetDirty(blackboard);
                }
                EditorGUILayout.EndHorizontal();
            }

            so.ApplyModifiedProperties();
        }
    }
}
