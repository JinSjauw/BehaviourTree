using System;
using System.Collections.Generic;
using BehaviourTree.Core;
using UnityEditor;
using UnityEngine;

namespace BehaviourTree.Editor
{
    [CustomEditor(typeof(BlackBoard))]
    public class BlackBoardEditor : UnityEditor.Editor
    {
        /// <summary>Slots that are in override mode (ObjectField visible even though serializedReferences is null).
        /// Keyed by slot index. Cleared and rebuilt when the definition layout changes.</summary>
        private HashSet<int> overrideActiveSlots = new();

        /// <summary>Value-type slots in transient override mode (Overridden clicked but no value committed yet).
        /// Keyed by compound string "variableName|elementIndex". Cleared on layout change,
        /// then rebuilt from BlackBoard.valueOverrides persistent state.</summary>
        private HashSet<string> overrideActiveValueSlots = new();

        /// <summary>Hash of the definition variable layout (names + strides in order).
        /// Used to detect layout changes and remap override state.</summary>
        private int lastLayoutHash;

        public override void OnInspectorGUI()
        {
            BlackBoard blackboard = (BlackBoard)target;
            SerializedObject so = serializedObject;

            BlackboardDefinition definition = blackboard.Definition;
            if (definition == null)
            {
                EditorGUILayout.HelpBox("No BlackboardDefinition found. Assign tree asset to TreeRunner", MessageType.Info);
                return;
            }

            // BuildSerializedReferences is the single owner of serializedReferences layout.
            // It handles layout-change detection, save/restore by name, and resizing.
            blackboard.BuildSerializedReferences(definition);

            // Sync SerializedObject after BuildSerializedReferences may have rebuilt the list.
            so.Update();

            IReadOnlyList<BlackboardVariableBase> allVars = definition.GetAllVariables();
            if (allVars.Count == 0)
            {
                EditorGUILayout.HelpBox("No variables in this definition.", MessageType.Info);
                so.ApplyModifiedProperties();
                return;
            }

            // Single pass: compute slot offsets and classify variables
            int varCount = allVars.Count;
            int[] slotOffsets = new int[varCount];
            int runningSlot = 0;
            int unresolvedCount = 0;
            int refCount = 0;
            int valueCount = 0;

            for (int i = 0; i < varCount; i++)
            {
                slotOffsets[i] = runningSlot;
                BlackboardVariableBase bv = allVars[i];
                Type type = bv.GetValueType();
                if (type == null)
                    unresolvedCount++;
                else if (!type.IsValueType)
                    refCount++;
                else
                    valueCount++;

                int stride = bv.Stride;
                runningSlot += (stride > 1) ? stride : 1;
            }

            if (unresolvedCount > 0)
                EditorGUILayout.HelpBox($"{unresolvedCount} variable(s) have an unresolved type name.", MessageType.Warning);

            bool hasRefs = refCount > 0;
            bool hasValues = valueCount > 0;

            if (!hasRefs && !hasValues)
            {
                EditorGUILayout.HelpBox("No variables in this definition.", MessageType.Info);
                so.ApplyModifiedProperties();
                return;
            }

            SerializedProperty serializedRefs = so.FindProperty("serializedReferences");

            // Detect layout changes and remap override state.
            // BuildSerializedReferences already remapped serializedReferences values by name;
            // we rebuild overrideActiveSlots and overriddenReferenceSlots from those remapped
            // values so committed overrides follow their variable. Transient override state
            // (clicked Override but not yet assigned) is discarded — the user can click again
            // at the variable's new position.
            int currentLayoutHash = ComputeLayoutHash(allVars);
            if (lastLayoutHash != 0 && currentLayoutHash != lastLayoutHash)
            {
                overrideActiveSlots.Clear();

                // Rebuild persisted reference-override tracking from remapped serializedReferences.
                SerializedProperty overriddenSlotsProp = so.FindProperty("overriddenReferenceSlots");
                overriddenSlotsProp.ClearArray();
                for (int i = 0; i < serializedRefs.arraySize; i++)
                {
                    if (serializedRefs.GetArrayElementAtIndex(i).objectReferenceValue != null)
                    {
                        overrideActiveSlots.Add(i);
                        overriddenSlotsProp.InsertArrayElementAtIndex(overriddenSlotsProp.arraySize);
                        overriddenSlotsProp.GetArrayElementAtIndex(overriddenSlotsProp.arraySize - 1).intValue = i;
                    }
                }

                // Rebuild value-type transient override state from persisted overrides.
                // BlackboardValueOverride stores variableName + elementIndex, so it survives
                // definition reorders without remapping — we just re-populate the HashSet keys.
                overrideActiveValueSlots.Clear();
                SerializedProperty valueOverridesProp = so.FindProperty("valueOverrides");
                for (int i = 0; i < valueOverridesProp.arraySize; i++)
                {
                    SerializedProperty vo = valueOverridesProp.GetArrayElementAtIndex(i);
                    string varName = vo.FindPropertyRelative("variableName").stringValue;
                    int elemIndex = vo.FindPropertyRelative("elementIndex").intValue;
                    if (!string.IsNullOrEmpty(varName))
                        overrideActiveValueSlots.Add($"{varName}|{elemIndex}");
                }

                lastLayoutHash = currentLayoutHash;
            }
            else if (lastLayoutHash == 0)
            {
                lastLayoutHash = currentLayoutHash;
            }

            bool isPlaying = EditorApplication.isPlaying;
            EditorGUI.BeginDisabledGroup(isPlaying);

            EditorGUI.BeginChangeCheck();
            if (hasRefs)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Serialized References", EditorStyles.boldLabel);
                EditorGUILayout.Space(2);

                for (int i = 0; i < varCount; i++)
                {
                    BlackboardVariableBase bv = allVars[i];
                    Type type = bv.GetValueType();
                    if (type == null || type.IsValueType)
                        continue;

                    int baseSlot = slotOffsets[i];
                    int stride = Mathf.Max(1, bv.Stride);
                    string displayName = FieldTypeHelper.GetDisplayName(type);

                    if (stride == 1)
                    {
                        DrawReferenceSlotEditor(serializedRefs, baseSlot, $"{bv.Name} : {displayName}", type, bv, 0, blackboard);
                    }
                    else
                    {
                        EditorGUILayout.LabelField($"{bv.Name} : {displayName} [{stride}]", EditorStyles.boldLabel);
                        EditorGUI.indentLevel++;
                        for (int slotOffset = 0; slotOffset < stride; slotOffset++)
                        {
                            int slotIndex = baseSlot + slotOffset;
                            DrawReferenceSlotEditor(serializedRefs, slotIndex, $"[{slotOffset}]", type, bv, slotOffset, blackboard);
                        }
                        EditorGUI.indentLevel--;
                    }
                }
            }

