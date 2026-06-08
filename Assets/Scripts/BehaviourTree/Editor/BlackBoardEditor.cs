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

            // Build filtered list with stride expansion for array variables
            refIndices.Clear();
            refNames.Clear();
            List<BlackboardVariable> variables = definition.sharedVariables;
            int unresolvedTypeCount = 0;
            for (int i = 0; i < variables.Count; i++)
            {
                BlackboardVariable bv = variables[i];
                if (!FieldTypeHelper.TryGetSystemTypeFromName(bv.typeName, out Type type) || type == null)
                {
                    unresolvedTypeCount++;
                    continue;
                }
                if (type != null && !type.IsValueType)
                {
                    refIndices.Add(i);
                    refNames.Add(bv.name);
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

            // Ensure array size matches total slot count (stride > 1 expands)
            int totalSlotCount = 0;
            for (int i = 0; i < variables.Count; i++)
            {
                int stride = variables[i].stride;
                totalSlotCount += (stride > 1) ? stride : 1;
            }

            while (serializedRefs.arraySize < totalSlotCount)
            {
                serializedRefs.InsertArrayElementAtIndex(serializedRefs.arraySize);
            }
            while (serializedRefs.arraySize > totalSlotCount)
            {
                serializedRefs.DeleteArrayElementAtIndex(serializedRefs.arraySize - 1);
            }

            // Compute per-variable slot offsets
            int[] slotOffsets = new int[variables.Count];
            int runningSlot = 0;
            for (int i = 0; i < variables.Count; i++)
            {
                slotOffsets[i] = runningSlot;
                int stride = variables[i].stride;
                runningSlot += (stride > 1) ? stride : 1;
            }

            for (int i = 0; i < refIndices.Count; i++)
            {
                int varIndex = refIndices[i];
                BlackboardVariable bv = variables[varIndex];
                if (!FieldTypeHelper.TryGetSystemTypeFromName(bv.typeName, out Type expectedType) || expectedType == null)
                    continue;

                string fieldName = refNames[i];
                int baseSlot = slotOffsets[varIndex];
                int stride = bv.stride;
                int effectiveStride = (stride > 1) ? stride : 1;

                if (effectiveStride == 1)
                {
                    // Single reference slot
                    SerializedProperty element = serializedRefs.GetArrayElementAtIndex(baseSlot);
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"<b> {fieldName} </b> : <color=#19E3B1>{expectedType.Name}</color>", RichStyle, GUILayout.ExpandWidth(false));
                    EditorGUI.BeginChangeCheck();
                    UnityEngine.Object newValue = EditorGUILayout.ObjectField(
                        GUIContent.none,
                        element.objectReferenceValue,
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
                else
                {
                    // Strided reference — draw one ObjectField per element
                    EditorGUILayout.LabelField($"<b> {fieldName} </b> : <color=#19E3B1>{expectedType.Name}[{effectiveStride}]</color>", RichStyle);
                    EditorGUI.indentLevel++;
                    for (int s = 0; s < effectiveStride; s++)
                    {
                        int slotIndex = baseSlot + s;
                        SerializedProperty element = serializedRefs.GetArrayElementAtIndex(slotIndex);

                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField($"[{s}]", GUILayout.Width(24));
                        EditorGUI.BeginChangeCheck();
                        UnityEngine.Object newValue = EditorGUILayout.ObjectField(
                            GUIContent.none,
                            element.objectReferenceValue,
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
                    EditorGUI.indentLevel--;
                }
            }

            so.ApplyModifiedProperties();
        }
    }
}
