using System;

namespace BehaviourTree.Core
{
    /// <summary>
    /// Optional override for the method name key. If omitted, the class name is used.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class NodeMethodAttribute : Attribute
    {
        public string methodName;
        public NodeMethodAttribute(string methodName) => this.methodName = methodName;
    }
}
