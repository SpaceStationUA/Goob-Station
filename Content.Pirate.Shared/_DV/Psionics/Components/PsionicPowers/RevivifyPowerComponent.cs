using Content.Shared.Damage;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._DV.Psionics.Components.PsionicPowers;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RevivifyPowerComponent : BasePsionicPowerComponent
{
    public override EntProtoId ActionProtoId { get; set; } = "ActionRevivify";

    public override string PowerName { get; set; } = "psionic-power-name-revivify";

    public override string? PowerInitFeedback { get; set; } = "revivify-power-initialization-feedback";

    public override string? PowerMetapsionicFeedback { get; set; } = "revivify-power-metapsionic-feedback";

    public override int MinGlimmerChanged { get; set; } = 5;

    public override int MaxGlimmerChanged { get; set; } = 10;

    /// <summary>
    /// How much the target is healed on a successful cast.
    /// </summary>
    [DataField]
    public DamageSpecifier? HealingAmount;

    /// <summary>
    /// How much rot is reduced on the target, in seconds.
    /// </summary>
    [DataField]
    public float RotReduction;

    /// <summary>
    /// Whether this power can revive the dead.
    /// </summary>
    [DataField]
    public bool DoRevive = true;

    /// <summary>
    /// How long the DoAfter takes. Casting time.
    /// </summary>
    [DataField]
    public TimeSpan UseDelay = TimeSpan.FromSeconds(8);
}
