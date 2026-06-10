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
