using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._DV.EntityEffects.Effects.Glimmer;

/// <inheritdoc cref="EntityEffect"/>
public sealed partial class AffectsGlimmer : EventEntityEffect<AffectsGlimmer>
{
    /// <summary>
    ///     Amount that is added or subtracted from glimmer.
    /// </summary>
    [DataField]
    public int Amount = 1;

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-change-glimmer-reaction-effect", ("chance", Probability),
            ("amount", Amount));
}