            // Only set dirty when a SerializedProperty was actually modified
            // (not for UI-only changes like the Override button click).
            if (EditorGUI.EndChangeCheck() && so.hasModifiedProperties)
                EditorUtility.SetDirty(blackboard);

            // ── Separator ─────────────────────────────────────────────
            if (hasRefs && hasValues)
            {
                EditorGUILayout.Space(6);
                Rect separatorRect = EditorGUILayout.GetControlRect(false, 1);
                EditorGUI.DrawRect(separatorRect, new Color(0.5f, 0.5f, 0.5f, 0.5f));
                EditorGUILayout.Space(6);
            }

            // ── Value-Type Variables ──────────────────────────────────
            if (hasValues)
            {
                EditorGUILayout.LabelField("Value-Type Variables", EditorStyles.boldLabel);
                EditorGUILayout.Space(2);

                for (int i = 0; i < varCount; i++)
                {
                    BlackboardVariableBase bv = allVars[i];
                    Type type = bv.GetValueType();
                    if (type == null || !type.IsValueType)
                        continue;

                    int stride = Mathf.Max(1, bv.Stride);
                    string displayName = FieldTypeHelper.GetDisplayName(type);

                    if (stride == 1)
                    {
                        DrawValueSlotEditor(bv, 0, $"{bv.Name} : {displayName}", type, definition, blackboard);
                    }
                    else
                    {
                        EditorGUILayout.LabelField($"{bv.Name} : {displayName} [{stride}]", EditorStyles.boldLabel);
                        EditorGUI.indentLevel++;
                        for (int elementIndex = 0; elementIndex < stride; elementIndex++)
                            DrawValueSlotEditor(bv, elementIndex, $"[{elementIndex}]", type, definition, blackboard);
                        EditorGUI.indentLevel--;
                    }
                }
            }

