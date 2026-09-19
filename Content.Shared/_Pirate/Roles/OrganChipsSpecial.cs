// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.Body.Chips;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Roles;

public sealed partial class OrganChipsSpecial : JobSpecial
{
    [DataField(required: true)]
    public List<EntProtoId<OrganChipComponent>> Chips = default!;

    public override void AfterEquip(EntityUid mob)
    {
        var entities = IoCManager.Resolve<IEntityManager>();
        var chips = entities.System<OrganChipSystem>();

        foreach (var id in Chips)
            chips.InstallChip(mob, id);
    }
}
