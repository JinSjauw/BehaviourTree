using System.Collections.Generic;
using UnityEngine;

namespace BehaviourTree.Core
{
    public class CompositeNode : BehaviourNode
    {
        [HideInInspector] [SerializeField] private BehaviourNodeType compositeType = BehaviourNodeType.COMPOSITE;

        public override BehaviourNodeType NodeType => compositeType;

        /// <summary>Class-based method name (e.g. "SEQUENCE", "SELECTOR"). Set at creation time.</summary>
        public string methodName;

        /// <summary>Dynamic list of field entries — generated from method metadata.</summary>
        public List<NodeFieldEntry> fieldEntries = new List<NodeFieldEntry>();

        /// <summary>Conditional abort type for this composite node.</summary>
        [HideInInspector] public AbortType abortType = AbortType.None;

        public void SetCompositeType(BehaviourNodeType type)
        {
            if (type != BehaviourNodeType.COMPOSITE)
            {
                Debug.LogError($"Invalid composite type '{type}' assigned to CompositeNode. Ignoring.");
                return;
            }
            compositeType = type;
        }
    }
}