            EditorGUI.EndDisabledGroup();

            so.ApplyModifiedProperties();
        }

        /// <summary>
        /// Draws the editor UI for a single reference-type slot.
        /// If the definition already has a value assigned, shows an Override button
        /// instead of the ObjectField. Once overridden, shows the ObjectField with a
        /// clear button to revert to the definition value.
        /// </summary>
        private void DrawReferenceSlotEditor(
            SerializedProperty serializedRefs,
            int slotIndex,
            string label,
            Type type,
            BlackboardVariableBase bv,
            int elementIndex,
            BlackBoard blackboard)
        {
            SerializedProperty element = serializedRefs.GetArrayElementAtIndex(slotIndex);
            UnityEngine.Object currentRef = element.objectReferenceValue;
            UnityEngine.Object definitionRef = bv.GetBoxedValue(elementIndex) as UnityEngine.Object;
            bool definitionHasValue = definitionRef != null;

            if (!definitionHasValue)
            {
                // No definition value — show ObjectField directly (component-level override not applicable)
                bool hasValue = currentRef != null;
                EditorGUI.BeginDisabledGroup(hasValue);
                UnityEngine.Object newRef = EditorGUILayout.ObjectField(label, currentRef, type, true);
                EditorGUI.EndDisabledGroup();
                if (!hasValue)
                    element.objectReferenceValue = newRef;
                return;
            }

            // Definition has a value — show override pattern
            bool isOverridden = overrideActiveSlots.Contains(slotIndex) || blackboard.IsReferenceSlotOverridden(slotIndex);

            if (!isOverridden)
            {
                // Definition value shown read-only + Override button
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.ObjectField(label, definitionRef, type, false);
                EditorGUI.EndDisabledGroup();
                if (GUILayout.Button("Override", GUILayout.Width(70)))
                {
                    overrideActiveSlots.Add(slotIndex);
                    Undo.RecordObject(blackboard, "Override Reference Slot");
                    blackboard.SetReferenceSlotOverridden(slotIndex);
                    EditorUtility.SetDirty(blackboard);
                }
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                // Overridden — editable ObjectField + clear button to revert
                EditorGUILayout.BeginHorizontal();
                UnityEngine.Object newRef = EditorGUILayout.ObjectField(label, currentRef, type, true);
                element.objectReferenceValue = newRef;
                if (GUILayout.Button("x", GUILayout.Width(22)))
                {
                    element.objectReferenceValue = null;
                    overrideActiveSlots.Remove(slotIndex);
                    Undo.RecordObject(blackboard, "Clear Reference Override");
                    blackboard.ClearReferenceSlotOverridden(slotIndex);
                    EditorUtility.SetDirty(blackboard);
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        /// <summary>
        /// Computes a hash from variable names and strides in order.
        /// Any layout change (reorder, insert, delete, rename, stride change) produces a different hash.
        /// </summary>
        private static int ComputeLayoutHash(IReadOnlyList<BlackboardVariableBase> vars)
        {
            int hash = 17;
            for (int i = 0; i < vars.Count; i++)
            {
                hash = hash * 31 + (vars[i].Name?.GetHashCode() ?? 0);
                hash = hash * 31 + vars[i].Stride;
            }
            return hash;
        }

        /// <summary>
        /// Draws the editor UI for a single value-type slot element using the same
        /// override pattern as reference types: definition value shown read-only with
        /// an Override button; when overridden, shows editable field with X to revert.
        /// Override values are stored as BlackboardValueOverride on the BlackBoard component.
        /// </summary>
        private void DrawValueSlotEditor(
            BlackboardVariableBase bv,
            int elementIndex,
            string label,
            Type type,
            BlackboardDefinition definition,
            BlackBoard blackboard)
        {
            object definitionValue = bv.GetBoxedValue(elementIndex);
            BlackboardValueOverride existingOverride = blackboard.GetValueOverride(bv.Name, elementIndex);
            string overrideKey = $"{bv.Name}|{elementIndex}";
            bool isOverridden = overrideActiveValueSlots.Contains(overrideKey) || existingOverride != null;

            if (!isOverridden)
            {
                // Definition value shown read-only + Override button
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginDisabledGroup(true);
                DrawTypedField(label, definitionValue, type);
                EditorGUI.EndDisabledGroup();
                if (GUILayout.Button("Override", GUILayout.Width(70)))
                    overrideActiveValueSlots.Add(overrideKey);
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                // Overridden — editable field pre-filled with current override or definition value + X button
                object currentValue = existingOverride?.GetBoxedValue() ?? definitionValue;

                EditorGUILayout.BeginHorizontal();

                EditorGUI.BeginChangeCheck();
                object newValue = DrawTypedField(label, currentValue, type);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(blackboard, "Edit Blackboard Value Override");
                    blackboard.SetValueOverride(bv.Name, elementIndex, newValue);
                    EditorUtility.SetDirty(blackboard);
                }

                if (GUILayout.Button("X", GUILayout.Width(22)))
                {
                    Undo.RecordObject(blackboard, "Clear Blackboard Value Override");
                    blackboard.ClearValueOverride(bv.Name, elementIndex);
                    overrideActiveValueSlots.Remove(overrideKey);
                    EditorUtility.SetDirty(blackboard);
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        /// <summary>
        /// Dispatches to the appropriate EditorGUI field for a given type.
        /// Falls back to a read-only label for unknown custom types.
        /// </summary>
        private static object DrawTypedField(string label, object value, Type type)
        {
            if (type == typeof(int))
                return EditorGUILayout.IntField(label, value is int intValue ? intValue : 0);
            if (type == typeof(float))
                return EditorGUILayout.FloatField(label, value is float floatValue ? floatValue : 0f);
            if (type == typeof(bool))
                return EditorGUILayout.Toggle(label, value is bool boolValue ? boolValue : false);
            if (type == typeof(Vector2))
                return EditorGUILayout.Vector2Field(label, value is Vector2 v2 ? v2 : Vector2.zero);
            if (type == typeof(Vector3))
                return EditorGUILayout.Vector3Field(label, value is Vector3 v3 ? v3 : Vector3.zero);
            if (type == typeof(Vector4))
                return EditorGUILayout.Vector4Field(label, value is Vector4 v4 ? v4 : Vector4.zero);
            if (type == typeof(Color))
                return EditorGUILayout.ColorField(label, value is Color color ? color : Color.white);
            if (type.IsEnum)
            {
                if (value is Enum enumValue)
                    return EditorGUILayout.EnumPopup(label, enumValue);
                Array enumValues = Enum.GetValues(type);
                return EditorGUILayout.EnumPopup(label, (Enum)(enumValues.Length > 0 ? enumValues.GetValue(0) : Activator.CreateInstance(type)));
            }

            // Fallback for unknown custom types: read-only label with type name and ToString() value.
            string displayValue = value?.ToString() ?? "null";
            EditorGUILayout.LabelField(label, $"{FieldTypeHelper.GetDisplayName(type)} — {displayValue}");
            return value;
        }
    }
}
