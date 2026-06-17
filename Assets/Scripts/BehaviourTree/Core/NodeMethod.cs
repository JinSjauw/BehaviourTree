using System;
using System.Reflection;
using BehaviourTree;
using UnityEngine;

namespace BehaviourTree.Core
{
    // ═══════════════════════════════════════════════════════════════════
    // FieldBinding
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Describes one serializable field on a NodeMethod subclass.
    /// Handles reading/writing to the blackboard for [SharedVar] fields.
    /// Created by MethodRegistry during type scanning; populated by
    /// NodeMethod.DeserializeFields during tree init.
    /// </summary>
    public sealed class FieldBinding
    {
        public FieldInfo fieldInfo;

        /// <summary>
        /// Assembly-qualified type name of this field.
        /// Resolved from fieldInfo.FieldType when created by MethodRegistry.
        /// </summary>
        public string fieldTypeName;

        /// <summary>-1 = constant (value set directly on field); >=0 = BB slot index</summary>
        public int bbSlotIndex = -1;

        /// <summary>If true, the field value is written back to BB after Execute.</summary>
        public bool isOutput = true;

        /// <summary>Resolved System.Type for this binding (lazy).</summary>
        public Type ResolvedType =>
            resolvedType ?? (resolvedType = ResolveType());
        private Type resolvedType;

        private Type ResolveType()
        {
            if (!string.IsNullOrEmpty(fieldTypeName))
                return Type.GetType(fieldTypeName);
            if (fieldInfo != null)
                return fieldInfo.FieldType;
            return null;
        }

        // ── Generic read/write (uses GetBoxed/SetBoxed — with type coercion) ──

        public void ReadFromBBGeneric(NodeMethod instance, IBlackBoardAccess bb)
        {
            if (bbSlotIndex < 0) return;
            object value = bb.GetBoxed(bbSlotIndex);
            if (value != null)
            {
                Type fieldType = fieldInfo.FieldType;
                Type valueType = value.GetType();
                if (fieldType != valueType && !fieldType.IsAssignableFrom(valueType))
                {
                    try { value = Convert.ChangeType(value, fieldType); }
                    catch
                    {
#if UNITY_EDITOR
                        Debug.LogWarning($"[FieldBinding] Cannot convert BB value '{value}' ({valueType.Name}) to field type '{fieldType.Name}' for field '{fieldInfo.Name}'");
#endif
                        return;
                    }
                }
            }
            fieldInfo.SetValue(instance, value);
        }

