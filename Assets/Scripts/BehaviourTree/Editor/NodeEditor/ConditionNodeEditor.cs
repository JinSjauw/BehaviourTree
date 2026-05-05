// #if UNITY_EDITOR
// using UnityEditor;
// using UnityEngine;
// using System.Collections.Generic;
// using BehaviourTree.Core;

// namespace BehaviourTree.Editor
// {
//     [CustomEditor(typeof(ConditionNode))]
//     public class ConditionNodeEditor : UnityEditor.Editor
//     {
//         private SerializedProperty methodIDProp;
//         private SerializedProperty fieldEntriesProp;
//         private SerializedProperty blackBoardTypeIDProp;
//         private SerializedProperty treeConfigIDProp;

//         private void OnEnable()
//         {
//             if (target == null) return;

//             methodIDProp = serializedObject.FindProperty("methodID");
//             fieldEntriesProp = serializedObject.FindProperty("fieldEntries");
//             blackBoardTypeIDProp = serializedObject.FindProperty("BlackBoardTypeID");
//             treeConfigIDProp = serializedObject.FindProperty("treeConfigID");
//         }

//         public override void OnInspectorGUI()
//         {
//             serializedObject.Update();

//             EditorGUILayout.PropertyField(methodIDProp);

//             MethodID selectedMethod = (MethodID)methodIDProp.enumValueIndex;
//             List<ParamInfo> paramInfoList = MethodMetadataCache.GetParamsForMethod(selectedMethod);

//             if (paramInfoList != null && paramInfoList.Count > 0)
//             {
//                 while (fieldEntriesProp.arraySize < paramInfoList.Count)
//                     fieldEntriesProp.InsertArrayElementAtIndex(fieldEntriesProp.arraySize);
//                 while (fieldEntriesProp.arraySize > paramInfoList.Count)
//                     fieldEntriesProp.DeleteArrayElementAtIndex(fieldEntriesProp.arraySize - 1);

//                 for (int i = 0; i < paramInfoList.Count; i++)
//                 {
//                     ParamInfo info = paramInfoList[i];
//                     SerializedProperty entryProp = fieldEntriesProp.GetArrayElementAtIndex(i);
//                     SerializedProperty fieldNameProp = entryProp.FindPropertyRelative("fieldName");
//                     SerializedProperty isVariableProp = entryProp.FindPropertyRelative("isVariable");
//                     SerializedProperty variableNameProp = entryProp.FindPropertyRelative("variableName");
//                     SerializedProperty fieldTypeProp = entryProp.FindPropertyRelative("fieldType");

//                     fieldNameProp.stringValue = info.fieldName;
//                     fieldTypeProp.enumValueIndex = (int)GetNodeFieldType(info.fieldType);

//                     EditorGUILayout.BeginVertical("box");
//                     EditorGUILayout.LabelField($"{info.fieldName} ({info.fieldType.Name})");

//                     isVariableProp.boolValue = EditorGUILayout.Toggle("Use Blackboard Variable", isVariableProp.boolValue);

//                     if (isVariableProp.boolValue)
//                     {
//                         DrawVariableDropdown(variableNameProp, info.fieldType);
//                     }
//                     else
//                     {
//                         DrawConstantField(entryProp, info.fieldType);
//                     }

//                     EditorGUILayout.EndVertical();
//                 }
//             }
//             else
//             {
//                 fieldEntriesProp.ClearArray();
//                 EditorGUILayout.HelpBox($"No *_Params struct found for method {selectedMethod}. Create a struct named {selectedMethod}_Params.", MessageType.Warning);
//             }

//             EditorGUILayout.Space();
//             EditorGUILayout.PropertyField(blackBoardTypeIDProp);
//             EditorGUILayout.PropertyField(treeConfigIDProp);

//             serializedObject.ApplyModifiedProperties();
//         }

