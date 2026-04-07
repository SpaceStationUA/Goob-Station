using Content.Shared.EntityEffects;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Shared.Chemistry.ReagentEffects;

/// <summary>
/// Restores a psionic reroll. Server execution is handled via <see cref="EventEntityEffect{T}"/>.
/// </summary>
[UsedImplicitly]
public sealed partial class ChemRestorePsionicReroll : EventEntityEffect<ChemRestorePsionicReroll>
{
    [DataField]
    public bool BypassRoller;

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-chem-restorereroll-psionic");
}