        public void WriteToBBGeneric(NodeMethod instance, IBlackBoardAccess bb)
        {
            if (!isOutput || bbSlotIndex < 0) return;
            object value = fieldInfo.GetValue(instance);
            if (value != null)
            {
                Type bbType = ResolvedType;
                if (bbType != null)
                {
                    Type valueType = value.GetType();
                    if (bbType != valueType && !bbType.IsAssignableFrom(valueType))
                    {
                        try { value = Convert.ChangeType(value, bbType); }
                        catch
                        {
#if UNITY_EDITOR
                            Debug.LogWarning($"[FieldBinding] Cannot convert field value '{value}' ({valueType.Name}) to BB type '{bbType.Name}' for field '{fieldInfo.Name}'");
#endif
                            return;
                        }
                    }
                }
            }
            bb.SetBoxed(bbSlotIndex, value);
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // NodeMethod — abstract base
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Base class for all leaf-node methods. Inherit from ActionMethod,
    /// ConditionMethod, or DecoratorMethod to auto-register a new method.
    /// Public fields define the inspector schema. Fields with [SharedVar]
    /// are automatically resolved from the blackboard before Execute() and
    /// written back after.
    /// </summary>
    public abstract class NodeMethod
    {
        internal FieldBinding[] bindings;
        internal object[] boxedConstants;
        private IBlackBoardAccess bbAccess;

        /// <summary>Blackboard accessor. Available during Execute().</summary>
        protected IBlackBoardAccess BB => bbAccess;

        /// <summary>
        /// Stable string identifier for this method. Defaults to the class name.
        /// Override or use [NodeMethod("name")] to customize.
        /// </summary>
        public virtual string MethodName
        {
            get
            {
                Type type = GetType();
                NodeMethodAttribute attr = type.GetCustomAttribute<NodeMethodAttribute>();
                return attr != null ? attr.methodName : type.Name;
            }
        }

        /// <summary>
        /// Called once during tree initialization. Copies baked constants directly
        /// onto instance fields and stores BB slot indices for [SharedVar] fields.
        /// </summary>
        public void DeserializeFields(ReadOnlySpan<FieldData> fields, FieldBinding[] bindings)
        {
            DeserializeFields(fields, bindings, null);
        }

        /// <summary>
        /// Called once during tree initialization. Accepts optional boxedConstants
        /// array for constants larger than 4 bytes (Vector3, Color, custom types).
        /// </summary>
        public void DeserializeFields(ReadOnlySpan<FieldData> fields, FieldBinding[] bindings, object[] boxedConstants)
        {
            // Clone bindings to avoid mutating the shared cached array from MethodRegistry
            if (bindings != null)
            {
                this.bindings = new FieldBinding[bindings.Length];
                for (int i = 0; i < bindings.Length; i++)
                {
                    FieldBinding source = bindings[i];
                    if (source != null)
                    {
                        this.bindings[i] = new FieldBinding
                        {
                            fieldInfo = source.fieldInfo,
                            fieldTypeName = source.fieldTypeName,
                            isOutput = source.isOutput,
                            bbSlotIndex = source.bbSlotIndex
                        };
                    }
                }
            }
            else
            {
                this.bindings = null;
            }

            this.boxedConstants = boxedConstants;

            int fieldCount = Math.Min(this.bindings != null ? this.bindings.Length : 0, fields.Length);
            for (int i = 0; i < fieldCount; i++)
            {
                FieldBinding binding = this.bindings[i];
                if (binding == null) continue;

                ref readonly FieldData fd = ref fields[i];
                if (fd.IsConstant)
                {
                    object constValue = ReadConstant(fd, binding.fieldInfo.FieldType, boxedConstants);
                    binding.fieldInfo.SetValue(this, constValue);
                    binding.bbSlotIndex = -1;
                }
                else if (fd.IsBoxedConstant)
                {
                    Type fieldType = binding.fieldInfo.FieldType;
                    object constValue = fd.GetBoxedConstant<object>(boxedConstants);
                    if (constValue != null && fieldType.IsAssignableFrom(constValue.GetType()))
                        binding.fieldInfo.SetValue(this, constValue);
                    binding.bbSlotIndex = -1;
                }
                else
            {
                binding.bbSlotIndex = fd.value;
#if UNITY_EDITOR
                //Debug.Log($"[DeserializeFields] '{GetType().Name}' field='{binding.fieldInfo.Name}' bbSlotIndex={binding.bbSlotIndex} fd.value={fd.value}");
#endif
            }
            }
        }

        /// <summary>
        /// Called by the framework before each Execute(). Copies BB values into
        /// [SharedVar] instance fields using GetBoxed/SetBoxed (supports any type).
        /// </summary>
        public void ResolveInputsGeneric(IBlackBoardAccess bb)
        {
            bbAccess = bb;
            FieldBinding[] b = bindings;
            if (b == null) return;
            for (int i = 0; i < b.Length; i++)
            {
                if (b[i] != null && b[i].bbSlotIndex >= 0)
                {
                    b[i].ReadFromBBGeneric(this, bb);
#if UNITY_EDITOR
                    object val = b[i].fieldInfo.GetValue(this);
                    string valStr = val != null ? (val is UnityEngine.Object obj && obj != null ? obj.name : val.ToString()) : "null";
                    //Debug.Log($"[ResolveInputs] '{GetType().Name}.{b[i].fieldInfo.Name}' bbSlotIndex={b[i].bbSlotIndex} value='{valStr}'");
#endif
                }
            }
        }

        /// <summary>
        /// Called by the framework after each Execute(). Copies [SharedVar]
        /// instance fields back to the BB using GetBoxed/SetBoxed (supports any type).
        /// </summary>
        public void WriteOutputsGeneric(IBlackBoardAccess bb)
        {
            FieldBinding[] b = bindings;
            if (b == null) return;
            for (int i = 0; i < b.Length; i++)
                b[i]?.WriteToBBGeneric(this, bb);
        }

        /// <summary>
        /// Called by AbortSubtree before resetting this node's state to INACTIVE.
        /// Override to clean up blackboard values or other shared state when a branch
        /// is aborted. Only use the provided bbAccess — instance fields are shared
        /// across agents and not safe to use here.
        /// </summary>
        public virtual void OnAbort(IBlackBoardAccess bbAccess) { }

        private static object ReadConstant(FieldData fd, Type fieldType, object[] boxedConstants)
        {
            if (fd.IsBoxedConstant && boxedConstants != null)
            {
                int index = fd.value;
                if (index >= 0 && index < boxedConstants.Length)
                    return boxedConstants[index];
                return null;
            }

            if (fieldType == typeof(float))
                return fd.GetFloat();
            if (fieldType == typeof(bool))
                return fd.GetBool();
            return fd.GetInt(); // int, enum, and fallback
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // Action / Condition / Decorator
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Leaf node that may return RUNNING.</summary>
    public abstract class ActionMethod : NodeMethod
    {
        public abstract NodeState Execute();
    }

    /// <summary>Leaf node that returns SUCCESS or FAILURE immediately.</summary>
    public abstract class ConditionMethod : NodeMethod
    {
        public abstract NodeState Execute();
    }

    /// <summary>Wraps a child node and transforms its result.</summary>
    public abstract class DecoratorMethod : NodeMethod
    {
        public abstract NodeState Execute(NodeState childResult);
    }
}
