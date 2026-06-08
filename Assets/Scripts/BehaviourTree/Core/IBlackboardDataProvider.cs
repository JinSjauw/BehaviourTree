namespace BehaviourTree.Core
{
    /// <summary>
    /// Implement on any component that needs to push runtime data
    /// into the agent's self blackboard before tree evaluation.
    /// E.g. HealthComponent writes health, AmmoComponent writes ammo.
    /// Called once per evaluation tick before the agent tree runs.
    /// </summary>
    public interface IBlackboardDataProvider
    {
        void ProvideData(BlackBoard selfBB);
    }
}
