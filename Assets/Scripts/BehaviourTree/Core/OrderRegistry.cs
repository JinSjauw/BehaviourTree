using System.Collections.Generic;
using UnityEngine;

namespace BehaviourTree.Core
{
    /// <summary>
    /// Project-wide singleton defining the available orders agents can receive
    /// and commanders can send. Each order is identified by name; its runtime
    /// value is its index in the list. Stored by name in NodeFieldEntry so that
    /// reordering the registry does not break existing trees.
    ///
    /// Create via Assets > Create > BehaviourTree > Order Registry.
    /// Only one instance should exist per project. The baker uses the singleton
    /// accessor to resolve name → index at bake time.
    /// </summary>
    [CreateAssetMenu(menuName = "BehaviourTree/Order Registry")]
    public class OrderRegistry : ScriptableObject
    {
        /// <summary>
        /// Ordered list of order names. Index 0 is the default / idle state.
        /// </summary>
        public List<string> orderNames = new List<string>();

        // ── Singleton ──────────────────────────────────────

        private static OrderRegistry cachedInstance;

        /// <summary>
        /// Finds the sole OrderRegistry asset in the project. Editor uses
        /// AssetDatabase; runtime uses Resources. Cached after first access.
        /// </summary>
        public static OrderRegistry FindInstance()
        {
            if (cachedInstance != null) return cachedInstance;

#if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:OrderRegistry");
            if (guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                cachedInstance = UnityEditor.AssetDatabase.LoadAssetAtPath<OrderRegistry>(path);
                if (guids.Length > 1)
                    Debug.LogWarning($"Multiple OrderRegistry assets found. Using the first one. at {path}");
            }
#else
            cachedInstance = Resources.Load<OrderRegistry>("OrderRegistry");
            if (cachedInstance == null)
            {
                OrderRegistry[] all = Resources.LoadAll<OrderRegistry>("");
                if (all.Length > 0) cachedInstance = all[0];
            }
#endif
            return cachedInstance;
        }

        /// <summary>
        /// Resolves an order name to its current index. Returns -1 if not found.
        /// </summary>
        public int GetIndex(string name)
        {
            if (orderNames == null || string.IsNullOrEmpty(name)) return -1;
            return orderNames.IndexOf(name);
        }

#if UNITY_EDITOR
        private void OnEnable() => cachedInstance = this;
        private void OnDisable() { if (cachedInstance == this) cachedInstance = null; }
#endif
    }
}
