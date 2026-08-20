using Content.Server._DV.StationEvents.GameRules;

namespace Content.Server._DV.StationEvents.Components;

[RegisterComponent, Access(typeof(NoosphericSilenceRule))]
public sealed partial class NoosphericSilenceRuleComponent : Component
{
    /// <summary>
    /// Minimum amount of psionics to mute.
    /// </summary>
    [DataField]
    public int MinAffected = 2;

    /// <summary>
    /// Maximum amount of psionics to mute (inclusive).
    /// </summary>
    [DataField]
    public int MaxAffected = 6;

    /// <summary>
    /// Minimum mute duration.
    /// </summary>
    [DataField]
    public TimeSpan MinDuration = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Maximum mute duration.
    /// </summary>
    [DataField]
    public TimeSpan MaxDuration = TimeSpan.FromSeconds(80);
}
