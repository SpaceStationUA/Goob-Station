// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Pirate.Body.Chips;

public sealed class OrganChipsOnSpawnSystem : EntitySystem
{
    [Dependency] private readonly OrganChipSystem _chips = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OrganChipsOnSpawnComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<OrganChipsOnSpawnComponent> ent, ref MapInitEvent args)
    {
        foreach (var chip in ent.Comp.Chips)
            _chips.InstallChip(ent.Owner, chip);

        RemCompDeferred<OrganChipsOnSpawnComponent>(ent);
    }
}
