using System;
using BehaviourTree.Core;
using BehaviourTree.Runtime;
using UnityEngine;

namespace BehaviourTree
{
    public partial class StandardMethods
    {
        private static bool RequireVariable(ReadOnlySpan<FieldData> fields, int index)
        {
            return fields.Length > index && fields[index].IsVariable;
        }

        private static bool RequireConstant(ReadOnlySpan<FieldData> fields, int index)
        {
            return fields.Length > index && fields[index].IsConstant;
        }

        [BTreeMethod(MethodID.BB_CompareInt)]
        public static NodeState BB_CompareInt(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            if (!RequireVariable(fields, 0) || !RequireVariable(fields, 1) || !RequireConstant(fields, 2)) return NodeState.FAILURE;

            int a = blackBoard.Get<int>(fields[0].value);
            int b = blackBoard.Get<int>(fields[1].value);
            NumericCompareOp op = (NumericCompareOp)fields[2].GetInt();

            bool result = op switch
            {
                NumericCompareOp.Equal => a == b,
                NumericCompareOp.NotEqual => a != b,
                NumericCompareOp.Less => a < b,
                NumericCompareOp.LessOrEqual => a <= b,
                NumericCompareOp.Greater => a > b,
                NumericCompareOp.GreaterOrEqual => a >= b,
                NumericCompareOp.ApproxEqual => a == b,
                _ => false
            };

            return result ? NodeState.SUCCESS : NodeState.FAILURE;
        }

        [BTreeMethod(MethodID.BB_CompareFloat)]
        public static NodeState BB_CompareFloat(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            if (!RequireVariable(fields, 0) || !RequireVariable(fields, 1) || !RequireConstant(fields, 2) || !RequireConstant(fields, 3)) return NodeState.FAILURE;

            float a = blackBoard.Get<float>(fields[0].value);
            float b = blackBoard.Get<float>(fields[1].value);
            NumericCompareOp op = (NumericCompareOp)fields[2].GetInt();
            float epsilon = fields[3].GetFloat();

            bool result = op switch
            {
                NumericCompareOp.Equal => a == b,
                NumericCompareOp.NotEqual => a != b,
                NumericCompareOp.Less => a < b,
                NumericCompareOp.LessOrEqual => a <= b,
                NumericCompareOp.Greater => a > b,
                NumericCompareOp.GreaterOrEqual => a >= b,
                NumericCompareOp.ApproxEqual => Mathf.Abs(a - b) <= epsilon,
                _ => false
            };

            return result ? NodeState.SUCCESS : NodeState.FAILURE;
        }

        [BTreeMethod(MethodID.BB_CompareBool)]
        public static NodeState BB_CompareBool(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            if (!RequireVariable(fields, 0) || !RequireVariable(fields, 1) || !RequireConstant(fields, 2)) return NodeState.FAILURE;

            bool a = blackBoard.Get<bool>(fields[0].value);
            bool b = blackBoard.Get<bool>(fields[1].value);
            BoolCompareOp op = (BoolCompareOp)fields[2].GetInt();

            bool result = op switch
            {
                BoolCompareOp.Equal => a == b,
                BoolCompareOp.NotEqual => a != b,
                _ => false
            };

            return result ? NodeState.SUCCESS : NodeState.FAILURE;
        }

        [BTreeMethod(MethodID.BB_CompareVector2)]
        public static NodeState BB_CompareVector2(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            if (!RequireVariable(fields, 0) || !RequireVariable(fields, 1) || !RequireConstant(fields, 2) || !RequireConstant(fields, 3)) return NodeState.FAILURE;

            Vector2 a = blackBoard.Get<Vector2>(fields[0].value);
            Vector2 b = blackBoard.Get<Vector2>(fields[1].value);
            VectorCompareOp op = (VectorCompareOp)fields[2].GetInt();
            float epsilon = fields[3].GetFloat();

            float aMag = a.magnitude;
            float bMag = b.magnitude;

            bool result = op switch
            {
                VectorCompareOp.Equal => a == b,
                VectorCompareOp.NotEqual => a != b,
                VectorCompareOp.MagnitudeLess => aMag < bMag,
                VectorCompareOp.MagnitudeLessOrEqual => aMag <= bMag,
                VectorCompareOp.MagnitudeGreater => aMag > bMag,
                VectorCompareOp.MagnitudeGreaterOrEqual => aMag >= bMag,
                VectorCompareOp.MagnitudeApproxEqual => Mathf.Abs(aMag - bMag) <= epsilon,
                _ => false
            };

            return result ? NodeState.SUCCESS : NodeState.FAILURE;
        }

