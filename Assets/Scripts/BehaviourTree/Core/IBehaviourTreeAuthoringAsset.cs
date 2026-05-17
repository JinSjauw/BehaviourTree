namespace BehaviourTree.Core
{
    public interface IBehaviourTreeAuthoringAsset
    {
        BehaviourNode Root { get; }
        BlackboardDefinition BlackboardDefinition { get; }
        string DisplayName { get; }
    }
}
