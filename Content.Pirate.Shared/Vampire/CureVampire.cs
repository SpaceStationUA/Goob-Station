using Content.Shared.EntityEffects;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Shared.Vampire;

/// <summary>
/// Holy water reagent effect that cures antag vampires back into mortals.
/// Implemented as an <see cref="EventEntityEffect{T}"/> so it can be instantiated client-side,
/// while still executing server-side.
/// </summary>
[UsedImplicitly]
public sealed partial class CureVampire : EventEntityEffect<CureVampire>
{
    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        // No dedicated guidebook entry for now.
        return null;
    }
}

