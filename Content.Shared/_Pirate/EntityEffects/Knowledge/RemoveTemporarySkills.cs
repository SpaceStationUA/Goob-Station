// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.Knowledge;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.EntityEffects.Knowledge;

public sealed partial class RemoveTemporarySkills : EntityEffectBase<RemoveTemporarySkills>
{
    [DataField(required: true)]
    public string Source = default!;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => null;
}

public sealed partial class RemoveTemporarySkillsEffectSystem
    : EntityEffectSystem<KnowledgeHolderComponent, RemoveTemporarySkills>
{
    [Dependency] private readonly SharedKnowledgeSystem _knowledge = default!;

    protected override void Effect(Entity<KnowledgeHolderComponent> entity, ref EntityEffectEvent<RemoveTemporarySkills> args)
    {
        if (_knowledge.GetContainer(entity.Owner) is { } store)
            _knowledge.RemoveNamedTemporaryModifiers(store, args.Effect.Source);
    }
}
