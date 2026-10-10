// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.Knowledge;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Roles;

public sealed partial class KnowledgeCompetencySpecial : JobSpecial
{
    [DataField(required: true)]
    public Dictionary<EntProtoId, int> Skills = default!;

    public override void AfterEquip(EntityUid mob)
    {
        var entities = IoCManager.Resolve<IEntityManager>();
        entities.System<SharedKnowledgeSystem>().GrantCompetency(mob, Skills);
    }
}
