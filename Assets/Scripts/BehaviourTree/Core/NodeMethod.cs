using System;
using System.Linq.Expressions;
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

        /// <summary>Compiled delegate for zero-allocation field read (null → fall back to reflection).</summary>
        private Action<NodeMethod, IBlackBoardAccess> readDelegate;

        /// <summary>Compiled delegate for zero-allocation field write (null → fall back to reflection).</summary>
        private Action<NodeMethod, IBlackBoardAccess> writeDelegate;

        /// <summary>True if CompileAccessors ran successfully and both delegates are ready.</summary>
        public bool IsCompiled => readDelegate != null && writeDelegate != null;

        /// <summary>
        /// Attempts to compile typed read/write delegates via Expression trees.
        /// Called once during tree init, after <see cref="bbSlotIndex"/> is assigned.
        /// Falls back silently — existing reflection path handles unsupported platforms.
        /// </summary>
        public void CompileAccessors(Type declaringType)
        {
            if (bbSlotIndex < 0 || fieldInfo == null) return;
            Type fieldType = fieldInfo.FieldType;
            if (fieldType == null) return;

            try
            {
                ParameterExpression instParam = Expression.Parameter(typeof(NodeMethod), "inst");
                ParameterExpression bbParam = Expression.Parameter(typeof(IBlackBoardAccess), "bb");
                UnaryExpression castInst = Expression.Convert(instParam, declaringType);
                MemberExpression fieldExpr = Expression.Field(castInst, fieldInfo);
                ConstantExpression slotConst = Expression.Constant(bbSlotIndex);

                // Read: ((ConcreteType)inst).field = bb.Get<T>(bbSlotIndex)
                MethodInfo getMethod = typeof(IBlackBoardAccess).GetMethod("Get")
                    .MakeGenericMethod(fieldType);
                MethodCallExpression getCall = Expression.Call(bbParam, getMethod, slotConst);
                BinaryExpression readBody = Expression.Assign(fieldExpr, getCall);
                readDelegate = Expression.Lambda<Action<NodeMethod, IBlackBoardAccess>>(
                    readBody, instParam, bbParam).Compile();

                // Write: bb.Set<T>(bbSlotIndex, ((ConcreteType)inst).field)
                // Skip when isOutput is false (toggle variables) — the non-compiled path in
                // WriteToBBGeneric checks isOutput and correctly skips the write.
                if (isOutput)
                {
                    MethodInfo setMethod = typeof(IBlackBoardAccess).GetMethod("Set")
                        .MakeGenericMethod(fieldType);
                    MethodCallExpression setCall = Expression.Call(bbParam, setMethod, slotConst, fieldExpr);
                    writeDelegate = Expression.Lambda<Action<NodeMethod, IBlackBoardAccess>>(
                        setCall, instParam, bbParam).Compile();
                }
            }
            catch
            {
                // IL2CPP AOT or unsupported type — delegates remain null, reflection fallback handles it.
            }
        }

        // ── Generic read/write (uses GetBoxed/SetBoxed — with type coercion) ──

        public void ReadFromBBGeneric(NodeMethod instance, IBlackBoardAccess bb)
        {
            if (readDelegate != null)
            {
                readDelegate(instance, bb);
                return;
            }

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
            if (writeDelegate != null)
            {
                writeDelegate(instance, bb);
                return;
            }

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
    // DynamicParamDescriptor — self-describing parameter layout for
    // dynamic-type nodes that use DeserializeParameters instead of
    // [SharedVar] C# fields. Each descriptor maps to one FieldData entry.
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Kind of a dynamic parameter in the node inspector.</summary>
    public enum DynamicParamKind
    {
        /// <summary>Blackboard variable picker with inline S+F buttons.</summary>
        Variable,
        /// <summary>Toggle between constant (typed field) and variable (dropdown). Shows C/V button.</summary>
        Toggle,
        /// <summary>Read-only constant value. No variable binding.</summary>
        Constant,
        /// <summary>Enum dropdown for operation selection (e.g. Equal, Less, Greater).</summary>
        Operation,
    }

    /// <summary>
    /// Describes one parameter slot for a dynamic-type node method.
    /// Returned by <see cref="NodeMethod.GetDynamicParamDescriptors"/>.
    /// When non-null, the editor renders a generic inspector from these descriptors
    /// instead of requiring per-node hardcoded switch cases.
    /// </summary>
    public struct DynamicParamDescriptor
    {
        /// <summary>UI label shown in the inspector.</summary>
        public string label;

        /// <summary>Controls what UI is rendered for this parameter.</summary>
        public DynamicParamKind kind;

        /// <summary>Index into the fieldEntries array for this parameter.</summary>
        public int index;

        /// <summary>
        /// When non-null, the F (filter) popup only shows these types.
        /// Null means any type is allowed. Ignored for non-Variable / non-Toggle kinds.
        /// </summary>
        public Type[] allowedTypes;

        /// <summary>
        /// For Operation kind: the enum type to render as a dropdown.
        /// Only used when kind == DynamicParamKind.Operation.
        /// </summary>
        public Type operationEnumType;

        /// <summary>
        /// For Operation kind: optional per-type filter. Receives the current variable
        /// type and returns the subset of enum value indices to display in the dropdown.
        /// Return null to show all enum values. Only used when kind == DynamicParamKind.Operation.
        /// </summary>
        public Func<Type, int[]> getAvailableOpIndices;
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
        /// Looks up the raw BB slot index for a [SharedVar] field by its C# field name.
        /// Returns -1 if the field is not found or has no BB binding.
        /// Preferred over positional bindings[N].bbSlotIndex which breaks on reorder.
        /// </summary>
        protected int GetSlotByName(string fieldName)
        {
            if (bindings == null) return -1;
            foreach (FieldBinding binding in bindings)
            {
                if (binding.fieldInfo != null && binding.fieldInfo.Name == fieldName)
                    return binding.bbSlotIndex;
            }
            return -1;
        }

        /// <summary>
        /// Number of parameter slots this method expects. Default 0 means
        /// "determine from [SharedVar] field count". Override to a positive
        /// number for dynamic-type nodes that receive FieldData directly
        /// via <see cref="DeserializeParameters"/> without C# [SharedVar] fields.
        /// When <see cref="GetDynamicParamDescriptors"/> returns non-null, this value
        /// is derived from descriptors.Length automatically.
        /// </summary>
        public virtual int ParameterCount => GetDynamicParamDescriptors()?.Length ?? 0;

        /// <summary>
        /// Returns the parameter layout for dynamic-type nodes. Null means
        /// "use [SharedVar] C# field binding" (legacy path).
        /// Non-null means the editor renders a generic inspector from these descriptors
        /// and the baker knows this node uses <see cref="DeserializeParameters"/>.
        /// Each descriptor maps to one FieldData entry by position (descriptors[i] → entry i).
        /// </summary>
        public virtual DynamicParamDescriptor[] GetDynamicParamDescriptors() => null;

        /// <summary>
        /// Called once during tree initialization for nodes with dynamic parameters
        /// (<see cref="GetDynamicParamDescriptors"/> returns non-null).
        /// Receives FieldData and type names directly — the node stores slot indices / constant values
        /// in its own fields. No FieldBindings are created.
        /// Fields with mode=1 contain slot offsets; mode=0 contain packed constants (use fieldTypeNames
        /// to interpret float bit patterns); mode=2 contain boxed constants.
        /// </summary>
        public virtual void DeserializeParameters(ReadOnlySpan<FieldData> fields, string[] fieldTypeNames, object[] boxedConstants) { }

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

            int bindingCount = this.bindings != null ? this.bindings.Length : 0;
            int fieldIndex = 0;
            for (int i = 0; i < bindingCount; i++)
            {
                if (fieldIndex >= fields.Length) break;

                FieldBinding binding = this.bindings[i];
                if (binding == null) continue;

                ref readonly FieldData fd = ref fields[fieldIndex];
                bool isVariableField = fd.IsVariable;

                if (fd.IsConstant)
                {
                    Type fieldType = binding.fieldInfo.FieldType;
                    if (IsPackedConstantType(fieldType))
                    {
                        object constValue = ReadConstant(fd, fieldType, boxedConstants);
                        binding.fieldInfo.SetValue(this, constValue);
                    }
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
                    binding.CompileAccessors(GetType());
#if UNITY_EDITOR
                    //Debug.Log($"[DeserializeFields] '{GetType().Name}' field='{binding.fieldInfo.Name}' bbSlotIndex={binding.bbSlotIndex} fd.value={fd.value}");
#endif
                }

                fieldIndex++;

                // TreeBaker injects a stride marker after variable fields with stride > 1.
                // Skip it so the next binding reads the correct FieldData entry.
                if (isVariableField && fieldIndex < fields.Length && fields[fieldIndex].IsStrideMarker)
                    fieldIndex++;
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

        /// <summary>
        /// Returns true when the given type can be stored in packed constant form (mode=0).
        /// Only int, float, bool, and enums fit in FieldData's 4-byte value field.
        /// Everything else (Vector3, GameObject, etc.) must use boxed constants (mode=2).
        /// </summary>
        private static bool IsPackedConstantType(Type fieldType)
        {
            if (fieldType == null) return false;
            return fieldType == typeof(int)
                || fieldType == typeof(uint)
                || fieldType == typeof(float)
                || fieldType == typeof(bool)
                || fieldType.IsEnum;
        }

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
