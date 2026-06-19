using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Runtime
{
    /// <summary>
    /// Shared logic for resolving a RuntimeBehaviourTreeAsset from either
    /// an authoring asset (editor bake) or a pre-baked runtime asset (build).
    /// Used by both TreeRunner and CommanderTreeRunner.
    /// </summary>
    public static class RuntimeAssetHelper
    {
        /// <summary>
        /// Returns a ready-to-use RuntimeBehaviourTreeAsset.
        /// If <paramref name="existing"/> is null, bakes from <paramref name="authoringAsset"/>
        /// in the editor, or logs an error in builds.
        /// If <paramref name="existing"/> is non-null, instantiates a copy.
        /// Returns null when resolution fails; caller should abort initialization.
        /// </summary>
        public static RuntimeBehaviourTreeAsset GetOrBake(
            RuntimeBehaviourTreeAsset existing,
            BehaviourTreeAssetBase authoringAsset,
            string logContext = "TreeRunner")
        {
            if (existing == null)
            {
#if UNITY_EDITOR
                if (authoringAsset != null)
                {
                    RuntimeBehaviourTreeAsset temp = ScriptableObject.CreateInstance<RuntimeBehaviourTreeAsset>();
                    temp.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
                    temp.name = authoringAsset.DisplayName + "_Runtime";
                    temp.sourceTree = authoringAsset;
#if UNITY_EDITOR
                    string assetPath = UnityEditor.AssetDatabase.GetAssetPath(authoringAsset);
                    temp.sourceTreeGuid = UnityEditor.AssetDatabase.AssetPathToGUID(assetPath);
#endif

                    temp.blackboardDefinition = TreeBaker.BakeTree(
                        authoringAsset.Root, authoringAsset,
                        ref temp.runtimeNodeData,
                        ref temp.runtimeFieldData,
                        ref temp.boxedConstants,
                        ref temp.runtimeNodeGuids,
                        out temp.maxTreeDepth);

                    return temp;
                }

                Debug.LogError($"[{logContext}] Authoring asset is null — cannot bake tree.");
                return null;
#else
                Debug.LogError($"[{logContext}] Runtime asset is null.");
                return null;
#endif
            }

            RuntimeBehaviourTreeAsset instance = Object.Instantiate(existing);
            instance.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            return instance;
        }
    }
}