        [BTreeMethod(MethodID.BB_CompareVector3)]
        public static NodeState BB_CompareVector3(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            if (!RequireVariable(fields, 0) || !RequireVariable(fields, 1) || !RequireConstant(fields, 2) || !RequireConstant(fields, 3)) return NodeState.FAILURE;

            Vector3 a = blackBoard.Get<Vector3>(fields[0].value);
            Vector3 b = blackBoard.Get<Vector3>(fields[1].value);
            VectorCompareOp op = (VectorCompareOp)fields[2].GetInt();
            float epsilon = fields[3].GetFloat();

            float aMag = a.magnitude;
            float bMag = b.magnitude;

            bool result = op switch
            {
                VectorCompareOp.Equal => a == b,
                VectorCompareOp.NotEqual => a != b,
                VectorCompareOp.MagnitudeLess => aMag < bMag,
                VectorCompareOp.MagnitudeLessOrEqual => aMag <= bMag,
                VectorCompareOp.MagnitudeGreater => aMag > bMag,
                VectorCompareOp.MagnitudeGreaterOrEqual => aMag >= bMag,
                VectorCompareOp.MagnitudeApproxEqual => Mathf.Abs(aMag - bMag) <= epsilon,
                _ => false
            };

            return result ? NodeState.SUCCESS : NodeState.FAILURE;
        }

        [BTreeMethod(MethodID.BB_CompareGameObject)]
        public static NodeState BB_CompareGameObject(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            if (!RequireVariable(fields, 0) || !RequireVariable(fields, 1) || !RequireConstant(fields, 2)) return NodeState.FAILURE;

            GameObject a = blackBoard.Get<GameObject>(fields[0].value);
            GameObject b = blackBoard.Get<GameObject>(fields[1].value);
            ObjectCompareOp op = (ObjectCompareOp)fields[2].GetInt();

            bool result = op switch
            {
                ObjectCompareOp.Equal => a == b,
                ObjectCompareOp.NotEqual => a != b,
                ObjectCompareOp.IsNull => a == null,
                ObjectCompareOp.IsNotNull => a != null,
                _ => false
            };

            return result ? NodeState.SUCCESS : NodeState.FAILURE;
        }

        [BTreeMethod(MethodID.BB_CompareTransform)]
        public static NodeState BB_CompareTransform(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            if (!RequireVariable(fields, 0) || !RequireVariable(fields, 1) || !RequireConstant(fields, 2)) return NodeState.FAILURE;

            Transform a = blackBoard.Get<Transform>(fields[0].value);
            Transform b = blackBoard.Get<Transform>(fields[1].value);
            ObjectCompareOp op = (ObjectCompareOp)fields[2].GetInt();

            bool result = op switch
            {
                ObjectCompareOp.Equal => a == b,
                ObjectCompareOp.NotEqual => a != b,
                ObjectCompareOp.IsNull => a == null,
                ObjectCompareOp.IsNotNull => a != null,
                _ => false
            };

            return result ? NodeState.SUCCESS : NodeState.FAILURE;
        }

        [BTreeMethod(MethodID.BB_SetInt)]
        public static NodeState BB_SetInt(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            if (!RequireVariable(fields, 0) || !RequireVariable(fields, 1)) return NodeState.FAILURE;
            blackBoard.Set(fields[0].value, blackBoard.Get<int>(fields[1].value));
            return NodeState.SUCCESS;
        }

        [BTreeMethod(MethodID.BB_SetFloat)]
        public static NodeState BB_SetFloat(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            if (!RequireVariable(fields, 0) || !RequireVariable(fields, 1)) return NodeState.FAILURE;
            blackBoard.Set(fields[0].value, blackBoard.Get<float>(fields[1].value));
            return NodeState.SUCCESS;
        }

        [BTreeMethod(MethodID.BB_SetBool)]
        public static NodeState BB_SetBool(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            if (!RequireVariable(fields, 0) || !RequireVariable(fields, 1)) return NodeState.FAILURE;
            blackBoard.Set(fields[0].value, blackBoard.Get<bool>(fields[1].value));
            return NodeState.SUCCESS;
        }

        [BTreeMethod(MethodID.BB_SetVector2)]
        public static NodeState BB_SetVector2(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            if (!RequireVariable(fields, 0) || !RequireVariable(fields, 1)) return NodeState.FAILURE;
            blackBoard.Set(fields[0].value, blackBoard.Get<Vector2>(fields[1].value));
            return NodeState.SUCCESS;
        }

        [BTreeMethod(MethodID.BB_SetVector3)]
        public static NodeState BB_SetVector3(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            if (!RequireVariable(fields, 0) || !RequireVariable(fields, 1)) return NodeState.FAILURE;
            blackBoard.Set(fields[0].value, blackBoard.Get<Vector3>(fields[1].value));
            return NodeState.SUCCESS;
        }

