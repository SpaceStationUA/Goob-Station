using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._DV.Psionics.Components;

/// <summary>
/// Entities with this component are psionics and can use powers.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class PsionicComponent : Component
{
    /// <summary>
    /// The list of the action buttons for every power.
    /// </summary>
    [DataField]
    public HashSet<EntityUid?> PsionicPowersActionEntities = [];

    /// <summary>
    /// Extra powers added to this psionic's random pull, keyed by power entity prototype id -> weight.
    /// Populated when the psionic gains a power that unlocks another (e.g. Healing Word unlocks Revivify).
    /// Merged into the base power table whenever a new power is rolled.
    /// </summary>
    [DataField]
    public Dictionary<EntProtoId, float> PowerPoolAdditions = new();

    /// <summary>
    /// Whether the psionic gets stunned when a psionic power gets removed. This doesn't mean they lost all psionic powers.
    /// Psionic powers themselves regulate if they can be removed.
    /// </summary>
    [DataField]
    public bool StunOnRemoval = true;
}
