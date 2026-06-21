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

                    if (info.isHidden)
                    {
                        // Auto-fill variable binding by convention — no UI rendered
                        if (methodChanged)
                        {
                            isVariableProp.boolValue = true;
                            variableNameProp.stringValue = info.autoVariableName;
                        }
                        EditorGUILayout.EndVertical();
                        continue;
                    }

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
                        DrawConstantField(entryProp, info);
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
                selectedIndex = EditorGUILayout.Popup("Shared Variable", selectedIndex, matchingVars.ToArray());
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
                int newIndex = EditorGUILayout.Popup("Role", currentIndex, roleNames);
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
                EditorGUILayout.LabelField("Order", displayName);
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                string buttonLabel = !string.IsNullOrEmpty(currentName) ? currentName : "Select Order...";
                if (GUILayout.Button(buttonLabel, EditorStyles.popup))
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
                EditorGUILayout.LabelField(currentIndex.ToString(), GUILayout.Width(30));
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

            // ── Type picker (shared by all three nodes) ──
            EditorGUILayout.BeginVertical("box");
            string typeLabel = selectedType != null ? selectedType.Name : "(none)";
            EditorGUILayout.LabelField($"<b>Variable Type</b> : <color=lightblue>{typeLabel}</color>", RichTextLabelStyle);

            if (!InspectorView.IsRenderingReadOnly)
            {
                if (GUILayout.Button("Pick Type...", GUILayout.Height(24)))
                {
                    VariableTypeSearchPopup popup = new VariableTypeSearchPopup(
                        (Type varType, bool isArray, int stride, bool isSquadData) =>
                        {
                            string newTypeName = varType?.AssemblyQualifiedName ?? string.Empty;
                            // Update fieldTypeName on all entries
                            for (int i = 0; i < paramCount && i < fieldEntriesProp.arraySize; i++)
                            {
                                SerializedProperty ftProp = fieldEntriesProp.GetArrayElementAtIndex(i)
                                    .FindPropertyRelative("fieldTypeName");
                                if (ftProp != null) ftProp.stringValue = newTypeName;
                            }
                            // Store array mode on the first entry — drives dropdown filtering
                            if (fieldEntriesProp.arraySize > 0)
                            {
                                fieldEntriesProp.GetArrayElementAtIndex(0)
                                    .FindPropertyRelative("isArray").boolValue = isArray || isSquadData;
                            }
                            // Clear variable selections (type may not match anymore)
                            for (int i = 0; i < paramCount && i < fieldEntriesProp.arraySize; i++)
                            {
                                SerializedProperty varProp = fieldEntriesProp.GetArrayElementAtIndex(i)
                                    .FindPropertyRelative("variableName");
                                if (varProp != null) varProp.stringValue = string.Empty;
                            }
                            fieldEntriesProp.serializedObject.ApplyModifiedProperties();
                        }, isSquadContext: true);
                    PopupWindow.Show(new Rect(GUIUtility.GUIToScreenPoint(Event.current.mousePosition), Vector2.zero), popup);
                }
            }
            EditorGUILayout.EndVertical();

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

            // Target variable dropdown
            DrawVariableDropdownWithSquadFilter(targetEntry.FindPropertyRelative("variableName"), selectedType, isArray);

            EditorGUILayout.Space();

            // Value: toggle between constant and variable
            SerializedProperty valueIsVar = valueEntry.FindPropertyRelative("isVariable");
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("<b>Value</b>", RichTextLabelStyle);

            valueIsVar.boolValue = EditorGUILayout.Toggle("From Variable", valueIsVar.boolValue);

            if (valueIsVar.boolValue)
            {
                DrawVariableDropdownWithSquadFilter(valueEntry.FindPropertyRelative("variableName"), selectedType, isArray);
            }
            else
            {
                DrawConstantFieldForType(valueEntry, selectedType);
            }
            EditorGUILayout.EndVertical();
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

            DrawVariableDropdownWithSquadFilter(targetEntry.FindPropertyRelative("variableName"), selectedType, isArray);
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

            DrawVariableDropdownWithSquadFilter(variableEntry.FindPropertyRelative("variableName"), selectedType, isArray);
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
            DrawVariableDropdownWithSquadFilter(entryA.FindPropertyRelative("variableName"), selectedType, isArray);

            EditorGUILayout.Space();

            // Value B: constant or from variable
            SerializedProperty bIsVar = entryB.FindPropertyRelative("isVariable");
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("<b>Compare With</b>", RichTextLabelStyle);
            bIsVar.boolValue = EditorGUILayout.Toggle("From Variable", bIsVar.boolValue);

            if (bIsVar.boolValue)
            {
                DrawVariableDropdownWithSquadFilter(entryB.FindPropertyRelative("variableName"), selectedType, isArray);
            }
            else
            {
                DrawConstantFieldForType(entryB, selectedType);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            // Operation dropdown
            SerializedProperty intValueProp = entryOp.FindPropertyRelative("intValue");
            string[] opNames = GetCompareOpNames(selectedType);
            int currentOp = intValueProp.intValue;
            if (currentOp < 0 || currentOp >= opNames.Length) currentOp = 0;
            intValueProp.intValue = EditorGUILayout.Popup("Operation", currentOp, opNames);
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
                EditorGUILayout.HelpBox("Select a variable type first using the 'Pick Type' button above.", MessageType.Info);
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
                    "Use the 'Pick Type' button to change the type or array/singular mode.",
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
                selectedIndex = EditorGUILayout.Popup("Variable", selectedIndex, matchingVars.ToArray());
                variableNameProp.stringValue = matchingVarNames[selectedIndex];
                if (variableNameProp.stringValue != previousVal)
                    nodeVisualsChangedThisFrame = true;
            }
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
