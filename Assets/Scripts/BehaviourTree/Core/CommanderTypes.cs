namespace BehaviourTree.Core
{
    /// <summary>
    /// Roles that a commander tree can assign to agents.
    /// Enum values are intentionally small to pack into blackboard int slots.
    /// </summary>
    public enum TacticalRole
    {
        NONE = 0,
        SCOUT = 1,
        SUPPRESSOR = 2,
        FLANKER = 3,
        ARTILLERY = 4,
        ASSAULT = 5,
        COVER = 6,
    }

    /// <summary>
    /// Formation types the commander can order agents into.
    /// </summary>
    public enum FormationType
    {
        LINE = 0,
        WEDGE = 1,
        COLUMN = 2,
        SCATTERED = 3,
        BOX = 4,
    }

    /// <summary>
    /// Steps in a sequenced tactic (room clearing, ambush, etc.).
    /// </summary>
    public enum TacticPhase
    {
        READY = 0,
        MOVING = 1,
        HOLD = 2,
        ENGAGE = 3,
    }
}
