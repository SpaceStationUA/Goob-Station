using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._DV.EntityEffects.Effects.Psionics;

/// <inheritdoc cref="EntityEffect"/>
/// <summary>
///     Permanently mindbreaks the target: removes all psionic abilities and psionic
///     potential, and fully insulates them from psionics.
/// </summary>
public sealed partial class MindBreak : EventEntityEffect<MindBreak>
{
    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-chem-mindbreak", ("chance", Probability));
}
