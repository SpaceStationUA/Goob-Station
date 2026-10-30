// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;

namespace Content.Pirate.Shared.Overlays;

// Pirate: relay this HUD without moving Goob's inventory system into our module.
public sealed class JaniVisionInventorySystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InventoryComponent, RefreshEquipmentHudEvent<JaniVisionComponent>>(OnRefreshEquipmentHud);
    }

    private void OnRefreshEquipmentHud(Entity<InventoryComponent> ent, ref RefreshEquipmentHudEvent<JaniVisionComponent> args)
    {
        _inventory.RelayEvent((ent.Owner, ent.Comp), ref args);
    }
}
