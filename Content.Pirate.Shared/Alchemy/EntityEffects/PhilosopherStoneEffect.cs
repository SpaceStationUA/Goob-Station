using Content.Shared.EntityEffects;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Shared.Alchemy.EntityEffects;

[UsedImplicitly, DataDefinition]
public sealed partial class PhilosopherStoneEffect : EventEntityEffect<PhilosopherStoneEffect>
{
    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return null;
    }
}
