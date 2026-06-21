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

        /// <summary>
        /// When true, this field is hidden from the inspector. The variable name
        /// is auto-filled from AutoVariableName by convention.
        /// </summary>
        public bool IsHidden = false;

        /// <summary>
        /// The BB variable name to auto-bind when IsHidden is true.
        /// The baker resolves this name to a slot offset during bake.
        /// </summary>
        public string AutoVariableName = null;

        /// <summary>
        /// When true, constant-mode shows an order search dropdown instead of a raw int field.
        /// The selected order name is stored in NodeFieldEntry.stringValue and the baker
        /// resolves it to the current index from OrderRegistry.
        /// </summary>
        public bool IsOrderDropdown = false;

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