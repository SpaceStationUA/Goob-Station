using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._DV.Psionics.Components.PsionicPowers;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PyrokinesisPowerComponent : BasePsionicPowerComponent
{
    public override EntProtoId ActionProtoId { get; set; } = "ActionPyrokinesis";

    public override string PowerName { get; set; } = "psionic-power-name-pyrokinesis";

    public override string? PowerInitFeedback { get; set; } = "pyrokinesis-power-initialization-feedback";

    public override string? PowerMetapsionicFeedback { get; set; } = "pyrokinesis-power-metapsionic-feedback";

    public override int MinGlimmerChanged { get; set; } = 5;

    public override int MaxGlimmerChanged { get; set; } = 15;

    /// <summary>
    /// How many firestacks will be added on the target.
    /// </summary>
    [DataField]
    public int AddedFirestacks = 5;
}
