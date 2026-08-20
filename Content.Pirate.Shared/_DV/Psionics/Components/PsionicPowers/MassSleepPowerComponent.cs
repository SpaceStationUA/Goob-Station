using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._DV.Psionics.Components.PsionicPowers;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MassSleepPowerComponent : BasePsionicPowerComponent
{
    public override EntProtoId ActionProtoId { get; set; } = "ActionMassSleep";

    public override string PowerName { get; set; } = "psionic-power-name-mass-sleep";

    public override string? PowerInitFeedback { get; set; } = "mass-sleep-power-initialization-feedback";

    public override string? PowerMetapsionicFeedback { get; set; } = "mass-sleep-power-metapsionic-feedback";

    public override int MinGlimmerChanged { get; set; } = 7;

    public override int MaxGlimmerChanged { get; set; } = 15;

    /// <summary>
    /// The radius around the cursor point where people will fall asleep.
    /// </summary>
    [DataField]
    public float Radius = 1.5f;

    /// <summary>
    /// How long the victims will be asleep.
    /// </summary>
    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(5);
}
