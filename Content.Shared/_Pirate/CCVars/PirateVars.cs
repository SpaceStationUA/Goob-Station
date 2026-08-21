using Robust.Shared.Configuration;

namespace Content.Shared._Pirate.CCVars;

/// <summary>
/// Pirate specific cvars for the trait system.
/// </summary>
[CVarDefs]
public sealed partial class PirateVars
{

    /// <summary>
    /// Maximum number of traits that can be selected globally.
    /// </summary>
    public static readonly CVarDef<int> MaxTraitCount =
        CVarDef.Create("traits.pirate.max_count", 10, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Maximum trait points available to spend.
    /// Traits with positive cost consume points, negative cost traits grant points.
    /// </summary>
    public static readonly CVarDef<int> MaxTraitPoints =
        CVarDef.Create("traits.pirate.max_points", 3, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Whether to skip showing the disabled traits popup when spawning.
    /// </summary>
    public static readonly CVarDef<bool> SkipDisabledTraitsPopup =
        CVarDef.Create("traits.pirate.skip_disabled_traits_popup", false, CVar.CLIENT | CVar.ARCHIVE);

    /// <summary>
    /// Allow ethereal shadowkin entities to pass through walls and objects while ethereal.
    /// </summary>
    public static readonly CVarDef<bool> EtherealPassThrough =
        CVarDef.Create("ic.EtherealPassThrough", false, CVar.SERVER);

    /// <summary>
    /// Whether glimmer is enabled.
    /// </summary>
    public static readonly CVarDef<bool> GlimmerEnabled =
        CVarDef.Create("glimmer.enabled", true, CVar.REPLICATED);

    /// <summary>
    /// Passive glimmer drain per second.
    /// Note that this is randomized and this is an average value.
    /// </summary>
    public static readonly CVarDef<float> GlimmerLostPerSecond =
        CVarDef.Create("glimmer.passive_drain_per_second", 0.1f, CVar.SERVERONLY);
}
