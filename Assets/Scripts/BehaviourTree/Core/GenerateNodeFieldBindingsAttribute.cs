using System;

namespace BehaviourTree.Core
{
    [AttributeUsage(AttributeTargets.Struct, AllowMultiple = false)]
    public sealed class GenerateNodeFieldBindingsAttribute : Attribute
    {
    }
}