        [BTreeMethod(MethodID.BB_SetGameObject)]
        public static NodeState BB_SetGameObject(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            if (!RequireVariable(fields, 0) || !RequireVariable(fields, 1)) return NodeState.FAILURE;
            blackBoard.Set(fields[0].value, blackBoard.Get<GameObject>(fields[1].value));
            return NodeState.SUCCESS;
        }

        [BTreeMethod(MethodID.BB_SetTransform)]
        public static NodeState BB_SetTransform(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            if (!RequireVariable(fields, 0) || !RequireVariable(fields, 1)) return NodeState.FAILURE;
            blackBoard.Set(fields[0].value, blackBoard.Get<Transform>(fields[1].value));
            return NodeState.SUCCESS;
        }

        [BTreeMethod(MethodID.BB_ClearInt)]
        public static NodeState BB_ClearInt(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            if (!RequireVariable(fields, 0)) return NodeState.FAILURE;
            blackBoard.Set(fields[0].value, default(int));
            return NodeState.SUCCESS;
        }

        [BTreeMethod(MethodID.BB_ClearFloat)]
        public static NodeState BB_ClearFloat(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            if (!RequireVariable(fields, 0)) return NodeState.FAILURE;
            blackBoard.Set(fields[0].value, default(float));
            return NodeState.SUCCESS;
        }

        [BTreeMethod(MethodID.BB_ClearBool)]
        public static NodeState BB_ClearBool(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            if (!RequireVariable(fields, 0)) return NodeState.FAILURE;
            blackBoard.Set(fields[0].value, default(bool));
            return NodeState.SUCCESS;
        }

        [BTreeMethod(MethodID.BB_ClearVector2)]
        public static NodeState BB_ClearVector2(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            if (!RequireVariable(fields, 0)) return NodeState.FAILURE;
            blackBoard.Set(fields[0].value, default(Vector2));
            return NodeState.SUCCESS;
        }

        [BTreeMethod(MethodID.BB_ClearVector3)]
        public static NodeState BB_ClearVector3(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            if (!RequireVariable(fields, 0)) return NodeState.FAILURE;
            blackBoard.Set(fields[0].value, default(Vector3));
            return NodeState.SUCCESS;
        }

        [BTreeMethod(MethodID.BB_ClearGameObject)]
        public static NodeState BB_ClearGameObject(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            if (!RequireVariable(fields, 0)) return NodeState.FAILURE;
            blackBoard.Set<GameObject>(fields[0].value, null);
            return NodeState.SUCCESS;
        }

        [BTreeMethod(MethodID.BB_ClearTransform)]
        public static NodeState BB_ClearTransform(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            if (!RequireVariable(fields, 0)) return NodeState.FAILURE;
            blackBoard.Set<Transform>(fields[0].value, null);
            return NodeState.SUCCESS;
        }

        [BTreeMethod(MethodID.BB_ToggleBool)]
        public static NodeState BB_ToggleBool(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            if (!RequireVariable(fields, 0)) return NodeState.FAILURE;
            int index = fields[0].value;
            blackBoard.Set(index, !blackBoard.Get<bool>(index));
            return NodeState.SUCCESS;
        }

        [BTreeMethod(MethodID.WaitSeconds)]
        public static NodeState WaitSeconds(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            if (!RequireConstant(fields, 0) || !RequireVariable(fields, 1)) return NodeState.FAILURE;

            float duration = fields[0].GetFloat();
            int elapsedIndex = fields[1].value;
            float elapsed = blackBoard.Get<float>(elapsedIndex) + Time.deltaTime;

            if (elapsed >= duration)
            {
                blackBoard.Set(elapsedIndex, 0f);
                return NodeState.SUCCESS;
            }

            blackBoard.Set(elapsedIndex, elapsed);
            return NodeState.RUNNING;
        }

        [BTreeMethod(MethodID.Cooldown)]
        public static NodeState Cooldown(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            if (!RequireConstant(fields, 0) || !RequireVariable(fields, 1)) return NodeState.FAILURE;

            float duration = fields[0].GetFloat();
            int remainingIndex = fields[1].value;
            float remaining = blackBoard.Get<float>(remainingIndex);

            if (remaining > 0f)
            {
                remaining = Mathf.Max(0f, remaining - Time.deltaTime);
                blackBoard.Set(remainingIndex, remaining);
                return NodeState.FAILURE;
            }

            blackBoard.Set(remainingIndex, duration);
            return NodeState.SUCCESS;
        }

