using System;
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

        [Header("Method IDs (Actions, Conditions & Decorators)")]
        [Tooltip("Auto-sized to match the MethodID enum. Fill in each entry manually.")]
        [SerializeField] private NodeTooltipData[] methodIdTooltips;

        private static TooltipRegistry loadedAsset;

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

        public static NodeTooltipData GetTooltip(MethodID methodId)
        {
            var registry = Load();
            return registry != null ? registry.GetTooltipInternal(methodId) : GetDefaultTooltip(methodId.ToString());
        }

        public static NodeTooltipData GetTooltip(BehaviourNode node)
        {
            if (node is ActionNode actionNode)
                return GetTooltip(actionNode.methodID);
            if (node is ConditionNode conditionNode)
                return GetTooltip(conditionNode.methodID);
            if (node is DecoratorNode decoratorNode)
                return GetTooltip(decoratorNode.methodID);

            return GetTooltip(node.NodeType);
        }

        private NodeTooltipData GetTooltipInternal(BehaviourNodeType nodeType)
        {
            return nodeType switch
            {
                BehaviourNodeType.ROOT => rootTooltip,
                BehaviourNodeType.SELECTOR => selectorTooltip,
                BehaviourNodeType.SEQUENCE => sequenceTooltip,
                _ => GetDefaultTooltip(nodeType.ToString())
            };
        }

        private NodeTooltipData GetTooltipInternal(MethodID methodId)
        {
            int index = (int)methodId;
            if (methodIdTooltips != null && index >= 0 && index < methodIdTooltips.Length)
            {
                return methodIdTooltips[index] ?? GetDefaultTooltip(methodId.ToString());
            }
            return GetDefaultTooltip(methodId.ToString());
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
            AutoResizeMethodIdArray();
            AutoDeriveFieldDescriptions();
        }

        private void OnValidate()
        {
            AutoResizeMethodIdArray();
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

        private void AutoResizeMethodIdArray()
        {
            int enumLength = Enum.GetValues(typeof(MethodID)).Length;
            if (methodIdTooltips == null || methodIdTooltips.Length != enumLength)
            {
                NodeTooltipData[] resized = new NodeTooltipData[enumLength];
                if (methodIdTooltips != null)
                {
                    int copyCount = Mathf.Min(methodIdTooltips.Length, enumLength);
                    Array.Copy(methodIdTooltips, resized, copyCount);
                }
                for (int i = 0; i < resized.Length; i++)
                {
                    if (resized[i] == null)
                    {
                        MethodID methodId = (MethodID)i;
                        resized[i] = new NodeTooltipData
                        {
                            nodeName = methodId.ToString(),
                            description = "No description available.",
                            returnValues = ""
                        };
                    }
                }
                methodIdTooltips = resized;
            }
        }

#if UNITY_EDITOR

        private void AutoDeriveFieldDescriptions()
        {
            if (methodIdTooltips == null) return;

            for (int i = 0; i < methodIdTooltips.Length; i++)
            {
                if (methodIdTooltips[i] == null) continue;

                MethodID methodId = (MethodID)i;
                var paramInfos = MethodMetadataCache.GetParamsForMethod(methodId);
                if (paramInfos == null || paramInfos.Count == 0) continue;

                var existingDescriptions = methodIdTooltips[i].fieldDescriptions;
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

                methodIdTooltips[i].fieldDescriptions = newDescriptions;
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

        public void SetNodeTypeTooltip(BehaviourNodeType nodeType, NodeTooltipData data)
        {
            switch (nodeType)
            {
                case BehaviourNodeType.ROOT:
                    rootTooltip = data;
                    break;
                case BehaviourNodeType.SELECTOR:
                    selectorTooltip = data;
                    break;
                case BehaviourNodeType.SEQUENCE:
                    sequenceTooltip = data;
                    break;
            }
        }

        public void SetMethodIdTooltip(int index, NodeTooltipData data)
        {
            if (methodIdTooltips == null)
            {
                methodIdTooltips = new NodeTooltipData[Enum.GetValues(typeof(MethodID)).Length];
            }

            if (index >= 0 && index < methodIdTooltips.Length)
            {
                methodIdTooltips[index] = data;
            }
        }

        public void SetMethodIdTooltip(MethodID methodId, NodeTooltipData data)
        {
            SetMethodIdTooltip((int)methodId, data);
        }
    }
}
