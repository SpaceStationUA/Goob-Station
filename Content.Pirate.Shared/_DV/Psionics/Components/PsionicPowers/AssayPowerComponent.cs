using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._DV.Psionics.Components.PsionicPowers;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AssayPowerComponent : BasePsionicPowerComponent
{
    public override EntProtoId ActionProtoId { get; set; } = "ActionAssay";

    public override string PowerName { get; set; } = "psionic-power-name-assay";

    public override string? PowerInitFeedback { get; set; } = "assay-power-initialization-feedback";

    public override string? PowerMetapsionicFeedback { get; set; } = "assay-power-metapsionic-feedback";

    public override int MinGlimmerChanged { get; set; } = 3;

    public override int MaxGlimmerChanged { get; set; } = 6;

    /// <summary>
    /// How long the scan takes. Casting time.
    /// </summary>
    [DataField]
    public TimeSpan UseDelay = TimeSpan.FromSeconds(8);

    /// <summary>
    /// The font size of the feedback message sent to chat.
    /// </summary>
    [DataField]
    public int FontSize = 12;

    /// <summary>
    /// The color of the feedback message sent to chat.
    /// </summary>
    [DataField]
    public string FontColor = "#8A00C2";
}
