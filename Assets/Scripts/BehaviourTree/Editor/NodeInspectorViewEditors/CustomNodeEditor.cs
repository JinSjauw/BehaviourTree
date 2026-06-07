using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using BehaviourTree.Core;
using System;

namespace BehaviourTree.Editor
{
    [CustomEditor(typeof(BehaviourNode), true)]
    public class CustomNodeEditor : UnityEditor.Editor
    {
        private MethodID lastMethodID;
        private SerializedProperty methodIDProp;
        private SerializedProperty fieldEntriesProp;
        private SerializedProperty blackBoardTypeIDProp;
        private SerializedProperty childrenProp;
        private SerializedProperty commentProp;
        private GUIStyle style;
        private List<string> matchingVars;
        private GUIStyle RichTextLabelStyle
        {
            get
            {
                if (style == null) 
                {
                    style = new GUIStyle(EditorStyles.label) { richText = true };
                }
                return style;
            }
        }

        private void OnEnable()
        {
            if (target == null) return;
            
            matchingVars = new List<string>();
            methodIDProp = serializedObject.FindProperty("methodID");
            fieldEntriesProp = serializedObject.FindProperty("fieldEntries");
            blackBoardTypeIDProp = serializedObject.FindProperty("BlackBoardTypeID");
            childrenProp = serializedObject.FindProperty("children");
            if (target is LeafNode) commentProp = serializedObject.FindProperty("comment");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if(!(target is LeafNode || target is DecoratorNode)) 
            {
                DrawDefaultInspector();
                DrawChildrenDebug();
                serializedObject.ApplyModifiedProperties();
                return;
            }

            // Check if method changed and rebuild field entries
            MethodID selectedMethod = (MethodID)methodIDProp.enumValueIndex;
            bool methodChanged = selectedMethod != lastMethodID;
            lastMethodID = selectedMethod;

            EditorGUI.BeginChangeCheck();

            if (target is LeafNode && commentProp != null)
            {
                EditorGUILayout.LabelField("Comment", EditorStyles.boldLabel);
                commentProp.stringValue = EditorGUILayout.TextArea(commentProp.stringValue, GUILayout.Height(60));
                EditorGUILayout.Space();
            }

            BuildFieldEntries(selectedMethod, methodChanged);

            EditorGUILayout.Space();
            //EditorGUILayout.PropertyField(blackBoardTypeIDProp);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawChildrenDebug()
        {
            if (childrenProp == null) return;

            EditorGUILayout.Space();
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.PropertyField(childrenProp, true);
            EditorGUI.EndDisabledGroup();
        }

        private void BuildFieldEntries(MethodID selectedMethod, bool methodChanged)
        {
            // Draw dynamic field entries based on metadata
            List<ParamInfo> paramInfoList = MethodMetadataCache.GetParamsForMethod(selectedMethod);

            if (paramInfoList != null && paramInfoList.Count > 0)
            {
                if(methodChanged)
                {
                    ResizeFieldEntries(paramInfoList);
                }

                for (int i = 0; i < paramInfoList.Count; i++)
                {
                    ParamInfo info = paramInfoList[i];
                    SerializedProperty entryProp = fieldEntriesProp.GetArrayElementAtIndex(i);
                    SerializedProperty fieldNameProp = entryProp.FindPropertyRelative("fieldName");
                    SerializedProperty isVariableProp = entryProp.FindPropertyRelative("isVariable");
                    SerializedProperty isToggleVariableProp = entryProp.FindPropertyRelative("isToggleVariable");
                    SerializedProperty variableNameProp = entryProp.FindPropertyRelative("variableName");
                    SerializedProperty fieldTypeProp = entryProp.FindPropertyRelative("fieldType");

                    FieldType fieldType = FieldTypeHelper.GetFieldType(info.fieldType);

                    // Set static metadata
                    fieldNameProp.stringValue = info.fieldName;
                    fieldTypeProp.enumValueIndex = (int)fieldType;

                    EditorGUILayout.BeginVertical("box");

                    string typeLabel = "";
                    if(info.fieldType != null && info.fieldType.IsEnum)
                    {
                        typeLabel = $"Enum( {info.fieldType.Name} )";
                    }
                    else
                    {
                        typeLabel = fieldType.ToString();
                    }
                    
                    string displayName = char.ToUpper(info.fieldName[0]) + info.fieldName.Substring(1);
                    EditorGUILayout.LabelField($"<b>{displayName}</b> : <color=lightblue>{typeLabel}</color>", RichTextLabelStyle);

                    isVariableProp.boolValue = info.isVariable;

                    if(info.isToggleVariable)
                    {
                        bool varToggle = EditorGUILayout.Toggle("is Variable", isToggleVariableProp.boolValue);
                        entryProp.FindPropertyRelative("isVariable").boolValue = varToggle;
                        entryProp.FindPropertyRelative("isToggleVariable").boolValue = varToggle;
                    }

                    // Hide customTickValue when useCustomTick is false
                    if (selectedMethod == MethodID.Cooldown && info.fieldName == "customTickValue")
                    {
                        SerializedProperty useCustomEntry = fieldEntriesProp.GetArrayElementAtIndex(i - 1);
                        if (!useCustomEntry.FindPropertyRelative("boolValue").boolValue)
                        {
                            EditorGUILayout.EndVertical();
                            continue;
                        }
                    }

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
                EditorGUILayout.HelpBox($"No *_NodeFields struct for MethodID {selectedMethod}. For fields create struct named {selectedMethod}_NodeFields.", MessageType.Info);
            }
        }

        private void ResizeFieldEntries(List<ParamInfo> paramInfoList)
        {
            while (fieldEntriesProp.arraySize < paramInfoList.Count)
                fieldEntriesProp.InsertArrayElementAtIndex(fieldEntriesProp.arraySize);
            while (fieldEntriesProp.arraySize > paramInfoList.Count)
                fieldEntriesProp.DeleteArrayElementAtIndex(fieldEntriesProp.arraySize - 1);
        }

        private void DrawConstantField(SerializedProperty entryProp, Type fieldType)
        {
            if (fieldType != null && fieldType.IsEnum)
            {
                SerializedProperty prop = entryProp.FindPropertyRelative("intValue");
                int currentRaw = prop.intValue;
                Enum current = (Enum)Enum.ToObject(fieldType, currentRaw);
                Enum next = EditorGUILayout.EnumPopup("Value", current);
                prop.intValue = Convert.ToInt32(next);
            }
            else if (fieldType == typeof(int) || fieldType == typeof(uint))
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

        private void DrawVariableDropdown(SerializedProperty variableNameProp, Type expectedType)
        {
            if (expectedType == null)
            {
                EditorGUILayout.HelpBox("[SharedVar] field type could not be resolved.", MessageType.Warning);
                return;
            }

            if (expectedType.IsEnum)
            {
                EditorGUILayout.HelpBox($"[SharedVar] enum fields are not supported. Use an int SharedVar instead.", MessageType.Warning);
                return;
            }

            bool isSupportedType = false;
            for (int i = 0; i < FieldTypeHelper.AllFieldTypes.Count; i++)
            {
                if (FieldTypeHelper.GetSystemType(FieldTypeHelper.AllFieldTypes[i]) == expectedType)
                {
                    isSupportedType = true;
                    break;
                }
            }

            if (!isSupportedType)
            {
                EditorGUILayout.HelpBox($"[SharedVar] type '{expectedType.FullName}' is not supported as a blackboard variable.", MessageType.Warning);
                return;
            }

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
            matchingVars.Clear();
            for (int v = 0; v < blackBoardDef.sharedVariables.Count; v++)
            {
                if (!FieldTypeHelper.TryGetSystemTypeFromName(blackBoardDef.sharedVariables[v].typeName, out Type bbType) || bbType == null)
                    continue;
                if (bbType == expectedType)
                {
                    matchingVars.Add(blackBoardDef.sharedVariables[v].name);
                }
            }

            if (matchingVars.Count == 0)
            {
                EditorGUILayout.HelpBox($"No matching variable of type <{FieldTypeHelper.GetFieldType(expectedType)}> in Blackboard.", MessageType.Info);
                variableNameProp.stringValue = "";
                return;
            }

            string currentVal = variableNameProp.stringValue;
            int selectedIndex = matchingVars.IndexOf(currentVal);
            if (selectedIndex < 0) selectedIndex = 0;

            if (InspectorView.IsRenderingReadOnly)
            {
                string display = currentVal;
                EditorGUILayout.LabelField("Shared Variable", display);

                if (InspectorView.CurrentProxyMappings != null &&
                    InspectorView.CurrentProxyMappings.TryGetValue(currentVal, out string parentVar))
                {
                    EditorGUILayout.LabelField("Mapped To: ", parentVar);
                }

            }
            else
            {
                selectedIndex = EditorGUILayout.Popup("Shared Variable", selectedIndex, matchingVars.ToArray());
                variableNameProp.stringValue = matchingVars[selectedIndex];
            }
        }
    }
}
