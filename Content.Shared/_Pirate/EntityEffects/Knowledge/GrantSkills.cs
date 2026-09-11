// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.Knowledge;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.EntityEffects.Knowledge;

public sealed partial class GrantSkills : EntityEffectBase<GrantSkills>
{
    [DataField(required: true)]
    public Dictionary<EntProtoId, int> Skills = new();

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => null;
}

public sealed partial class GrantSkillsEffectSystem : EntityEffectSystem<MetaDataComponent, GrantSkills>
{
    [Dependency] private readonly SharedKnowledgeSystem _knowledge = default!;

    protected override void Effect(Entity<MetaDataComponent> entity, ref EntityEffectEvent<GrantSkills> args)
    {
        _knowledge.GrantCompetency(entity.Owner, args.Effect.Skills);
    }
}
