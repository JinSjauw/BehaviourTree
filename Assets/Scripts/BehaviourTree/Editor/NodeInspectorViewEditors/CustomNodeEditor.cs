using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using BehaviourTree;
using BehaviourTree.Core;
using System;
using UnityEditor.Experimental.GraphView;

namespace BehaviourTree.Editor
{
    [CustomEditor(typeof(BehaviourNode), true)]
    public class CustomNodeEditor : UnityEditor.Editor
    {
        private const float SmallButtonWidth = 22f;
        private const float FieldLabelWidth = 100f;
        private const float DropdownFieldWidth = 150f;

        public bool nodeNameChangedThisFrame;
        public bool nodeVisualsChangedThisFrame;

        private string lastMethodName;
        private SerializedProperty nodeNameProp;
        private SerializedProperty methodNameProp;
        private SerializedProperty fieldEntriesProp;
        private SerializedProperty childrenProp;
        private SerializedProperty commentProp;
        private SerializedProperty abortTypeProp;
        private AbortType lastAbortType;
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
            if (target is CompositeNode composite) lastAbortType = composite.abortType;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUIUtility.labelWidth = FieldLabelWidth;
            
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
                if (currentAbort != lastAbortType)
                {
                    lastAbortType = currentAbort;
                    nodeVisualsChangedThisFrame = true;
                }

