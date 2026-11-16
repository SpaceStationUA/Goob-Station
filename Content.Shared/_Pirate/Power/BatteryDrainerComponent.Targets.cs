using Content.Shared.Whitelist;

namespace Content.Shared.Ninja.Components;

public sealed partial class BatteryDrainerComponent
{
    /// <summary>
    /// Power sources this drainer cannot feed on.
    /// </summary>
    [DataField]
    public EntityWhitelist? TargetBlacklist;
}
