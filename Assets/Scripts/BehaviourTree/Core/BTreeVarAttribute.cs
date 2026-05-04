using System;

namespace BehaviourTree.Core
{
    /// <summary>
    /// Marks a field of a *_Params struct as a blackboard variable.
    /// Fields without this attribute are treated as constants.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public class BTreeVarAttribute : Attribute
    {
    }
}