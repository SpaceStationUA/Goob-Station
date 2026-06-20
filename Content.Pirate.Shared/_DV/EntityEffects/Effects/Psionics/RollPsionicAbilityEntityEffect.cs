using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._DV.EntityEffects.Effects.Psionics;

/// <inheritdoc cref="EntityEffect"/>
public sealed partial class RollPsionicAbility : EventEntityEffect<RollPsionicAbility>
{
    /// <summary>
    ///     Reroll multiplier.
    /// </summary>
    [DataField]
    public float BonusMultiplier = 1f;

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-chem-roll-psionic", ("chance", Probability), ("multiplier", BonusMultiplier));
}
