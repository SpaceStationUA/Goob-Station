// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Body.Systems;

namespace Content.Shared._Pirate.Body.Chips;

public sealed class OrganChipsOnSpawnSystem : EntitySystem
{
    [Dependency] private readonly OrganChipSystem _chips = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Body initialization must run before chip installation.
        SubscribeLocalEvent<OrganChipsOnSpawnComponent, MapInitEvent>(OnMapInit,
            after: [typeof(SharedBodySystem)]);
    }

    private void OnMapInit(Entity<OrganChipsOnSpawnComponent> ent, ref MapInitEvent args)
    {
        foreach (var chip in ent.Comp.Chips)
            _chips.InstallChip(ent.Owner, chip);

        RemCompDeferred<OrganChipsOnSpawnComponent>(ent);
    }
}
