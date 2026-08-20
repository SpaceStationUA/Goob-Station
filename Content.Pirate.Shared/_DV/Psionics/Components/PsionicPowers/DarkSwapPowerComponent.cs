using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._DV.Psionics.Components.PsionicPowers;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DarkSwapPowerComponent : BasePsionicPowerComponent
{
    public override EntProtoId ActionProtoId { get; set; } = "ActionDarkSwap";

    public override string PowerName { get; set; } = "psionic-power-name-darkswap";

    public override string? PowerInitFeedback { get; set; } = "darkswap-power-initialization-feedback";

    public override string? PowerMetapsionicFeedback { get; set; } = "darkswap-power-metapsionic-feedback";

    public override int MinGlimmerChanged { get; set; } = 2;

    public override int MaxGlimmerChanged { get; set; } = 7;
}
