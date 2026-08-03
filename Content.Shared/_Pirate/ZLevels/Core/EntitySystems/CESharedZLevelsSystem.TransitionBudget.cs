namespace Content.Shared._Pirate.ZLevels.Core.EntitySystems;

/// <summary>
/// Per-tick budget gate for Z-level transitions. Shared default accepts every transition; the
/// server override (<c>CEZLevelsSystem.TransitionBudget.cs</c>) imposes a count + wallclock cap
/// so a tower-collapse event can't stall the simulation tick.
/// </summary>
public abstract partial class CESharedZLevelsSystem
{
    /// <param name="ent">The mover attempting to cross a Z layer.</param>
    /// <param name="offset">-1 for a downward fall, +1 for an upward auto-transfer.</param>
    /// <returns>True if the transition is allowed this tick; false to defer to the next tick.</returns>
    protected virtual bool CanProcessZLevelTransition(EntityUid ent, int offset)
    {
        return true;
    }
}
