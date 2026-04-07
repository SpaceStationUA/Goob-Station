using Content.Shared.EntityEffects;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Shared.Chemistry.ReagentEffects;

/// <summary>
/// Rerolls psionics once. Server execution is handled via <see cref="EventEntityEffect{T}"/>.
/// </summary>
[UsedImplicitly]
public sealed partial class ChemRerollPsionic : EventEntityEffect<ChemRerollPsionic>
{
    /// <summary>
    /// Reroll multiplier.
    /// </summary>
    [DataField("bonusMultiplier")]
    public float BonusMuliplier = 1f;

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-chem-reroll-psionic", ("chance", Probability));
}

