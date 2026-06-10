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
        public FieldType fieldType;
        /// <summary>-1 = constant (value set directly on field); >=0 = BB slot index</summary>
        public int bbSlotIndex = -1;
        /// <summary>If true, the field value is written back to BB after Execute.</summary>
        public bool isOutput = true;

        public void ReadFromBB(NodeMethod instance, IBlackBoardAccess bb)
        {
            if (bbSlotIndex < 0) return;
            switch (fieldType)
            {
                case FieldType.Int:       fieldInfo.SetValue(instance, bb.GetInt(bbSlotIndex)); break;
                case FieldType.Float:     fieldInfo.SetValue(instance, bb.GetFloat(bbSlotIndex)); break;
                case FieldType.Bool:      fieldInfo.SetValue(instance, bb.GetBool(bbSlotIndex)); break;
                case FieldType.Vector2:   fieldInfo.SetValue(instance, bb.GetVector2(bbSlotIndex)); break;
                case FieldType.Vector3:   fieldInfo.SetValue(instance, bb.GetVector3(bbSlotIndex)); break;
                case FieldType.GameObject: fieldInfo.SetValue(instance, bb.GetGameObject(bbSlotIndex)); break;
                case FieldType.Transform: fieldInfo.SetValue(instance, bb.GetTransform(bbSlotIndex)); break;
            }
        }

        public void WriteToBB(NodeMethod instance, IBlackBoardAccess bb)
        {
            if (!isOutput || bbSlotIndex < 0) return;
            switch (fieldType)
            {
                case FieldType.Int:       bb.SetInt(bbSlotIndex, (int)fieldInfo.GetValue(instance)); break;
                case FieldType.Float:     bb.SetFloat(bbSlotIndex, (float)fieldInfo.GetValue(instance)); break;
                case FieldType.Bool:      bb.SetBool(bbSlotIndex, (bool)fieldInfo.GetValue(instance)); break;
                case FieldType.Vector2:   bb.SetVector2(bbSlotIndex, (Vector2)fieldInfo.GetValue(instance)); break;
                case FieldType.Vector3:   bb.SetVector3(bbSlotIndex, (Vector3)fieldInfo.GetValue(instance)); break;
                case FieldType.GameObject: bb.SetGameObject(bbSlotIndex, (GameObject)fieldInfo.GetValue(instance)); break;
                case FieldType.Transform: bb.SetTransform(bbSlotIndex, (Transform)fieldInfo.GetValue(instance)); break;
            }
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
            this.bindings = bindings;
            for (int i = 0; i < bindings.Length; i++)
            {
                FieldBinding binding = bindings[i];
                if (binding == null) continue;

                ref readonly FieldData fd = ref fields[i];
                if (fd.IsConstant)
                {
                    object constValue = ReadConstant(fd, binding.fieldType);
                    binding.fieldInfo.SetValue(this, constValue);
                    binding.bbSlotIndex = -1;
                }
                else
                {
                    binding.bbSlotIndex = fd.value;
                }
            }
        }

        /// <summary>
        /// Called by the framework before each Execute(). Copies BB values into
        /// [SharedVar] instance fields.
        /// </summary>
        public void ResolveInputs(IBlackBoardAccess bb)
        {
            bbAccess = bb;
            FieldBinding[] b = bindings;
            if (b == null) return;
            for (int i = 0; i < b.Length; i++)
            {
                if (b[i] != null && b[i].bbSlotIndex >= 0)
                    b[i].ReadFromBB(this, bb);
            }
        }

        /// <summary>
        /// Called by the framework after each Execute(). Copies [SharedVar]
        /// instance fields back to the BB.
        /// </summary>
        public void WriteOutputs(IBlackBoardAccess bb)
        {
            FieldBinding[] b = bindings;
            if (b == null) return;
            for (int i = 0; i < b.Length; i++)
                b[i]?.WriteToBB(this, bb);
        }

        private static object ReadConstant(FieldData fd, FieldType type)
        {
            return type switch
            {
                FieldType.Int   => fd.GetInt(),
                FieldType.Float => fd.GetFloat(),
                FieldType.Bool  => fd.GetBool(),
                _               => fd.GetInt()
            };
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
