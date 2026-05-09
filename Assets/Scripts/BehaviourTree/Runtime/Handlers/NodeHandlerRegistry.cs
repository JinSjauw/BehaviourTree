using System.Collections.Generic;
using BehaviourTree.Core;

namespace BehaviourTree.Runtime
{
    /// <summary>
    /// Registry mapping node types to their handler implementations.
    /// </summary>
    public static class NodeHandlerRegistry
    {
        private static readonly Dictionary<BehaviourNodeType, INodeHandler> handlers = new Dictionary<BehaviourNodeType, INodeHandler>();

        //private static bool _initialized;

        public static void RegisterDefaults()
        {
            Register(BehaviourNodeType.ACTION, new ActionHandler());
            Register(BehaviourNodeType.CONDITION, new ConditionHandler());
            Register(BehaviourNodeType.SEQUENCE, new SequenceHandler());
            Register(BehaviourNodeType.SELECTOR, new SelectorHandler());
            //_initialized = true;
        }

        /// <summary>
        /// Register a custom handler for a node type, overriding existing if present.
        /// </summary>
        public static void Register(BehaviourNodeType type, INodeHandler handler)
        {
            handlers[type] = handler;
        }

        public static INodeHandler GetHandler(BehaviourNodeType type)
        {
            handlers.TryGetValue(type, out var handler);
            return handler;
        }
    }
}