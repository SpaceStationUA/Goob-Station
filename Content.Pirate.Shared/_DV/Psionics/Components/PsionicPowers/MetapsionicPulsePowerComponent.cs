using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._DV.Psionics.Components.PsionicPowers;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MetapsionicPulsePowerComponent : BasePsionicPowerComponent
{
    public override EntProtoId ActionProtoId { get; set; } = "ActionMetapsionicPulse";

    public override string PowerName { get; set; } = "psionic-power-name-metapsionic";

    public override string? PowerInitFeedback { get; set; } = "metapsionic-power-initialization-feedback";

    public override string? PowerMetapsionicFeedback { get; set; } = "metapsionic-power-metapsionic-feedback";

    public override int MinGlimmerChanged { get; set; } = 0;

    public override int MaxGlimmerChanged { get; set; } = 5;

    /// <summary>
    /// The radius of the pulse.
    /// </summary>
    [DataField]
    public float Range = 1.5f;
}
