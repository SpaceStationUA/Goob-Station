using Content.Shared.EntityEffects;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Shared.Chemistry.ReagentEffects;

/// <summary>
/// Removes psionics / mindbreaks a target. Server execution is handled via <see cref="EventEntityEffect{T}"/>.
/// </summary>
[UsedImplicitly]
public sealed partial class ChemRemovePsionic : EventEntityEffect<ChemRemovePsionic>
{
    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-chem-remove-psionic", ("chance", Probability));
}

