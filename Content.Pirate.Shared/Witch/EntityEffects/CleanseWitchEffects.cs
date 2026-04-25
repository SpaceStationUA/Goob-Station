using Content.Shared.EntityEffects;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Shared.Witch.EntityEffects;

[UsedImplicitly, DataDefinition]
public sealed partial class CleanseWitchEffects : EventEntityEffect<CleanseWitchEffects>
{
    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return null;
    }
}