        [BTreeMethod(MethodID.BB_HasChangedInt)]
        public static NodeState BB_HasChangedInt(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            if (!RequireVariable(fields, 0) || !RequireVariable(fields, 1)) return NodeState.FAILURE;
            int current = blackBoard.Get<int>(fields[0].value);
            int previousIndex = fields[1].value;
            int previous = blackBoard.Get<int>(previousIndex);
            blackBoard.Set(previousIndex, current);
            return current != previous ? NodeState.SUCCESS : NodeState.FAILURE;
        }

        [BTreeMethod(MethodID.BB_HasChangedFloat)]
        public static NodeState BB_HasChangedFloat(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            if (!RequireVariable(fields, 0) || !RequireVariable(fields, 1)) return NodeState.FAILURE;
            float current = blackBoard.Get<float>(fields[0].value);
            int previousIndex = fields[1].value;
            float previous = blackBoard.Get<float>(previousIndex);
            blackBoard.Set(previousIndex, current);
            return current != previous ? NodeState.SUCCESS : NodeState.FAILURE;
        }

        [BTreeMethod(MethodID.BB_HasChangedBool)]
        public static NodeState BB_HasChangedBool(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            if (!RequireVariable(fields, 0) || !RequireVariable(fields, 1)) return NodeState.FAILURE;
            bool current = blackBoard.Get<bool>(fields[0].value);
            int previousIndex = fields[1].value;
            bool previous = blackBoard.Get<bool>(previousIndex);
            blackBoard.Set(previousIndex, current);
            return current != previous ? NodeState.SUCCESS : NodeState.FAILURE;
        }

        [BTreeMethod(MethodID.BB_HasChangedVector2)]
        public static NodeState BB_HasChangedVector2(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            if (!RequireVariable(fields, 0) || !RequireVariable(fields, 1)) return NodeState.FAILURE;
            Vector2 current = blackBoard.Get<Vector2>(fields[0].value);
            int previousIndex = fields[1].value;
            Vector2 previous = blackBoard.Get<Vector2>(previousIndex);
            blackBoard.Set(previousIndex, current);
            return current != previous ? NodeState.SUCCESS : NodeState.FAILURE;
        }

        [BTreeMethod(MethodID.BB_HasChangedVector3)]
        public static NodeState BB_HasChangedVector3(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            if (!RequireVariable(fields, 0) || !RequireVariable(fields, 1)) return NodeState.FAILURE;
            Vector3 current = blackBoard.Get<Vector3>(fields[0].value);
            int previousIndex = fields[1].value;
            Vector3 previous = blackBoard.Get<Vector3>(previousIndex);
            blackBoard.Set(previousIndex, current);
            return current != previous ? NodeState.SUCCESS : NodeState.FAILURE;
        }

        [BTreeMethod(MethodID.BB_HasChangedGameObject)]
        public static NodeState BB_HasChangedGameObject(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            if (!RequireVariable(fields, 0) || !RequireVariable(fields, 1)) return NodeState.FAILURE;
            GameObject current = blackBoard.Get<GameObject>(fields[0].value);
            int previousIndex = fields[1].value;
            GameObject previous = blackBoard.Get<GameObject>(previousIndex);
            blackBoard.Set(previousIndex, current);
            return current != previous ? NodeState.SUCCESS : NodeState.FAILURE;
        }

        [BTreeMethod(MethodID.BB_HasChangedTransform)]
        public static NodeState BB_HasChangedTransform(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            if (!RequireVariable(fields, 0) || !RequireVariable(fields, 1)) return NodeState.FAILURE;
            Transform current = blackBoard.Get<Transform>(fields[0].value);
            int previousIndex = fields[1].value;
            Transform previous = blackBoard.Get<Transform>(previousIndex);
            blackBoard.Set(previousIndex, current);
            return current != previous ? NodeState.SUCCESS : NodeState.FAILURE;
        }

        [BTreeMethod(MethodID.BB_EdgeRisingBool)]
        public static NodeState BB_EdgeRisingBool(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            if (!RequireVariable(fields, 0) || !RequireVariable(fields, 1)) return NodeState.FAILURE;
            bool current = blackBoard.Get<bool>(fields[0].value);
            int previousIndex = fields[1].value;
            bool previous = blackBoard.Get<bool>(previousIndex);
            blackBoard.Set(previousIndex, current);
            return current && !previous ? NodeState.SUCCESS : NodeState.FAILURE;
        }

        [BTreeMethod(MethodID.BB_EdgeFallingBool)]
        public static NodeState BB_EdgeFallingBool(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            if (!RequireVariable(fields, 0) || !RequireVariable(fields, 1)) return NodeState.FAILURE;
            bool current = blackBoard.Get<bool>(fields[0].value);
            int previousIndex = fields[1].value;
            bool previous = blackBoard.Get<bool>(previousIndex);
            blackBoard.Set(previousIndex, current);
            return !current && previous ? NodeState.SUCCESS : NodeState.FAILURE;
        }
    }
}