                if (currentAbort != AbortType.None)
                {
                    CompositeNode composite = (CompositeNode)target;
                    if (!NodeWarningEvaluator.HasValidConditionForAbort(composite, currentAbort))
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
            // Dynamic-type nodes (SetVariable, ClearVariable, CompareVariable) have
            // ParameterCount > 0 but no [SharedVar] C# fields — render a custom inspector.
            int dynamicParamCount = GetDynamicParameterCount(selectedMethodName);
            if (dynamicParamCount > 0)
            {
                BuildDynamicFieldEntries(selectedMethodName, dynamicParamCount, methodChanged);
                return;
            }

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

                    if (methodChanged && info.isOrderDropdown)
                    {
                        SerializedProperty isOrderProp = entryProp.FindPropertyRelative("isOrderConstant");
                        if (isOrderProp != null) isOrderProp.boolValue = true;
                    }

                    if (info.isHidden)
                    {
                        // Auto-fill variable binding by convention — no UI rendered
                        if (methodChanged)
                        {
                            isVariableProp.boolValue = true;
                            variableNameProp.stringValue = info.autoVariableName;
                        }
                        continue;
                    }

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

                    if (!info.isToggleVariable)
                        isVariableProp.boolValue = info.isVariable || info.isArray;

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

                    EditorGUILayout.BeginHorizontal();

                    bool drawVariableField = info.isToggleVariable ? isToggleVariableProp.boolValue : isVariableProp.boolValue;

                    if (drawVariableField)
                    {
                        DrawVariableDropdown(variableNameProp, info.fieldType, info.isArray);
                    }
                    else
                    {
                        DrawConstantField(entryProp, info);
                    }

                    GUILayout.FlexibleSpace();

                    if (info.isToggleVariable)
                    {
                        string toggleLabel = drawVariableField ? "C" : "V";
                        string toggleTooltip = drawVariableField ? "Switch to constant" : "Switch to variable";
                        if (GUILayout.Button(new GUIContent(toggleLabel, toggleTooltip), GUILayout.Width(SmallButtonWidth), GUILayout.Height(EditorGUIUtility.singleLineHeight)))
                        {
                            bool newValue = !drawVariableField;
                            entryProp.FindPropertyRelative("isVariable").boolValue = newValue;
                            entryProp.FindPropertyRelative("isToggleVariable").boolValue = newValue;
                            nodeVisualsChangedThisFrame = true;
                        }
                    }

                    EditorGUILayout.EndHorizontal();

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

        private void DrawConstantField(SerializedProperty entryProp, ParamInfo info)
        {
            Type fieldType = info.fieldType;

            if (info.isRoleDropdown)
            {
                DrawRoleDropdown(entryProp);
                return;
            }

            if (info.isOrderDropdown)
            {
                DrawOrderDropdown(entryProp);
                return;
            }

            if (fieldType != null && fieldType.IsEnum)
            {
                SerializedProperty prop = entryProp.FindPropertyRelative("intValue");
                int currentRaw = prop.intValue;
                Enum current = (Enum)Enum.ToObject(fieldType, currentRaw);
                Enum next = EditorGUILayout.EnumPopup("Value", current, GUILayout.Width(FieldLabelWidth + DropdownFieldWidth));
                prop.intValue = Convert.ToInt32(next);
            }
            else if (fieldType == typeof(int) || fieldType == typeof(uint))
            {
                SerializedProperty prop = entryProp.FindPropertyRelative("intValue");
                prop.intValue = EditorGUILayout.IntField("Value", prop.intValue, GUILayout.Width(FieldLabelWidth + DropdownFieldWidth));
            }
            else if (fieldType == typeof(float))
            {
                SerializedProperty prop = entryProp.FindPropertyRelative("floatValue");
                prop.floatValue = EditorGUILayout.FloatField("Value", prop.floatValue, GUILayout.Width(FieldLabelWidth + DropdownFieldWidth));
            }
            else if (fieldType == typeof(bool))
            {
                SerializedProperty prop = entryProp.FindPropertyRelative("boolValue");
                prop.boolValue = EditorGUILayout.Toggle("Value", prop.boolValue, GUILayout.Width(FieldLabelWidth + DropdownFieldWidth));
            }
            else if (fieldType == typeof(Vector2))
            {
                SerializedProperty prop = entryProp.FindPropertyRelative("vector2Value");
                prop.vector2Value = EditorGUILayout.Vector2Field("Value", prop.vector2Value, GUILayout.Width(FieldLabelWidth + DropdownFieldWidth));
            }
            else if (fieldType == typeof(Vector3))
            {
                SerializedProperty prop = entryProp.FindPropertyRelative("vector3Value");
                prop.vector3Value = EditorGUILayout.Vector3Field("Value", prop.vector3Value, GUILayout.Width(FieldLabelWidth + DropdownFieldWidth));
            }
            else if (typeof(UnityEngine.Object).IsAssignableFrom(fieldType))
            {
                SerializedProperty prop = fieldType == typeof(GameObject)
                    ? entryProp.FindPropertyRelative("gameObjectValue")
                    : entryProp.FindPropertyRelative("transformValue");
                prop.objectReferenceValue = EditorGUILayout.ObjectField("Value", prop.objectReferenceValue, fieldType, true, GUILayout.Width(FieldLabelWidth + DropdownFieldWidth));
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

            // Prepend placeholder so unassigned fields don't auto-pick the first variable
            matchingVars.Insert(0, "Select a variable...");
            matchingVarNames.Insert(0, string.Empty);

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
                string previousVal = variableNameProp.stringValue;
                selectedIndex = EditorGUILayout.Popup("Shared Variable", selectedIndex, matchingVars.ToArray(), GUILayout.Width(FieldLabelWidth + DropdownFieldWidth));
                variableNameProp.stringValue = matchingVarNames[selectedIndex];
                if (variableNameProp.stringValue != previousVal)
                    nodeVisualsChangedThisFrame = true;
            }
        }

        // ── Conditional Abort Validation ──

        private void DrawRoleDropdown(SerializedProperty entryProp)
        {
            SerializedProperty intValueProp = entryProp.FindPropertyRelative("intValue");
            if (intValueProp == null) return;

            BaseEditorTreeAsset tree = BehaviourTreeEditor.currentTree;

            CommanderTreeAsset commanderTree = tree as CommanderTreeAsset;
            if (commanderTree == null)
            {
                EditorGUILayout.HelpBox("Role dropdown only available on commander trees.", MessageType.Warning);
                return;
            }

            SquadDefinition squad = commanderTree.commanderSquad;
            if (squad == null)
            {
                EditorGUILayout.HelpBox("No commander squad assigned. Configure it in the Commander tab.", MessageType.Warning);
                return;
            }

            if (squad.availableRoles == null || squad.availableRoles.Count == 0)
            {
                EditorGUILayout.HelpBox("No roles defined in the commander squad.", MessageType.Warning);
                return;
            }

            List<SquadRole> roles = squad.availableRoles;
            string[] roleNames = new string[roles.Count];
            for (int roleIndex = 0; roleIndex < roles.Count; roleIndex++)
                roleNames[roleIndex] = roles[roleIndex].name;

            int currentIndex = intValueProp.intValue;
            if (currentIndex < 0 || currentIndex >= roles.Count) currentIndex = 0;

            if (InspectorView.IsRenderingReadOnly)
            {
                EditorGUILayout.LabelField("Role", roleNames[currentIndex]);
            }
            else
            {
                int newIndex = EditorGUILayout.Popup("Role", currentIndex, roleNames, GUILayout.Width(FieldLabelWidth + DropdownFieldWidth));
                intValueProp.intValue = newIndex;
            }
        }

        private void DrawOrderDropdown(SerializedProperty entryProp)
        {
            SerializedProperty stringValueProp = entryProp.FindPropertyRelative("stringValue");
            SerializedProperty intValueProp = entryProp.FindPropertyRelative("intValue");
            SerializedProperty isOrderProp = entryProp.FindPropertyRelative("isOrderConstant");
            if (stringValueProp == null || intValueProp == null) return;

            if (isOrderProp != null)
                isOrderProp.boolValue = true;

            OrderRegistry registry = OrderRegistry.FindInstance();
            if (registry == null || registry.orderNames == null || registry.orderNames.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No OrderRegistry asset found. Create one via Assets > Create > BehaviourTree > Order Registry.",
                    MessageType.Warning);
                return;
            }

            string currentName = stringValueProp.stringValue;
            int currentIndex = 0;
            if (!string.IsNullOrEmpty(currentName))
            {
                currentIndex = registry.orderNames.IndexOf(currentName);
                if (currentIndex < 0) currentIndex = 0;
            }

            if (InspectorView.IsRenderingReadOnly)
            {
                string displayName = currentIndex < registry.orderNames.Count
                    ? registry.orderNames[currentIndex]
                    : "(unknown)";
                EditorGUILayout.LabelField("Value", displayName);
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("Value");
                string buttonLabel = !string.IsNullOrEmpty(currentName) ? currentName : "Select Order...";
                if (GUILayout.Button(buttonLabel, EditorStyles.popup, GUILayout.Width(DropdownFieldWidth), GUILayout.Height(EditorGUIUtility.singleLineHeight)))
                {
                    OrderSearchProvider provider = ScriptableObject.CreateInstance<OrderSearchProvider>();
                    provider.registry = registry;
                    provider.onOrderSelected = name =>
                    {
                        stringValueProp.stringValue = name;
                        intValueProp.intValue = registry.orderNames.IndexOf(name);
                        stringValueProp.serializedObject.ApplyModifiedProperties();
                    };
                    SearchWindow.Open(
                        new SearchWindowContext(GUIUtility.GUIToScreenPoint(
                            Event.current.mousePosition)), provider);
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // Dynamic-Type Node Methods (SetVariable, ClearVariable, CompareVariable)
        // ═══════════════════════════════════════════════════════════════

        private const string SetVariableName = "SetVariable";
        private const string ClearVariableName = "ClearVariable";
        private const string CompareVariableName = "CompareVariable";
        private const string LogVariableName = "LogVariable";

        private static int GetDynamicParameterCount(string methodName)
        {
            return methodName switch
            {
                SetVariableName => 2,
                ClearVariableName => 1,
                CompareVariableName => 3,
                LogVariableName => 1,
                _ => 0
            };
        }

        private void BuildDynamicFieldEntries(string methodName, int paramCount, bool methodChanged)
        {
            // Resize entries on method change
            if (methodChanged)
            {
                while (fieldEntriesProp.arraySize < paramCount)
                    fieldEntriesProp.InsertArrayElementAtIndex(fieldEntriesProp.arraySize);
                while (fieldEntriesProp.arraySize > paramCount)
                    fieldEntriesProp.DeleteArrayElementAtIndex(fieldEntriesProp.arraySize - 1);
            }

            if (fieldEntriesProp.arraySize < paramCount)
                return;

            // Read the currently selected type from entry 0's fieldTypeName
            Type selectedType = null;
            string typeName = string.Empty;
            if (fieldEntriesProp.arraySize > 0)
            {
                SerializedProperty typeProp = fieldEntriesProp.GetArrayElementAtIndex(0)
                    .FindPropertyRelative("fieldTypeName");
                typeName = typeProp?.stringValue ?? string.Empty;
                if (!string.IsNullOrEmpty(typeName))
                    selectedType = FieldTypeHelper.TryGetSystemTypeFromName(typeName, out Type resolvedType) ? resolvedType : null;
            }

            // ── Type display (read-only; type changes via filter button next to variable dropdown) ──
            EditorGUILayout.BeginVertical("box");

            string typeLabel = selectedType != null ? selectedType.Name : "(none)";
            EditorGUILayout.LabelField($"<b>Variable Type</b> : <color=lightblue>{typeLabel}</color>", RichTextLabelStyle);

            EditorGUILayout.Space();

            // ── Node-specific fields ──
            switch (methodName)
            {
                case SetVariableName:
                    DrawSetVariableFields(selectedType);
                    break;
                case ClearVariableName:
                    DrawClearVariableFields(selectedType);
                    break;
                case CompareVariableName:
                    DrawCompareVariableFields(selectedType);
                    break;
                case LogVariableName:
                    DrawLogVariableFields(selectedType);
                    break;
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawSetVariableFields(Type selectedType)
        {
            if (fieldEntriesProp.arraySize < 2) return;

            SerializedProperty targetEntry = fieldEntriesProp.GetArrayElementAtIndex(0);
            SerializedProperty valueEntry = fieldEntriesProp.GetArrayElementAtIndex(1);
            bool isArray = fieldEntriesProp.arraySize > 0
                ? fieldEntriesProp.GetArrayElementAtIndex(0).FindPropertyRelative("isArray").boolValue
                : false;

            // Ensure field names are set
            targetEntry.FindPropertyRelative("fieldName").stringValue = "target";
            targetEntry.FindPropertyRelative("isVariable").boolValue = true;
            valueEntry.FindPropertyRelative("fieldName").stringValue = "value";

            // Target variable dropdown (with search + filter buttons)
            DrawDynamicVariableField(targetEntry.FindPropertyRelative("variableName"), selectedType, isArray, "Target");

            EditorGUILayout.Space();

            // Value: toggle between constant and variable
            SerializedProperty valueIsVar = valueEntry.FindPropertyRelative("isVariable");
            EditorGUILayout.LabelField("<b>Value</b>", RichTextLabelStyle);

            EditorGUILayout.BeginHorizontal();

            if (valueIsVar.boolValue)
            {
                DrawVariableDropdownWithSquadFilter(valueEntry.FindPropertyRelative("variableName"), selectedType, isArray);
            }
            else
            {
                DrawConstantFieldForType(valueEntry, selectedType);
            }

            GUILayout.FlexibleSpace();

            // C/V toggle button
            {
                string toggleLabel = valueIsVar.boolValue ? "C" : "V";
                string toggleTooltip = valueIsVar.boolValue ? "Switch to constant" : "Switch to variable";
                if (GUILayout.Button(new GUIContent(toggleLabel, toggleTooltip), GUILayout.Width(SmallButtonWidth), GUILayout.Height(EditorGUIUtility.singleLineHeight)))
                {
                    valueIsVar.boolValue = !valueIsVar.boolValue;
                    nodeVisualsChangedThisFrame = true;
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawClearVariableFields(Type selectedType)
        {
            if (fieldEntriesProp.arraySize < 1) return;

            SerializedProperty targetEntry = fieldEntriesProp.GetArrayElementAtIndex(0);
            bool isArray = fieldEntriesProp.arraySize > 0
                ? fieldEntriesProp.GetArrayElementAtIndex(0).FindPropertyRelative("isArray").boolValue
                : false;

            targetEntry.FindPropertyRelative("fieldName").stringValue = "target";
            targetEntry.FindPropertyRelative("isVariable").boolValue = true;

            DrawDynamicVariableField(targetEntry.FindPropertyRelative("variableName"), selectedType, isArray, "Target");
        }

        private void DrawLogVariableFields(Type selectedType)
        {
            if (fieldEntriesProp.arraySize < 1) return;

            SerializedProperty variableEntry = fieldEntriesProp.GetArrayElementAtIndex(0);
            bool isArray = fieldEntriesProp.arraySize > 0
                ? fieldEntriesProp.GetArrayElementAtIndex(0).FindPropertyRelative("isArray").boolValue
                : false;

            variableEntry.FindPropertyRelative("fieldName").stringValue = "variable";
            variableEntry.FindPropertyRelative("isVariable").boolValue = true;

            DrawDynamicVariableField(variableEntry.FindPropertyRelative("variableName"), selectedType, isArray, "Variable");
        }

        private void DrawCompareVariableFields(Type selectedType)
        {
            if (fieldEntriesProp.arraySize < 3) return;

            SerializedProperty entryA = fieldEntriesProp.GetArrayElementAtIndex(0);
            SerializedProperty entryB = fieldEntriesProp.GetArrayElementAtIndex(1);
            SerializedProperty entryOp = fieldEntriesProp.GetArrayElementAtIndex(2);
            bool isArray = fieldEntriesProp.arraySize > 0
                ? fieldEntriesProp.GetArrayElementAtIndex(0).FindPropertyRelative("isArray").boolValue
                : false;

            entryA.FindPropertyRelative("fieldName").stringValue = "a";
            entryA.FindPropertyRelative("isVariable").boolValue = true;
            entryB.FindPropertyRelative("fieldName").stringValue = "b";
            entryOp.FindPropertyRelative("fieldName").stringValue = "operation";
            entryOp.FindPropertyRelative("isVariable").boolValue = false;
            entryOp.FindPropertyRelative("fieldTypeName").stringValue = typeof(int).AssemblyQualifiedName;

            // Variable A
            DrawDynamicVariableField(entryA.FindPropertyRelative("variableName"), selectedType, isArray, "Operand A");

            EditorGUILayout.Space();

            // Value B: constant or from variable
            SerializedProperty bIsVar = entryB.FindPropertyRelative("isVariable");
            EditorGUILayout.LabelField("<b>Compare With</b>", RichTextLabelStyle);

            EditorGUILayout.BeginHorizontal();

            if (bIsVar.boolValue)
            {
                DrawVariableDropdownWithSquadFilter(entryB.FindPropertyRelative("variableName"), selectedType, isArray);
            }
            else
            {
                DrawConstantFieldForType(entryB, selectedType);
            }

            GUILayout.FlexibleSpace();

            // C/V toggle button
            {
                string toggleLabel = bIsVar.boolValue ? "C" : "V";
                string toggleTooltip = bIsVar.boolValue ? "Switch to constant" : "Switch to variable";
                if (GUILayout.Button(new GUIContent(toggleLabel, toggleTooltip), GUILayout.Width(SmallButtonWidth), GUILayout.Height(EditorGUIUtility.singleLineHeight)))
                {
                    bIsVar.boolValue = !bIsVar.boolValue;
                    nodeVisualsChangedThisFrame = true;
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // Operation dropdown
            SerializedProperty intValueProp = entryOp.FindPropertyRelative("intValue");
            string[] opNames = GetCompareOpNames(selectedType);
            int currentOp = intValueProp.intValue;
            if (currentOp < 0 || currentOp >= opNames.Length) currentOp = 0;
            intValueProp.intValue = EditorGUILayout.Popup("Operation", currentOp, opNames, GUILayout.Width(FieldLabelWidth + DropdownFieldWidth));
        }

        /// <summary>
        /// Variable dropdown filtered by type and stride mode (array vs singular).
        /// When isArray is true, only shows stride > 1 variables (squad data).
        /// When isArray is false, only shows stride <= 1 variables (singular).
        /// </summary>
        private void DrawVariableDropdownWithSquadFilter(SerializedProperty variableNameProp, Type expectedType, bool isArray)
        {
            if (expectedType == null)
            {
                EditorGUILayout.HelpBox("Select a variable type first using the 'F' (filter) button next to the first variable dropdown.", MessageType.Info);
                return;
            }

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

            matchingVars.Clear();
            matchingVarNames.Clear();
            for (int variableIndex = 0; variableIndex < allVars.Count; variableIndex++)
            {
                BlackboardVariableBase bv = allVars[variableIndex];
                Type bbType = bv.GetValueType();
                if (bbType == null) continue;
                if (bbType != expectedType) continue;

                // Filter by stride: array mode shows stride > 1, singular shows stride <= 1
                if (isArray)
                {
                    if (bv.Stride <= 1) continue;
                    matchingVars.Add($"{bv.Name} [{bv.Stride}]");
                }
                else
                {
                    if (bv.Stride > 1) continue;
                    matchingVars.Add(bv.Name);
                }
                matchingVarNames.Add(bv.Name);
            }

            if (matchingVars.Count == 0)
            {
                string modeLabel = isArray ? "array" : "singular";
                EditorGUILayout.HelpBox(
                    $"No {modeLabel} variable of type '{expectedType.Name}' in Blackboard. " +
                    "Use the 'F' (filter) button to change the type or array/singular mode.",
                    MessageType.Info);
                variableNameProp.stringValue = "";
                return;
            }

            matchingVars.Insert(0, "Select a variable...");
            matchingVarNames.Insert(0, string.Empty);

            string currentVal = variableNameProp.stringValue;
            int selectedIndex = matchingVarNames.IndexOf(currentVal);
            if (selectedIndex < 0) selectedIndex = 0;

            if (InspectorView.IsRenderingReadOnly)
            {
                EditorGUILayout.LabelField("Variable", currentVal);
            }
            else
            {
                string previousVal = variableNameProp.stringValue;
                selectedIndex = EditorGUILayout.Popup("Variable", selectedIndex, matchingVars.ToArray(), GUILayout.Width(FieldLabelWidth + DropdownFieldWidth));
                variableNameProp.stringValue = matchingVarNames[selectedIndex];
                if (variableNameProp.stringValue != previousVal)
                    nodeVisualsChangedThisFrame = true;
            }
        }

        /// <summary>
        /// Draws the primary variable field for dynamic-type nodes with two inline
        /// buttons: a search button (variable by name) and a filter button (change type).
        /// </summary>
        private void DrawDynamicVariableField(SerializedProperty variableNameProp, Type selectedType, bool isArray, string label)
        {
            if (InspectorView.IsRenderingReadOnly)
            {
                EditorGUILayout.LabelField(label, variableNameProp.stringValue);
                return;
            }

            BlackboardDefinition blackboardDef = BehaviourTreeEditor.currentBlackboardDef;
            bool squadCtx = BehaviourTreeEditor.currentTree is CommanderTreeAsset;

            float savedLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = FieldLabelWidth;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(label);

            if (selectedType != null && blackboardDef != null)
            {
                // ── Compact dropdown (no label) ──
                IReadOnlyList<BlackboardVariableBase> allVars = blackboardDef.GetAllVariables();
                matchingVars.Clear();
                matchingVarNames.Clear();
                for (int variableIndex = 0; variableIndex < allVars.Count; variableIndex++)
                {
                    BlackboardVariableBase bv = allVars[variableIndex];
                    Type bbType = bv.GetValueType();
                    if (bbType == null || bbType != selectedType) continue;
                    if (isArray) { if (bv.Stride <= 1) continue; matchingVars.Add($"{bv.Name} [{bv.Stride}]"); }
                    else { if (bv.Stride > 1) continue; matchingVars.Add(bv.Name); }
                    matchingVarNames.Add(bv.Name);
                }

                if (matchingVars.Count > 0)
                {
                    matchingVars.Insert(0, "Select a variable...");
                    matchingVarNames.Insert(0, string.Empty);
                    string currentVal = variableNameProp.stringValue;
                    int selectedIndex = matchingVarNames.IndexOf(currentVal);
                    if (selectedIndex < 0) selectedIndex = 0;
                    string previousVal = currentVal;
                    selectedIndex = EditorGUILayout.Popup(selectedIndex, matchingVars.ToArray(), GUILayout.Width(DropdownFieldWidth), GUILayout.Height(EditorGUIUtility.singleLineHeight));
                    variableNameProp.stringValue = matchingVarNames[selectedIndex];
                    if (variableNameProp.stringValue != previousVal)
                        nodeVisualsChangedThisFrame = true;
                }
                else
                {
                    EditorGUILayout.LabelField("(no matches)", GUILayout.Width(DropdownFieldWidth), GUILayout.Height(EditorGUIUtility.singleLineHeight));
                }
            }
            else
            {
                EditorGUILayout.LabelField("(pick a variable below)", GUILayout.Width(DropdownFieldWidth), GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }

            GUILayout.FlexibleSpace();

            // ── S + F buttons ──
            Rect searchButtonRect = EditorGUILayout.GetControlRect(GUILayout.Width(SmallButtonWidth), GUILayout.Height(EditorGUIUtility.singleLineHeight));
            if (GUI.Button(searchButtonRect, new GUIContent("S", "Search for a variable by name")))
            {
                GUI.FocusControl(null);
                VariableSearchPopup popup = new VariableSearchPopup(blackboardDef, null,
                    (BlackboardVariableBase chosen, bool chosenIsArray) =>
                    {
                        if (chosen == null) return;
                        // Auto-configure type and array mode from chosen variable
                        Type varType = chosen.GetValueType();
                        string newTypeName = varType?.AssemblyQualifiedName ?? string.Empty;
                        for (int i = 0; i < fieldEntriesProp.arraySize; i++)
                        {
                            SerializedProperty ftProp = fieldEntriesProp.GetArrayElementAtIndex(i)
                                .FindPropertyRelative("fieldTypeName");
                            if (ftProp != null) ftProp.stringValue = newTypeName;
                        }
                        fieldEntriesProp.GetArrayElementAtIndex(0)
                            .FindPropertyRelative("isArray").boolValue = chosen.Stride > 1;
                        variableNameProp.stringValue = chosen.Name;
                        fieldEntriesProp.serializedObject.ApplyModifiedProperties();
                        nodeVisualsChangedThisFrame = true;
                    }, isSquadContext: squadCtx);
                // IMGUI is in the editor window's GUI space; only scroll offset needs correction
                UnityEditor.PopupWindow.Show(
                    new Rect(searchButtonRect.x, searchButtonRect.yMax - InspectorView.InspectorScrollOffset.y, 0, 0),
                    popup);
            }

            // ── Filter (type) button ──
            Rect filterButtonRect = EditorGUILayout.GetControlRect(GUILayout.Width(SmallButtonWidth), GUILayout.Height(EditorGUIUtility.singleLineHeight));
            if (GUI.Button(filterButtonRect, new GUIContent("F", "Change the variable type filter")))
            {
                GUI.FocusControl(null);
                VariableTypeSearchPopup popup = new VariableTypeSearchPopup(
                    (Type varType, bool varIsArray, int stride, bool isSquadData) =>
                    {
                        string newTypeName = varType?.AssemblyQualifiedName ?? string.Empty;
                        for (int i = 0; i < fieldEntriesProp.arraySize; i++)
                        {
                            SerializedProperty ftProp = fieldEntriesProp.GetArrayElementAtIndex(i)
                                .FindPropertyRelative("fieldTypeName");
                            if (ftProp != null) ftProp.stringValue = newTypeName;
                        }
                        fieldEntriesProp.GetArrayElementAtIndex(0)
                            .FindPropertyRelative("isArray").boolValue = varIsArray || isSquadData;
                        for (int i = 0; i < fieldEntriesProp.arraySize; i++)
                        {
                            SerializedProperty varProp = fieldEntriesProp.GetArrayElementAtIndex(i)
                                .FindPropertyRelative("variableName");
                            if (varProp != null) varProp.stringValue = string.Empty;
                        }
                        fieldEntriesProp.serializedObject.ApplyModifiedProperties();
                        nodeVisualsChangedThisFrame = true;
                    }, isSquadContext: squadCtx);
                PopupWindow.Show(
                    new Rect(filterButtonRect.x, filterButtonRect.yMax - InspectorView.InspectorScrollOffset.y, 0, 0),
                    popup);
            }

            EditorGUILayout.EndHorizontal();
            EditorGUIUtility.labelWidth = savedLabelWidth;
        }

        /// <summary>
        /// Renders a constant field appropriate for the given type,
        /// reading/writing to the NodeFieldEntry's typed value properties.
        /// </summary>
        private void DrawConstantFieldForType(SerializedProperty entryProp, Type fieldType)
        {
            if (fieldType == null)
            {
                EditorGUILayout.HelpBox("No type selected.", MessageType.Warning);
                return;
            }

            if (fieldType.IsEnum)
            {
                SerializedProperty prop = entryProp.FindPropertyRelative("intValue");
                int currentRaw = prop.intValue;
                Enum current = (Enum)Enum.ToObject(fieldType, currentRaw);
                Enum next = EditorGUILayout.EnumPopup("Value", current, GUILayout.Width(FieldLabelWidth + DropdownFieldWidth));
                prop.intValue = Convert.ToInt32(next);
            }
            else if (fieldType == typeof(int) || fieldType == typeof(uint))
            {
                SerializedProperty prop = entryProp.FindPropertyRelative("intValue");
                prop.intValue = EditorGUILayout.IntField("Value", prop.intValue, GUILayout.Width(FieldLabelWidth + DropdownFieldWidth));
            }
            else if (fieldType == typeof(float))
            {
                SerializedProperty prop = entryProp.FindPropertyRelative("floatValue");
                prop.floatValue = EditorGUILayout.FloatField("Value", prop.floatValue, GUILayout.Width(FieldLabelWidth + DropdownFieldWidth));
            }
            else if (fieldType == typeof(bool))
            {
                SerializedProperty prop = entryProp.FindPropertyRelative("boolValue");
                prop.boolValue = EditorGUILayout.Toggle("Value", prop.boolValue, GUILayout.Width(FieldLabelWidth + DropdownFieldWidth));
            }
            else if (fieldType == typeof(Vector2))
            {
                SerializedProperty prop = entryProp.FindPropertyRelative("vector2Value");
                prop.vector2Value = EditorGUILayout.Vector2Field("Value", prop.vector2Value, GUILayout.Width(FieldLabelWidth + DropdownFieldWidth));
            }
            else if (fieldType == typeof(Vector3))
            {
                SerializedProperty prop = entryProp.FindPropertyRelative("vector3Value");
                prop.vector3Value = EditorGUILayout.Vector3Field("Value", prop.vector3Value, GUILayout.Width(FieldLabelWidth + DropdownFieldWidth));
            }
            else if (typeof(UnityEngine.Object).IsAssignableFrom(fieldType))
            {
                SerializedProperty prop = fieldType == typeof(GameObject)
                    ? entryProp.FindPropertyRelative("gameObjectValue")
                    : entryProp.FindPropertyRelative("transformValue");
                prop.objectReferenceValue = EditorGUILayout.ObjectField("Value", prop.objectReferenceValue, fieldType, true, GUILayout.Width(FieldLabelWidth + DropdownFieldWidth));
            }
            else
            {
                EditorGUILayout.HelpBox(
                    $"Type '{fieldType.Name}' is not supported for constant values. Use a variable source instead.",
                    MessageType.Warning);
            }
        }

        /// <summary>
        /// Returns the subset of VariableCompareOp names applicable to the given type.
        /// </summary>
        private static string[] GetCompareOpNames(Type fieldType)
        {
            if (fieldType == null)
                return new[] { "Equal", "NotEqual" };

            if (fieldType == typeof(Vector2) || fieldType == typeof(Vector3))
                return new[] { "Equal", "NotEqual", "Mag <", "Mag <=", "Mag >", "Mag >=" };

            if (fieldType == typeof(int) || fieldType == typeof(float))
                return new[] { "Equal", "NotEqual", "Less", "LessOrEqual", "Greater", "GreaterOrEqual" };

            // bool, enum, GameObject, Transform, etc.
            return new[] { "Equal", "NotEqual" };
        }

        private static bool HasValidConditionForAbort(CompositeNode composite, AbortType abortType)
        {
            return NodeWarningEvaluator.HasValidConditionForAbort(composite, abortType);
        }
    }
}
