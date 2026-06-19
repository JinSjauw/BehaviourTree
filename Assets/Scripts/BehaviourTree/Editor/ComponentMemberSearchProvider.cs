using System;
using System.Collections.Generic;
using System.Reflection;
using BehaviourTree.Core;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace BehaviourTree.Editor
{
    /// <summary>
    /// Selected member info passed back when the user picks a component field/property
    /// in the SearchWindow.
    /// </summary>
    public readonly struct SelectedMemberInfo
    {
        public readonly Component TargetComponent;
        public readonly string MemberName;
        public readonly bool IsProperty;
        public readonly Type MemberType;

        public SelectedMemberInfo(Component component, string memberName, bool isProperty, Type memberType)
        {
            TargetComponent = component;
            MemberName = memberName;
            IsProperty = isProperty;
            MemberType = memberType;
        }
    }

    /// <summary>
    /// SearchWindow provider that builds a 3-level tree:
    ///   lv0: GameObject groups
    ///   lv1: User components (filtered by assembly)
    ///   lv2: Public fields/properties (filtered by VariableTypeRegistry compatibility)
    /// </summary>
    public class ComponentMemberSearchProvider : ScriptableObject, ISearchWindowProvider
    {
        /// <summary>GameObjects to scan for components.</summary>
        private List<GameObject> sourceGameObjects;

        /// <summary>Called when the user selects a member.</summary>
        private Action<SelectedMemberInfo> onMemberSelected;

        private Texture2D indentIcon;

        public void Initialize(List<GameObject> gameObjects, Action<SelectedMemberInfo> onSelected)
        {
            sourceGameObjects = gameObjects;
            onMemberSelected = onSelected;

            indentIcon = new Texture2D(1, 1);
            indentIcon.SetPixel(0, 0, Color.clear);
            indentIcon.Apply();
        }

        private void OnDestroy()
        {
            if (indentIcon != null)
            {
                DestroyImmediate(indentIcon);
                indentIcon = null;
            }
        }

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            List<SearchTreeEntry> searchList = new List<SearchTreeEntry>();
            searchList.Add(new SearchTreeGroupEntry(new GUIContent("Select Component Member"), 0));

            if (sourceGameObjects == null || sourceGameObjects.Count == 0)
            {
                searchList.Add(new SearchTreeEntry(new GUIContent("(no GameObjects available)"))
                    { level = 1, userData = null });
                return searchList;
            }

            IReadOnlyList<Type> supportedTypes = VariableTypeRegistry.Types;

            for (int goIndex = 0; goIndex < sourceGameObjects.Count; goIndex++)
            {
                GameObject go = sourceGameObjects[goIndex];
                if (go == null) continue;

                Component[] components = go.GetComponents<Component>();
                List<Component> userComponents = new List<Component>();

                for (int compIndex = 0; compIndex < components.Length; compIndex++)
                {
                    Component comp = components[compIndex];
                    if (comp == null) continue;

                    // Filter to user-defined assemblies only
                    string assemblyName = comp.GetType().Assembly.FullName;
                    if (assemblyName.StartsWith("UnityEngine") || assemblyName.StartsWith("UnityEditor"))
                        continue;

                    userComponents.Add(comp);
                }

                if (userComponents.Count == 0) continue;

                // GameObject group
                searchList.Add(new SearchTreeGroupEntry(new GUIContent(go.name), 1));

                for (int compIndex = 0; compIndex < userComponents.Count; compIndex++)
                {
                    Component comp = userComponents[compIndex];
                    Type componentType = comp.GetType();

                    // Collect compatible members
                    List<SearchTreeEntry> memberEntries = new List<SearchTreeEntry>();

                    // Fields
                    FieldInfo[] fields = componentType.GetFields(
                        BindingFlags.Public | BindingFlags.Instance);
                    for (int fi = 0; fi < fields.Length; fi++)
                    {
                        FieldInfo field = fields[fi];
                        if (!IsTypeSupported(field.FieldType, supportedTypes)) continue;

                        string display = $"{field.Name}  ({FieldTypeHelper.GetDisplayName(field.FieldType)})";
                        SelectedMemberInfo info = new SelectedMemberInfo(comp, field.Name, false, field.FieldType);
                        memberEntries.Add(new SearchTreeEntry(new GUIContent(display, indentIcon))
                            { level = 3, userData = info });
                    }

                    // Properties
                    PropertyInfo[] properties = componentType.GetProperties(
                        BindingFlags.Public | BindingFlags.Instance);
                    for (int pi = 0; pi < properties.Length; pi++)
                    {
                        PropertyInfo prop = properties[pi];
                        if (!prop.CanRead) continue;
                        if (!IsTypeSupported(prop.PropertyType, supportedTypes)) continue;

                        string display = $"{prop.Name}  ({FieldTypeHelper.GetDisplayName(prop.PropertyType)})";
                        SelectedMemberInfo info = new SelectedMemberInfo(comp, prop.Name, true, prop.PropertyType);
                        memberEntries.Add(new SearchTreeEntry(new GUIContent(display, indentIcon))
                            { level = 3, userData = info });
                    }

                    if (memberEntries.Count == 0) continue;

                    // Component group
                    searchList.Add(new SearchTreeGroupEntry(new GUIContent(componentType.Name), 2));

                    // Add all member entries
                    memberEntries.Sort((a, b) =>
                        string.CompareOrdinal(((GUIContent)a.content).text,
                                              ((GUIContent)b.content).text));
                    for (int mi = 0; mi < memberEntries.Count; mi++)
                        searchList.Add(memberEntries[mi]);
                }
            }

            if (searchList.Count == 1)
            {
                searchList.Add(new SearchTreeEntry(new GUIContent("(no compatible members found)"))
                    { level = 1, userData = null });
            }

            return searchList;
        }

        public bool OnSelectEntry(SearchTreeEntry searchTreeEntry, SearchWindowContext context)
        {
            if (searchTreeEntry.userData is SelectedMemberInfo info)
            {
                onMemberSelected?.Invoke(info);
                return true;
            }
            return false;
        }

        private static bool IsTypeSupported(Type type, IReadOnlyList<Type> supportedTypes)
        {
            for (int i = 0; i < supportedTypes.Count; i++)
            {
                if (supportedTypes[i] == type)
                    return true;
            }
            return false;
        }
    }
}
