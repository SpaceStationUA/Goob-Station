using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._DV.EntityEffects.Effects.Psionics;

/// <inheritdoc cref="EntityEffect"/>
public sealed partial class RemovePsionicAbilities : EventEntityEffect<RemovePsionicAbilities>
{
    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-chem-remove-psionic", ("chance", Probability));
}
