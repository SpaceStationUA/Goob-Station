// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;
using Content.Shared.Preferences.Loadouts;

namespace Content.Shared.Station;

public abstract partial class SharedStationSpawningSystem
{
    public void EquipReplacingRoleLoadout(EntityUid owner, RoleLoadout loadout, RoleLoadoutPrototype roleProto)
    {
        var slots = new HashSet<string>();
        foreach (var (group, selections) in loadout.SelectedLoadouts)
        {
            if (group.IsDeprecatedGroup(PrototypeManager))
                continue;

            foreach (var selection in selections)
            {
                if (!PrototypeManager.TryIndex(selection.Prototype, out var prototype))
                    continue;

                slots.UnionWith(prototype.Equipment.Keys);
                if (PrototypeManager.Resolve(prototype.StartingGear, out var gear))
                    slots.UnionWith(gear.Equipment.Keys);
            }
        }

        var displaced = new List<EntityUid>();
        foreach (var slot in slots)
        {
            if (InventorySystem.TryUnequip(owner, slot, out var item, silent: true, force: true))
                displaced.Add(item.Value);
        }

        EquipRoleLoadout(owner, loadout, roleProto);

        // Preserve role gear and its contents when a selected item replaces it.
        InventorySystem.TryGetSlotEntity(owner, "back", out var backpack);
        foreach (var item in displaced)
        {
            if (backpack is { } storageUid && item != storageUid &&
                _storageQuery.TryComp(storageUid, out var storage) &&
                _storage.Insert(storageUid, item, out _, storageComp: storage, playSound: false))
                continue;

            _handsSystem.TryPickupAnyHand(owner, item, checkActionBlocker: false, animate: false);
        }
    }

    private void EquipSelectedLoadoutStorage(
        EntityUid owner,
        string slot,
        IEnumerable<EntProtoId> prototypes,
        string? tint,
        string? name,
        string? description)
    {
        var coordinates = _xformSystem.GetMapCoordinates(owner);
        InventorySystem.TryGetSlotEntity(owner, slot, out var storageEntity);

        foreach (var prototype in prototypes)
        {
            var item = Spawn(prototype, coordinates);
            RaiseSelectedLoadoutEntitySpawned(item, owner, true);
            ApplyLoadoutTint(item, tint);
            ApplyLoadoutMetadata(item, name, description);

            if (storageEntity is { } storageUid
                && _storageQuery.TryComp(storageUid, out var storage)
                && _storage.Insert(storageUid, item, out _, storageComp: storage, playSound: false))
            {
                continue;
            }

            // Failed pickup leaves the item at the owner's feet.
            _handsSystem.TryPickupAnyHand(owner, item, checkActionBlocker: false, animate: false);
        }
    }
}
