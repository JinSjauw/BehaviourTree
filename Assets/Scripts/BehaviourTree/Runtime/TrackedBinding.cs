using System;
using System.Collections.Generic;
using System.Reflection;
using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Runtime
{
    /// <summary>
    /// Serializable binding that maps a component field/property to a blackboard variable.
    /// Persisted on the TreeRunner component. At runtime, the TreeRunner resolves the
    /// member info and variable index once, then pushes the value each frame.
    /// </summary>
    [Serializable]
    public class TrackedBinding
    {
        /// <summary>The component whose field/property is tracked.</summary>
        public Component targetComponent;

        /// <summary>Name of the field or property on the component.</summary>
        public string memberName;

        /// <summary>Name of the target blackboard variable.</summary>
        public string blackboardVariableName;

        /// <summary>True if this binding targets a property; false for a field.</summary>
        public bool isProperty;

        /// <summary>Assembly-qualified type name of the member. Used to filter compatible blackboard variables.</summary>
        public string memberTypeName;

        // ── Transient (resolved once at runtime) ───────────────────

        [NonSerialized] public FieldInfo cachedFieldInfo;
        [NonSerialized] public PropertyInfo cachedPropertyInfo;
        [NonSerialized] public int variableIndex = -1;
    }

    /// <summary>
    /// Groups tracked bindings by their target behaviour tree asset.
    /// Only the group matching the currently assigned tree is activated at runtime.
    /// Matching uses the asset GUID so it works in builds where the SO reference is stripped.
    /// </summary>
    [Serializable]
    public class TrackedBindingGroup
    {
        /// <summary>The tree asset these bindings are scoped to (editor-only reference, stripped in builds).</summary>
#if UNITY_EDITOR
        public BehaviourTreeAssetBase targetTree;
#endif

        /// <summary>GUID of the source tree asset. Used for matching in builds.</summary>
        public string targetTreeGuid;

        /// <summary>Bindings that should push data when targetTree is the active tree.</summary>
        public List<TrackedBinding> bindings = new();
    }
}
