// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.Knowledge;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.EntityEffects.Knowledge;

public sealed partial class GrantTemporarySkills : EntityEffectBase<GrantTemporarySkills>
{
    [DataField(required: true)]
    public string Source = default!;

    [DataField(required: true)]
    public Dictionary<EntProtoId, int> Skills = new();

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => null;
}

public sealed partial class GrantTemporarySkillsEffectSystem
    : EntityEffectSystem<MetaDataComponent, GrantTemporarySkills>
{
    [Dependency] private readonly SharedKnowledgeSystem _knowledge = default!;

    protected override void Effect(Entity<MetaDataComponent> entity, ref EntityEffectEvent<GrantTemporarySkills> args)
    {
        var store = _knowledge.EnsureKnowledgeContainer(entity.Owner);
        foreach (var (id, level) in args.Effect.Skills)
            _knowledge.SetNamedTemporaryModifier(store, id, args.Effect.Source, level);
    }
}
