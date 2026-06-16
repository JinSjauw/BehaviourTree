namespace BehaviourTree
{
    public enum AbortType : byte
    {
        None = 0,
        Self = 1,
        LowerPriority = 2,
        Both = 3,
    }
}
