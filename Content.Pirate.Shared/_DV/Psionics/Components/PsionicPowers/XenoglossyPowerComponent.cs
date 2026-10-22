using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._DV.Psionics.Components.PsionicPowers;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class XenoglossyPowerComponent : BasePsionicPowerComponent
{
    // No action — xenoglossy is a purely passive power.
    // The value is never resolved because OnPowerInit skips Action.AddAction.
    public override EntProtoId ActionProtoId { get; set; } = "";

    public override string PowerName { get; set; } = "psionic-power-name-xenoglossy";

    public override string? PowerInitFeedback { get; set; } = "xenoglossy-power-initialization-feedback";

    public override string? PowerMetapsionicFeedback { get; set; } = "psionic-language-power-metapsionic-feedback";

    public override int MinGlimmerChanged { get; set; } = 0;

    public override int MaxGlimmerChanged { get; set; } = 0;
}
