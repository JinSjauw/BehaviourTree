using System;

namespace BehaviourTree.Core
{
    /// <summary>
    /// Marks a field of a *_NodeFields struct as a blackboard variable.
    /// Fields without this attribute are treated as constants.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public class SharedVarAttribute : Attribute
    {
        public bool IsToggleVariable = false;

        /// <summary>
        /// When true, constant-mode shows a role dropdown pulled from the tree's
        /// SquadDefinition.availableRoles instead of a raw int field.
        /// </summary>
        public bool IsRoleDropdown = false;

        public SharedVarAttribute(bool isToggleVariable = false) => IsToggleVariable = isToggleVariable;
    }

    /// <summary>
    /// Marks a field of a *_NodeFields struct as a blackboard array variable (stride > 1).
    /// The editor filter will only show strided variables for this field.
    /// The baker packs both base slot and stride into consecutive FieldData entries.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public class SharedArrayAttribute : Attribute
    {
    }
}