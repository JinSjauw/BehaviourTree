using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using BehaviourTree;
using BehaviourTree.Core;
using System;

namespace BehaviourTree.Editor
{
    [CustomEditor(typeof(BehaviourNode), true)]
    public class CustomNodeEditor : UnityEditor.Editor
    {
        public bool nodeNameChangedThisFrame;

        private string lastMethodName;
        private SerializedProperty nodeNameProp;
        private SerializedProperty methodNameProp;
        private SerializedProperty fieldEntriesProp;
        private SerializedProperty childrenProp;
        private SerializedProperty commentProp;
        private SerializedProperty abortTypeProp;
        private GUIStyle style;
        private List<string> matchingVars;
        private List<string> matchingVarNames;
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
            matchingVarNames = new List<string>();
            nodeNameProp = serializedObject.FindProperty("nodeName");
            methodNameProp = serializedObject.FindProperty("methodName");
            fieldEntriesProp = serializedObject.FindProperty("fieldEntries");
            childrenProp = serializedObject.FindProperty("children");
            commentProp = serializedObject.FindProperty("comment");
            if (target is CompositeNode) abortTypeProp = serializedObject.FindProperty("abortType");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            if(target is RootNode) return;

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(nodeNameProp, new GUIContent("Node Name"));
            nodeNameChangedThisFrame = EditorGUI.EndChangeCheck();            
            
            if(!(target is LeafNode || target is DecoratorNode || target is CompositeNode)) 
            {
                DrawDefaultInspector();
                DrawChildrenDebug();
                serializedObject.ApplyModifiedProperties();
                return;
            }

            // Check if method changed and rebuild field entries
            string selectedMethodName = methodNameProp != null ? methodNameProp.stringValue : null;
            bool methodChanged = selectedMethodName != lastMethodName;
            lastMethodName = selectedMethodName;
            EditorGUI.BeginChangeCheck();

            if (commentProp != null)
            {
                EditorGUILayout.LabelField("Comment", EditorStyles.boldLabel);
                commentProp.stringValue = EditorGUILayout.TextArea(commentProp.stringValue, GUILayout.Height(60));
                EditorGUILayout.Space();
            }

            BuildFieldEntries(selectedMethodName, methodChanged);

            if (abortTypeProp != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Conditional Abort", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(abortTypeProp, new GUIContent("Abort Type"));

                AbortType currentAbort = (AbortType)abortTypeProp.enumValueIndex;
                if (currentAbort != AbortType.None)
                {
                    CompositeNode composite = (CompositeNode)target;
                    if (!HasValidConditionForAbort(composite, currentAbort))
                    {
                        EditorGUILayout.HelpBox(
                            "No reachable Condition node found. Add a Condition node as a " +
                            "descendant for this abort type to take effect.",
                            MessageType.Warning);
                    }
                }
            }

            if(target is CompositeNode)
            {
                DrawChildrenDebug();
            }

            EditorGUILayout.Space();

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

        private void BuildFieldEntries(string selectedMethodName, bool methodChanged)
        {
            List<ParamInfo> paramInfoList = null;
            if (!string.IsNullOrEmpty(selectedMethodName))
                paramInfoList = MethodMetadataCache.GetParamsForMethod(selectedMethodName);

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
                    SerializedProperty isArrayProp = entryProp.FindPropertyRelative("isArray");
                    SerializedProperty isToggleVariableProp = entryProp.FindPropertyRelative("isToggleVariable");
                    SerializedProperty variableNameProp = entryProp.FindPropertyRelative("variableName");
                    SerializedProperty fieldTypeNameProp = entryProp.FindPropertyRelative("fieldTypeName");

                    // Set static metadata
                    fieldNameProp.stringValue = info.fieldName;
                    fieldTypeNameProp.stringValue = info.fieldType?.AssemblyQualifiedName ?? string.Empty;
                    isArrayProp.boolValue = info.isArray;

                    EditorGUILayout.BeginVertical("box");

                    string typeLabel;
                    if(info.fieldType != null && info.fieldType.IsEnum)
                    {
                        typeLabel = $"Enum( {info.fieldType.Name} )";
                    }
                    else
                    {
                        typeLabel = info.fieldType != null ? info.fieldType.Name : "Unknown";
                        if (info.isArray) typeLabel += "[]";
                    }
                    
                    string displayName = char.ToUpper(info.fieldName[0]) + info.fieldName.Substring(1);
                    EditorGUILayout.LabelField($"<b>{displayName}</b> : <color=lightblue>{typeLabel}</color>", RichTextLabelStyle);

                    isVariableProp.boolValue = info.isVariable || info.isArray;

                    if(info.isToggleVariable)
                    {
                        bool varToggle = EditorGUILayout.Toggle("is Variable", isToggleVariableProp.boolValue);
                        entryProp.FindPropertyRelative("isVariable").boolValue = varToggle;
                        entryProp.FindPropertyRelative("isToggleVariable").boolValue = varToggle;
                    }

                    // Hide customTickValue when useCustomTick is false (works for both legacy and new)
                    if (info.fieldName == "customTickValue" && i > 0)
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
                        DrawVariableDropdown(variableNameProp, info.fieldType, info.isArray);
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

                if(target is CompositeNode) return;

                string methodDesc = !string.IsNullOrEmpty(selectedMethodName) ? selectedMethodName : "(none)";
                EditorGUILayout.HelpBox($"No schema found for method '{methodDesc}'.", MessageType.Info);
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
            else if (fieldType == typeof(Vector2))
            {
                SerializedProperty prop = entryProp.FindPropertyRelative("vector2Value");
                prop.vector2Value = EditorGUILayout.Vector2Field("Value", prop.vector2Value);
            }
            else if (fieldType == typeof(Vector3))
            {
                SerializedProperty prop = entryProp.FindPropertyRelative("vector3Value");
                prop.vector3Value = EditorGUILayout.Vector3Field("Value", prop.vector3Value);
            }
            else if (typeof(UnityEngine.Object).IsAssignableFrom(fieldType))
            {
                SerializedProperty prop = fieldType == typeof(GameObject)
                    ? entryProp.FindPropertyRelative("gameObjectValue")
                    : entryProp.FindPropertyRelative("transformValue");
                prop.objectReferenceValue = EditorGUILayout.ObjectField("Value", prop.objectReferenceValue, fieldType, true);
            }
            else
            {
                EditorGUILayout.HelpBox($"Type '{fieldType.Name}' requires a [SharedVar] — use a blackboard variable instead of a constant.", MessageType.Warning);
            }
        }

