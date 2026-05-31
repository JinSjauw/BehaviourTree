using System.Collections.Generic;
using BehaviourTree.Core;

namespace BehaviourTree.Editor
{
    public static class SubtreeCycleValidator
    {
        private static readonly Dictionary<BehaviourTreeAssetBase, HashSet<BehaviourTreeAssetBase>> reachableCache = new();

        public static bool WouldCreateCycle(BehaviourTreeAssetBase candidateSubtree, BehaviourTreeAsset parentTree)
        {
            if (candidateSubtree == null || parentTree == null) return false;
            if (candidateSubtree == parentTree) return true;
            return GetReachableSubtrees(candidateSubtree).Contains(parentTree);
        }

        private static HashSet<BehaviourTreeAssetBase> GetReachableSubtrees(BehaviourTreeAssetBase asset)
        {
            if (reachableCache.TryGetValue(asset, out var cached))
                return cached;

            var result = new HashSet<BehaviourTreeAssetBase>();
            BuildReachableSet(asset, result);
            reachableCache[asset] = result;
            return result;
        }

        private static void BuildReachableSet(BehaviourTreeAssetBase asset, HashSet<BehaviourTreeAssetBase> result)
        {
            if (asset is not BehaviourTreeAsset editorAsset || editorAsset.nodesList == null) return;

            for (int i = 0; i < editorAsset.nodesList.Count; i++)
            {
                if (editorAsset.nodesList[i] is SubtreeNode st && st.subTreeAsset != null)
                {
                    if (result.Add(st.subTreeAsset))
                        BuildReachableSet(st.subTreeAsset, result);
                }
            }
        }

        public static void InvalidateCache()
        {
            reachableCache.Clear();
        }
    }
}
