using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._DV.Psionics.Components.PsionicPowers;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AnoigoPowerComponent : BasePsionicPowerComponent
{
    public override EntProtoId ActionProtoId { get; set; } = "ActionAnoigo";

    public override string PowerName { get; set; } = "psionic-power-name-anoigo";

    public override string? PowerInitFeedback { get; set; } = "anoigo-power-initialization-feedback";

    public override string? PowerMetapsionicFeedback { get; set; } = "anoigo-power-metapsionic-feedback";

    public override int MinGlimmerChanged { get; set; } = 2;

    public override int MaxGlimmerChanged { get; set; } = 7;
}
