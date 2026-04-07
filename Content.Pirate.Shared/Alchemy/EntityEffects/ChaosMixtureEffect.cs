using Content.Shared.Chemistry.Reagent;
using Content.Shared.EntityEffects;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Shared.Alchemy.EntityEffects;

[UsedImplicitly, DataDefinition]
public sealed partial class ChaosMixtureEffect : EventEntityEffect<ChaosMixtureEffect>
{
    [DataField]
    public float Quantity = 6f;

    [DataField(required: true)]
    public List<ProtoId<ReagentPrototype>> Reagents = new();

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return Loc.GetString("chaos-mixture-effect-guidebook");
    }
}
