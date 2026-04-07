using Content.Shared.EntityEffects;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Shared.Alchemy.EntityEffects;

[UsedImplicitly, DataDefinition]
public sealed partial class PirateAcidCorrode : EventEntityEffect<PirateAcidCorrode>
{
    [DataField]
    public float CausticDamage = 6f;

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return null;
    }
}
