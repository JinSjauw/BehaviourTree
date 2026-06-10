using System;
using System.Collections.Generic;
using UnityEngine;
using BehaviourTree.Core;
using UnityEditor;

namespace BehaviourTree.Editor
{
    [Serializable]
    public class NodeFieldDescription
    {
        [HideInInspector] public string fieldName;
        [TextArea(1, 3)]
        public string description;
    }

    [Serializable]
    public class NodeTooltipData
    {
        public string nodeName;
        [TextArea(2, 5)]
        public string description;
        [TextArea(2, 4)]
        public string returnValues;
        public NodeFieldDescription[] fieldDescriptions;
    }

    public class TooltipRegistry : ScriptableObject
    {
        private const string AssetPath = "Assets/Scripts/BehaviourTree/TooltipRegistry.asset";

        [Header("Composite Node Types")]
        [SerializeField] private NodeTooltipData rootTooltip;
        [SerializeField] private NodeTooltipData selectorTooltip;
        [SerializeField] private NodeTooltipData sequenceTooltip;
        [SerializeField] private NodeTooltipData parallelTooltip;
        [SerializeField] private NodeTooltipData priorityTooltip;

        [Header("Method ID Tooltips (keyed by method name)")]
        [SerializeField] private List<MethodTooltipEntry> methodTooltips = new();

        [Serializable]
        public class MethodTooltipEntry
        {
            public string methodName;
            public NodeTooltipData data;
        }

        private static TooltipRegistry loadedAsset;
        private Dictionary<string, NodeTooltipData> tooltipLookup;

        public static TooltipRegistry Load()
        {
            if (loadedAsset == null)
            {
                loadedAsset = AssetDatabase.LoadAssetAtPath<TooltipRegistry>(AssetPath);
                if (loadedAsset == null)
                {
                    EnsureAssetExists();
                    loadedAsset = AssetDatabase.LoadAssetAtPath<TooltipRegistry>(AssetPath);
                }
            }
            return loadedAsset;
        }

        public static void InvalidateCache()
        {
            loadedAsset = null;
        }

        public static NodeTooltipData GetTooltip(BehaviourNodeType nodeType)
        {
            var registry = Load();
            return registry != null ? registry.GetTooltipInternal(nodeType) : GetDefaultTooltip(nodeType.ToString());
        }

        public static NodeTooltipData GetTooltip(string methodName)
        {
            var registry = Load();
            return registry != null ? registry.GetTooltipInternal(methodName) : GetDefaultTooltip(methodName);
        }

        public static NodeTooltipData GetTooltip(BehaviourNode node)
        {
            if (node is LeafNode actionNode)
                return GetTooltip(actionNode.methodName);
            if (node is DecoratorNode decoratorNode)
                return GetTooltip(decoratorNode.methodName);

            return GetTooltip(node.NodeType);
        }

        private NodeTooltipData GetTooltipInternal(BehaviourNodeType nodeType)
        {
            return nodeType switch
            {
                BehaviourNodeType.ROOT => rootTooltip,
                BehaviourNodeType.SELECTOR => selectorTooltip,
                BehaviourNodeType.SEQUENCE => sequenceTooltip,
                BehaviourNodeType.PARALLEL => parallelTooltip,
                BehaviourNodeType.PRIORITY => priorityTooltip,
                _ => GetDefaultTooltip(nodeType.ToString())
            };
        }

        private NodeTooltipData GetTooltipInternal(string methodName)
        {
            EnsureLookup();
            if (tooltipLookup != null && tooltipLookup.TryGetValue(methodName, out var data))
                return data;

            // Auto-create entry for new methods
            var entry = new MethodTooltipEntry
            {
                methodName = methodName,
                data = new NodeTooltipData
                {
                    nodeName = methodName,
                    description = "No description available.",
                    returnValues = ""
                }
            };
            methodTooltips.Add(entry);
            if (tooltipLookup != null)
                tooltipLookup[methodName] = entry.data;
            return entry.data;
        }

        private void EnsureLookup()
        {
            if (tooltipLookup == null)
            {
                tooltipLookup = new Dictionary<string, NodeTooltipData>();
                for (int i = 0; i < methodTooltips.Count; i++)
                {
                    if (methodTooltips[i] != null && !string.IsNullOrEmpty(methodTooltips[i].methodName))
                        tooltipLookup[methodTooltips[i].methodName] = methodTooltips[i].data;
                }
            }
        }

        private static NodeTooltipData GetDefaultTooltip(string name)
        {
            return new NodeTooltipData
            {
                nodeName = name,
                description = "No description available.",
                returnValues = ""
            };
        }

        private void OnEnable()
        {
            AutoDeriveFieldDescriptions();
        }

        private void OnValidate()
        {
            AutoDeriveFieldDescriptions();
            InvalidateCache();
        }

        private static void EnsureAssetExists()
        {
            string directory = System.IO.Path.GetDirectoryName(AssetPath);
            if (!AssetDatabase.IsValidFolder(directory))
            {
                string parent = System.IO.Path.GetDirectoryName(directory);
                string folder = System.IO.Path.GetFileName(directory);
                AssetDatabase.CreateFolder(parent, folder);
            }

            if (AssetDatabase.LoadAssetAtPath<TooltipRegistry>(AssetPath) == null)
            {
                var registry = CreateInstance<TooltipRegistry>();
                AssetDatabase.CreateAsset(registry, AssetPath);
                AssetDatabase.SaveAssets();
            }
        }

#if UNITY_EDITOR

        private void AutoDeriveFieldDescriptions()
        {
            if (methodTooltips == null) return;

            for (int i = 0; i < methodTooltips.Count; i++)
            {
                var entry = methodTooltips[i];
                if (entry == null || entry.data == null || string.IsNullOrEmpty(entry.methodName)) continue;

                var paramInfos = MethodMetadataCache.GetParamsForMethod(entry.methodName);
                if (paramInfos == null || paramInfos.Count == 0) continue;

                var existingDescriptions = entry.data.fieldDescriptions;
                var newDescriptions = new NodeFieldDescription[paramInfos.Count];

                for (int f = 0; f < paramInfos.Count; f++)
                {
                    string fieldName = paramInfos[f].fieldName;
                    string existingDesc = FindExistingDescription(existingDescriptions, fieldName);

                    newDescriptions[f] = new NodeFieldDescription
                    {
                        fieldName = fieldName,
                        description = existingDesc ?? ""
                    };
                }

                entry.data.fieldDescriptions = newDescriptions;
            }
        }

        private static string FindExistingDescription(NodeFieldDescription[] existing, string fieldName)
        {
            if (existing == null) return null;
            for (int i = 0; i < existing.Length; i++)
            {
                if (existing[i] != null && existing[i].fieldName == fieldName)
                    return existing[i].description;
            }
            return null;
        }
#endif
    }
}
