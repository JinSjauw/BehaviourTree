using System;
using UnityEditor;
using UnityEngine;
using Unity.GraphToolkit.Editor;

namespace BehaviourTree.Editor
{
    [Graph(AssetExtension)]
    [Serializable]
    public class BTGraph : Graph
    {
        public const string AssetExtension = "BTGraph";

        [MenuItem("Assets/Create/BehaviourTree/BTGraph", false)]
        static void CreateAssetFile()
        {
            GraphDatabase.PromptInProjectBrowserToCreateNewAsset<BTGraph>();
        }
    }
}