        private void DrawVariableDropdown(SerializedProperty variableNameProp, Type expectedType, bool isArray = false)
        {
            if (expectedType == null)
            {
                EditorGUILayout.HelpBox("[SharedVar] field type could not be resolved.", MessageType.Warning);
                return;
            }

            // All types are supported as blackboard variables (including enums)
            BlackboardDefinition blackBoardDef = BehaviourTreeEditor.currentBlackboardDef;

            if (blackBoardDef == null)
            {
                EditorGUILayout.HelpBox("No Blackboard Definition assigned.", MessageType.Warning);
                return;
            }

            IReadOnlyList<BlackboardVariableBase> allVars = blackBoardDef.GetAllVariables();
            if (allVars == null || allVars.Count == 0)
            {
                EditorGUILayout.HelpBox("No Blackboard variables added.", MessageType.Warning);
                return;
            }

            // Filter variables whose type matches and stride matches the param kind
            matchingVars.Clear();
            matchingVarNames.Clear();
            for (int variableIndex = 0; variableIndex < allVars.Count; variableIndex++)
            {
                BlackboardVariableBase bv = allVars[variableIndex];
                Type bbType = bv.GetValueType();
                if (bbType == null) continue;
                if (bbType != expectedType) continue;

                if (isArray)
                {
                    if (bv.Stride <= 1) continue;
                    matchingVarNames.Add(bv.Name);
                    matchingVars.Add($"{bv.Name} [{bv.Stride}]");
                }
                else
                {
                    if (bv.Stride > 1) continue;
                    matchingVarNames.Add(bv.Name);
                    matchingVars.Add(bv.Name);
                }
            }

            if (matchingVars.Count == 0)
            {
                EditorGUILayout.HelpBox($"No matching variable of type '{expectedType.Name}' in Blackboard.", MessageType.Info);
                variableNameProp.stringValue = "";
                return;
            }

            string currentVal = variableNameProp.stringValue;
            int selectedIndex = matchingVarNames.IndexOf(currentVal);
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
                variableNameProp.stringValue = matchingVarNames[selectedIndex];
            }
        }

        // ── Conditional Abort Validation ──

        private static bool HasValidConditionForAbort(CompositeNode composite, AbortType abortType)
        {
            bool found = false;
            HasConditionRecursiveEditor(composite.children, abortType, ref found);
            return found;
        }

        private static void HasConditionRecursiveEditor(List<BehaviourNode> children,
            AbortType requiredType, ref bool found)
        {
            if (children == null || found) return;

            // Pass 1: check direct children for method-bearing condition nodes
            for (int i = 0; i < children.Count; i++)
            {
                if (children[i] is LeafNode leaf &&
                    leaf.NodeType == BehaviourNodeType.CONDITION &&
                    !string.IsNullOrEmpty(leaf.methodName))
                {
                    found = true;
                    return;
                }
            }

            // Pass 2: recurse into child composites with compatible abort type
            for (int i = 0; i < children.Count; i++)
            {
                if (children[i] is CompositeNode childComposite)
                {
                    AbortType childAbort = childComposite.abortType;
                    bool compatible = childAbort == requiredType || childAbort == AbortType.Both;
                    if (compatible)
                        HasConditionRecursiveEditor(childComposite.children, requiredType, ref found);
                }
            }
        }
    }
}
