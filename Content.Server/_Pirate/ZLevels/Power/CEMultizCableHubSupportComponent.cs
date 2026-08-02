using Content.Shared.Stacks;
using Robust.Shared.Prototypes;

namespace Content.Server._Pirate.ZLevels.Power;

/// <summary>
/// Tracks the per-stack "support" cost that <see cref="CEMultizCableHubSystem"/> deducts from a
/// cross-deck cable hub when the hub is participating in cross-z linkage. Each linkage to a peer
/// hub consumes support proportional to the cable type forming the link.
/// </summary>
[RegisterComponent]
[Access(typeof(CEMultizCableHubSystem))]
public sealed partial class CEMultizCableHubSupportComponent : Component
{
    /// <summary>
    /// How many support points a single link of each cable type costs the hub. Keyed by the
    /// stack prototype id of the cable. Defaults to 5 for the three vanilla cable tiers so the
    /// hub can sustain a small mesh by default; servers tuning this should raise the values to
    /// reduce mesh density or lower them to allow larger meshes.
    /// </summary>
    /// <remarks>
    /// Persisted via <see cref="DataFieldAttribute"/> so YAML overrides can re-balance per
    /// station/prototype. The system reads these values during link establishment and decrements
    /// the hub's remaining support budget by the matching entry.
    /// </remarks>
    [DataField]
    public Dictionary<ProtoId<StackPrototype>, int> SupportLossStacks = new()
    {
        ["Cable"] = 5,
        ["CableMV"] = 5,
        ["CableHV"] = 5,
    };
}
