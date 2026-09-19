// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.Body.Chips;
using Content.Shared.Body.Components;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.EntityEffects.Knowledge;

public sealed partial class InstallOrganChip : EntityEffectBase<InstallOrganChip>
{
    [DataField(required: true)]
    public EntProtoId<OrganChipComponent> Chip;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => null;
}

public sealed partial class InstallOrganChipEffectSystem : EntityEffectSystem<BodyComponent, InstallOrganChip>
{
    [Dependency] private readonly OrganChipSystem _chips = default!;

    protected override void Effect(Entity<BodyComponent> entity, ref EntityEffectEvent<InstallOrganChip> args)
    {
        _chips.InstallChip(entity.Owner, args.Effect.Chip);
    }
}
