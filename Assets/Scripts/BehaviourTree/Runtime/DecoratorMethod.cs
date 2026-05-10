using System;
using BehaviourTree.Core;

namespace BehaviourTree.Runtime
{
    /// <summary>
    /// Attribute to mark a static method as a decorator behaviour tree method.
    /// The method signature must match <see cref="DecoratorMethod"/>:
    /// <c>NodeState(NodeState childResult, BlackBoard blackBoard, ReadOnlySpan&lt;FieldData&gt; fields)</c>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class BTreeDecoratorMethodAttribute : Attribute
    {
        public MethodID methodID;
        public BTreeDecoratorMethodAttribute(MethodID methodID) => this.methodID = methodID;
    }

    /// <summary>
    /// Delegate for decorator methods.
    /// Receives the child's result, the blackboard (for reading/writing), and the decorator's own field data.
    /// Returns the transformed result.
    public delegate NodeState DecoratorMethod(NodeState childResult, BlackBoard blackBoard, ReadOnlySpan<FieldData> fields);
}