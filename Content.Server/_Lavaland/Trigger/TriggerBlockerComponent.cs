using Content.Shared.Whitelist;

namespace Content.Server._Lavaland.Trigger;

/// <summary>
/// Simple component for blocking actions in some specific scenarios
/// </summary>
[RegisterComponent]
public sealed partial class TriggerBlockerComponent : Component
{
    [DataField]
    public EntityWhitelist? MapWhitelist;

    [DataField]
    public EntityWhitelist? MapBlacklist;
    #region Pirates: death rattle update
    /// <summary>
    /// Blocks trigger activation while the trigger user (or owner if no user) is on a station grid.
    /// </summary>
    [DataField]
    public bool RequireOffStation;
    #endregion
}
