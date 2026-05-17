namespace BehaviourTree.Runtime
{
    public enum NumericCompareOp : int
    {
        Equal = 0,
        NotEqual = 1,
        Less = 2,
        LessOrEqual = 3,
        Greater = 4,
        GreaterOrEqual = 5,
        ApproxEqual = 6,
    }

    public enum BoolCompareOp : int
    {
        Equal = 0,
        NotEqual = 1,
    }

    public enum VectorCompareOp : int
    {
        Equal = 0,
        NotEqual = 1,
        MagnitudeLess = 2,
        MagnitudeLessOrEqual = 3,
        MagnitudeGreater = 4,
        MagnitudeGreaterOrEqual = 5,
        MagnitudeApproxEqual = 6,
    }

    public enum ObjectCompareOp : int
    {
        Equal = 0,
        NotEqual = 1,
    }

    public enum NullCheckOp : int
    {
        IsNull = 0,
        IsNotNull = 1,
    }

    public enum BoolCheckOp : int
    {
        IsTrue = 0,
        IsFalse = 1,
    }
}
