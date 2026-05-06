using System;

namespace BehaviourTree.Core
{
    [AttributeUsage(AttributeTargets.Field)]
    public class MethodCategoryAttribute : Attribute
    {
        public BehaviourNodeType nodeCategory;
        public MethodCategoryAttribute(BehaviourNodeType category) => nodeCategory = category;
    }
}
