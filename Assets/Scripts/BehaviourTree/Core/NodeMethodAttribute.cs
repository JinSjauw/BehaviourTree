using System;

namespace BehaviourTree.Core
{
    /// <summary>
    /// Restricts which tree types a node method may appear in.
    /// </summary>
    public enum AllowedTreeType
    {
        Any = 0,
        Agent = 1,
        Commander = 2
    }

    /// <summary>
    /// Optional override for the method name key. If omitted, the class name is used.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class NodeMethodAttribute : Attribute
    {
        public string methodName;
        public AllowedTreeType allowedTreeType = AllowedTreeType.Any;
        public NodeMethodAttribute(string methodName) => this.methodName = methodName;
    }
}
