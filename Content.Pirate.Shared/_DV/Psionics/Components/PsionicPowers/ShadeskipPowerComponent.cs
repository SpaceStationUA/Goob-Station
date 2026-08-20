using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._DV.Psionics.Components.PsionicPowers;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ShadeskipPowerComponent : BasePsionicPowerComponent
{
    public override EntProtoId ActionProtoId { get; set; } = "ActionShadeskip";

    public override string PowerName { get; set; } = "psionic-power-name-shadeskip";

    public override string? PowerInitFeedback { get; set; } = "shadeskip-power-initialization-feedback";

    public override string? PowerMetapsionicFeedback { get; set; } = "shadeskip-power-metapsionic-feedback";

    public override int MinGlimmerChanged { get; set; } = 3;

    public override int MaxGlimmerChanged { get; set; } = 4;

    /// <summary>
    /// The minimum amount of shadow kudzu spawned.
    /// </summary>
    [DataField]
    public int MinAmount = 20;

    /// <summary>
    /// The maximum amount of shadow kudzu spawned.
    /// </summary>
    [DataField]
    public int MaxAmount = 25;

    /// <summary>
    /// The radius around the caster in which the kudzu will spawn.
    /// </summary>
    [DataField]
    public float MaxRange = 2.25f;
}
