using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using BehaviourTree.Core;

namespace BehaviourTree.Editor
{
    [CustomEditor(typeof(BehaviourNode), true)]
    public class CustomNodeEditor : UnityEditor.Editor
    {
        private MethodID lastMethodID;
        private SerializedProperty methodIDProp;
        private SerializedProperty fieldEntriesProp;
        private SerializedProperty blackBoardTypeIDProp;

        private void OnEnable()
        {
            if (target == null) return;

            methodIDProp = serializedObject.FindProperty("methodID");
            fieldEntriesProp = serializedObject.FindProperty("fieldEntries");
            blackBoardTypeIDProp = serializedObject.FindProperty("BlackBoardTypeID");
        }

        public override void OnInspectorGUI()
        {
            if(!(target is LeafNode || target is DecoratorNode)) 
            {
                DrawDefaultInspector();
                return;
            }

            serializedObject.Update();
            
            GUIStyle style = new GUIStyle(EditorStyles.label);
            style.richText = true;

            // Check if method changed and rebuild field entries
            MethodID selectedMethod = (MethodID)methodIDProp.enumValueIndex;
            bool methodChanged = selectedMethod != lastMethodID;
            lastMethodID = selectedMethod;

            EditorGUI.BeginChangeCheck();

            // Draw dynamic field entries based on metadata
            List<ParamInfo> paramInfoList = MethodMetadataCache.GetParamsForMethod(selectedMethod);

            if (paramInfoList != null && paramInfoList.Count > 0)
            {
                // Ensure the list size matches
                while (fieldEntriesProp.arraySize < paramInfoList.Count)
                    fieldEntriesProp.InsertArrayElementAtIndex(fieldEntriesProp.arraySize);
                while (fieldEntriesProp.arraySize > paramInfoList.Count)
                    fieldEntriesProp.DeleteArrayElementAtIndex(fieldEntriesProp.arraySize - 1);

                for (int i = 0; i < paramInfoList.Count; i++)
                {
                    ParamInfo info = paramInfoList[i];
                    SerializedProperty entryProp = fieldEntriesProp.GetArrayElementAtIndex(i);
                    SerializedProperty fieldNameProp = entryProp.FindPropertyRelative("fieldName");
                    SerializedProperty isVariableProp = entryProp.FindPropertyRelative("isVariable");
                    SerializedProperty variableNameProp = entryProp.FindPropertyRelative("variableName");
                    SerializedProperty fieldTypeProp = entryProp.FindPropertyRelative("fieldType");

                    FieldType fieldType = FieldTypeHelper.GetFieldType(info.fieldType);

                    // Set static metadata
                    fieldNameProp.stringValue = info.fieldName;
                    fieldTypeProp.enumValueIndex = (int)fieldType;

                    if (methodChanged)
                    {
                        isVariableProp.boolValue = info.isVariable;
                    }

                    EditorGUILayout.BeginVertical("box");
                    EditorGUILayout.LabelField($"<b>{info.fieldName}</b> : <color=lightblue>{fieldType}</color>", style);

                    if (isVariableProp.boolValue)
                    {
                        DrawVariableDropdown(variableNameProp, info.fieldType);
                    }
                    else
                    {
                        DrawConstantField(entryProp, info.fieldType);
                    }

                    EditorGUILayout.EndVertical();
                }
            }
            else
            {
                // No metadata; clear entries
                fieldEntriesProp.ClearArray();
                EditorGUILayout.HelpBox($"No *_Params struct found for method {selectedMethod}. Create a struct named {selectedMethod}_Params.", MessageType.Warning);
            }

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(blackBoardTypeIDProp);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawConstantField(SerializedProperty entryProp, System.Type fieldType)
        {
            if (fieldType == typeof(int) || fieldType == typeof(uint))
            {
                SerializedProperty prop = entryProp.FindPropertyRelative("intValue");
                prop.intValue = EditorGUILayout.IntField("Value", prop.intValue);
            }
            else if (fieldType == typeof(float))
            {
                SerializedProperty prop = entryProp.FindPropertyRelative("floatValue");
                prop.floatValue = EditorGUILayout.FloatField("Value", prop.floatValue);
            }
            else if (fieldType == typeof(bool))
            {
                SerializedProperty prop = entryProp.FindPropertyRelative("boolValue");
                prop.boolValue = EditorGUILayout.Toggle("Value", prop.boolValue);
            }
            else
            {
                EditorGUILayout.HelpBox($"Value type can't be static! Add [SharedVar] attribute", MessageType.Warning);
            }
        }

        private void DrawVariableDropdown(SerializedProperty variableNameProp, System.Type expectedType)
        {
            BlackboardDefinition blackBoardDef = BehaviourTreeEditor.currentBlackboardDef;

            if (blackBoardDef == null)
            {
                EditorGUILayout.HelpBox("No Blackboard Definition assigned.", MessageType.Warning);
                return;
            }

            if (blackBoardDef.sharedVariables == null || blackBoardDef.sharedVariables.Count == 0)
            {
                EditorGUILayout.HelpBox("No Blackboard variables added.", MessageType.Warning);
                return;
            }

            // Filter variables whose type matches the expected type using unified helper
            var matchingVars = new List<string>();
            for (int v = 0; v < blackBoardDef.sharedVariables.Count; v++)
            {
                System.Type bbType = FieldTypeHelper.GetSystemTypeFromName(blackBoardDef.sharedVariables[v].typeName);
                if (bbType == expectedType)
                {
                    matchingVars.Add(blackBoardDef.sharedVariables[v].name);
                }
            }

            if (matchingVars.Count == 0)
            {
                EditorGUILayout.HelpBox($"No matching variable of type <{FieldTypeHelper.GetFieldType(expectedType)}> in Blackboard.", MessageType.Info);
                return;
            }

            string currentVal = variableNameProp.stringValue;
            int selectedIndex = matchingVars.IndexOf(currentVal);
            if (selectedIndex < 0) selectedIndex = 0;

            selectedIndex = EditorGUILayout.Popup("Shared Variable", selectedIndex, matchingVars.ToArray());
            variableNameProp.stringValue = matchingVars[selectedIndex];
        }
    }
}