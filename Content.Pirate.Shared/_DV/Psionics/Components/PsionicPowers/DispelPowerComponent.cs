using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._DV.Psionics.Components.PsionicPowers;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DispelPowerComponent : BasePsionicPowerComponent
{
    public override EntProtoId ActionProtoId { get; set; } = "ActionDispel";

    public override string PowerName { get; set; } = "psionic-power-name-dispel";

    public override string? PowerInitFeedback { get; set; } = "dispel-power-initialization-feedback";

    public override string? PowerMetapsionicFeedback { get; set; } = "dispel-power-metapsionic-feedback";

    public override int MinGlimmerChanged { get; set; } = 2;

    public override int MaxGlimmerChanged { get; set; } = 5;
}
