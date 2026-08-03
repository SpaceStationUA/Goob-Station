using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._DV.Psionics.Components.PsionicPowers;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MindSwappedReturnPowerComponent : BasePsionicPowerComponent
{
    public override EntProtoId ActionProtoId { get; set; } = "ActionMindSwapReturn";

    public override string PowerName { get; set; } = "psionic-power-name-mindswap-return";

    public override string? PowerInitFeedback { get; set; } = "mind-swap-return-power-initialization-feedback";

    public override string? PowerMetapsionicFeedback { get; set; } = "mind-swap-return-power-metapsionic-feedback";

    public override int MinGlimmerChanged { get; set; } = 0;

    public override int MaxGlimmerChanged { get; set; } = 0;

    [DataField, AutoNetworkedField]
    public EntityUid OriginalEntity;
}
