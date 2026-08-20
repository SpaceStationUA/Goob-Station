using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._DV.Psionics.Components.PsionicPowers;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MindSwapPowerComponent : BasePsionicPowerComponent
{
    public override EntProtoId ActionProtoId { get; set; } = "ActionMindSwapPsionic";

    public override string PowerName { get; set; } = "psionic-power-name-mindswap";

    public override string? PowerInitFeedback { get; set; } = "mind-swap-power-initialization-feedback";

    public override string? PowerMetapsionicFeedback { get; set; } = "mind-swap-power-metapsionic-feedback";

    public override int MinGlimmerChanged { get; set; } = 2;

    public override int MaxGlimmerChanged { get; set; } = 7;
}
