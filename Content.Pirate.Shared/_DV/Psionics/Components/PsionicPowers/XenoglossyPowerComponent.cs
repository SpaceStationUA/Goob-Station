using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._DV.Psionics.Components.PsionicPowers;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class XenoglossyPowerComponent : BasePsionicPowerComponent
{
    // No action — xenoglossy is a purely passive power.
    public override EntProtoId? ActionProtoId { get; set; } = null;

    public override string PowerName { get; set; } = "psionic-power-name-xenoglossy";

    public override string? PowerInitFeedback { get; set; } = "xenoglossy-power-initialization-feedback";

    public override string? PowerMetapsionicFeedback { get; set; } = "psionic-language-power-metapsionic-feedback";

    public override int MinGlimmerChanged { get; set; } = 0;

    public override int MaxGlimmerChanged { get; set; } = 0;
}
