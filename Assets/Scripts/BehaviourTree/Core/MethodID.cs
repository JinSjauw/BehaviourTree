namespace BehaviourTree.Core
{
        public enum MethodID 
    {
        NONE = 0,
        [MethodCategory(BehaviourNodeType.ACTION)]HELLOWORLD = 1,
        [MethodCategory(BehaviourNodeType.ACTION)]BYEWORLD = 2,
        [MethodCategory(BehaviourNodeType.CONDITION)]WAITWORLD = 3,
    }
}
