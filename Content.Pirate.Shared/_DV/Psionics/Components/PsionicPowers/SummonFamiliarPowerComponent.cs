using Content.Shared._DV.Psionics.Systems.PsionicPowers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._DV.Psionics.Components.PsionicPowers;

/// <summary>
///     Pirate: Psionic power that summons a familiar mob bound to the psionic as its master.
///     Which familiar is summoned is fully prototype-driven via <see cref="Prototype"/> - to add
///     a new familiar, define a mob entity and a power entity using this component pointing at
///     it. The familiar is an NPC pet until taken over (for ghost-role familiars like Remilia)
///     and can be dispelled. One familiar at a time; summoning again requires the old one to die.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(SharedSummonFamiliarPowerSystem))]
public sealed partial class SummonFamiliarPowerComponent : BasePsionicPowerComponent
{
    public override EntProtoId ActionProtoId { get; set; } = "ActionSummonRemilia";

    public override string PowerName { get; set; } = "psionic-power-name-remilia";

    public override string? PowerInitFeedback { get; set; } = "remilia-power-initialization-feedback";

    public override string? PowerMetapsionicFeedback { get; set; } = "remilia-power-metapsionic-feedback";

    public override int MinGlimmerChanged { get; set; } = 3;

    public override int MaxGlimmerChanged { get; set; } = 10;

    /// <summary>
    /// The familiar mob prototype summoned by this power.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntProtoId Prototype = "MobBatRemilia";

    /// <summary>
    /// HTN compound the summoned familiar is switched to so it follows its master.
    /// Set to null to leave the mob's own HTN untouched. Server-only - HTN prototypes
    /// don't exist client-side, so this is a plain id string.
    /// </summary>
    [DataField]
    public string? FollowHtn = "FamiliarFollowCompound";

    /// <summary>
    /// How far (in tiles) the familiar may stray from its master. Past this it breaks off any
    /// fight and follows its master back. Also used as the follow blackboard range.
    /// </summary>
    [DataField]
    public float LeashRange = 6f;

    /// <summary>
    /// The currently summoned familiar, if any. Transient server state - never networked.
    /// </summary>
    public EntityUid? Summon;
}
