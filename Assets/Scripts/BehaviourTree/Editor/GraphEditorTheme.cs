using System.IO;
using UnityEditor;
using UnityEngine;

namespace BehaviourTree.Editor
{
    /// <summary>
    /// Central colour palette for the behaviour tree graph editor.
    /// Access via <c>GraphEditorTheme.instance</c>. Auto-created at
    /// Assets/BehaviourTree/GraphEditorTheme.asset on domain reload if missing.
    /// </summary>
    public sealed class GraphEditorTheme : ScriptableObject
    {
        private const string AssetPath = "Assets/BehaviourTree/GraphEditorTheme.asset";

        private static GraphEditorTheme cachedInstance;

        public static GraphEditorTheme instance
        {
            get
            {
                if (cachedInstance == null)
                    cachedInstance = AssetDatabase.LoadAssetAtPath<GraphEditorTheme>(AssetPath);
                return cachedInstance;
            }
        }

        [Header("Graph Backgrounds")]
        public Color graphBgAgent     = new(0.16f, 0.16f, 0.16f);
        public Color graphBgCommander = new(0.12f, 0.08f, 0.15f);

        [Header("Panels")]
        public Color panelBg          = new(0.18f, 0.18f, 0.18f);
        public Color panelSeparator   = new(0.40f, 0.40f, 0.40f, 0.60f);
        public Color panelPlaceholder = Color.grey;

        [Header("Node Type Bands")]
        public Color nodeBandRoot      = Color.green;
        public Color nodeBandAction    = Color.red;
        public Color nodeBandCondition = Color.yellow;
        public Color nodeBandDecorator = new(0.82f, 0.41f, 0.12f);
        public Color nodeBandSubtree   = new(0.60f, 0.80f, 0.20f);
        public Color nodeBandUnknown   = Color.gray;

        [Header("Composite Sub-Types")]
        public Color compositeSelector  = Color.blue;
        public Color compositeSequence  = Color.purple;
        public Color compositeParallel  = Color.magenta;
        public Color compositePriority  = Color.cyan;
        public Color compositeCommander = new(0.70f, 0.40f, 0.10f);  // orange-brown
        public Color compositeFallback  = Color.gray;

        [Header("Title Badges")]
        public Color badgeAgent     = new(0.40f, 0.65f, 0.95f);
        public Color badgeCommander = new(0.69f, 0.53f, 0.80f);

        [Header("Abort Type Icons")]
        public Color abortSelf          = new(0.38f, 0.00f, 1.00f);
        public Color abortLowerPriority = new(0.00f, 0.78f, 1.00f);
        public Color abortBoth          = new(1.00f, 0.00f, 0.78f);

        [Header("Grid")]
        public Color gridLine      = new(0.76f, 0.77f, 0.75f, 0.10f);
        public Color gridThickLine = new(0.76f, 0.77f, 0.75f, 0.10f);

        [InitializeOnLoadMethod]
        private static void EnsureAssetExists()
        {
            if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(AssetPath)))
                return;

            string dir = Path.GetDirectoryName(AssetPath);
            if (!AssetDatabase.IsValidFolder(dir))
            {
                string parent = Path.GetDirectoryName(dir);
                AssetDatabase.CreateFolder(parent, Path.GetFileName(dir));
            }

            GraphEditorTheme theme = CreateInstance<GraphEditorTheme>();
            AssetDatabase.CreateAsset(theme, AssetPath);
            AssetDatabase.SaveAssets();
            cachedInstance = null;
        }
    }
}