//         private void DrawConstantField(SerializedProperty entryProp, System.Type fieldType)
//         {
//             if (fieldType == typeof(int) || fieldType == typeof(uint))
//             {
//                 SerializedProperty prop = entryProp.FindPropertyRelative("intValue");
//                 prop.intValue = EditorGUILayout.IntField("Value", prop.intValue);
//             }
//             else if (fieldType == typeof(float))
//             {
//                 SerializedProperty prop = entryProp.FindPropertyRelative("floatValue");
//                 prop.floatValue = EditorGUILayout.FloatField("Value", prop.floatValue);
//             }
//             else if (fieldType == typeof(bool))
//             {
//                 SerializedProperty prop = entryProp.FindPropertyRelative("boolValue");
//                 prop.boolValue = EditorGUILayout.Toggle("Value", prop.boolValue);
//             }
//             else if (fieldType == typeof(Vector2))
//             {
//                 SerializedProperty prop = entryProp.FindPropertyRelative("vector2Value");
//                 prop.vector2Value = EditorGUILayout.Vector2Field("Value", prop.vector2Value);
//             }
//             else if (fieldType == typeof(Vector3))
//             {
//                 SerializedProperty prop = entryProp.FindPropertyRelative("vector3Value");
//                 prop.vector3Value = EditorGUILayout.Vector3Field("Value", prop.vector3Value);
//             }
//             else if (fieldType == typeof(GameObject))
//             {
//                 SerializedProperty prop = entryProp.FindPropertyRelative("gameObjectValue");
//                 prop.objectReferenceValue = EditorGUILayout.ObjectField("Value", prop.objectReferenceValue, typeof(GameObject), true);
//             }
//             else if (fieldType == typeof(Transform))
//             {
//                 SerializedProperty prop = entryProp.FindPropertyRelative("transformValue");
//                 prop.objectReferenceValue = EditorGUILayout.ObjectField("Value", prop.objectReferenceValue, typeof(Transform), true);
//             }
//         }

//         private void DrawVariableDropdown(SerializedProperty variableNameProp, System.Type expectedType)
//         {
//             BlackboardDefinition bbDef = BehaviourTreeEditor.currentBlackboardDef;

//             if (bbDef == null || bbDef.sharedVariables == null || bbDef.sharedVariables.Count == 0)
//             {
//                 EditorGUILayout.HelpBox("No Blackboard Definition assigned.", MessageType.Warning);
//                 return;
//             }

//             var matchingVars = new List<string>();
//             for (int v = 0; v < bbDef.sharedVariables.Count; v++)
//             {
//                 string typeName = bbDef.sharedVariables[v].typeName;
//                 System.Type bbType = System.Type.GetType(typeName);
//                 if (bbType == expectedType || (expectedType.IsValueType && bbType == expectedType))
//                 {
//                     matchingVars.Add(bbDef.sharedVariables[v].name);
//                 }
//             }

//             if (matchingVars.Count == 0)
//             {
//                 EditorGUILayout.HelpBox($"No matching variable of type {expectedType.Name} in Blackboard.", MessageType.Info);
//                 return;
//             }

//             string currentVal = variableNameProp.stringValue;
//             int selectedIndex = matchingVars.IndexOf(currentVal);
//             if (selectedIndex < 0) selectedIndex = 0;

//             selectedIndex = EditorGUILayout.Popup("Variable", selectedIndex, matchingVars.ToArray());
//             variableNameProp.stringValue = matchingVars[selectedIndex];
//         }

//         private NodeFieldType GetNodeFieldType(System.Type type)
//         {
//             if (type == typeof(int) || type == typeof(uint)) return NodeFieldType.Int;
//             if (type == typeof(float)) return NodeFieldType.Float;
//             if (type == typeof(bool)) return NodeFieldType.Bool;
//             if (type == typeof(Vector2)) return NodeFieldType.Vector2;
//             if (type == typeof(Vector3)) return NodeFieldType.Vector3;
//             if (type == typeof(GameObject)) return NodeFieldType.GameObject;
//             if (type == typeof(Transform)) return NodeFieldType.Transform;
//             return NodeFieldType.Int;
//         }
//     }
// }
// #endif