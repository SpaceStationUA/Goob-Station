// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.Clothing;
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

        var displaced = new List<(string Slot, EntityUid Item)>();
        foreach (var slot in slots)
        {
            if (InventorySystem.TryUnequip(owner, slot, out var item, silent: true, force: true))
                displaced.Add((slot, item.Value));
        }

        EquipRoleLoadout(owner, loadout, roleProto);

        // Preserve role gear and its contents when a selected item replaces it.
        InventorySystem.TryGetSlotEntity(owner, "back", out var backpack);
        foreach (var (slot, item) in displaced)
        {
            if (!InventorySystem.TryGetSlotEntity(owner, slot, out _) &&
                InventorySystem.TryEquip(owner, item, slot, silent: true, force: true))
                continue;

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

            if (TryDeferLoadoutAccessory(item, null, slot))
                continue;

            InsertSelectedLoadoutStorage(owner, storageEntity, item);
        }
    }

    private void InsertSelectedLoadoutStorage(EntityUid owner, EntityUid? storageEntity, EntityUid item)
    {
        if (storageEntity is { } storageUid
            && _storageQuery.TryComp(storageUid, out var storage)
            && _storage.Insert(storageUid, item, out _, storageComp: storage, playSound: false))
        {
            return;
        }

        _handsSystem.TryPickupAnyHand(owner, item, checkActionBlocker: false, animate: false);
    }

    #region Clothing accessories

    private List<(EntityUid Item, string? Slot, string? StorageSlot)>? _deferredLoadoutAccessories;

    private void EquipWithDeferredLoadoutAccessories(EntityUid owner, Action equip)
    {
        var previous = _deferredLoadoutAccessories;
        var deferred = new List<(EntityUid Item, string? Slot, string? StorageSlot)>();
        _deferredLoadoutAccessories = deferred;

        try
        {
            equip();
        }
        finally
        {
            _deferredLoadoutAccessories = previous;
        }

        EquipDeferredLoadoutAccessories(owner, deferred);
    }

    private bool TryDeferLoadoutAccessory(EntityUid item, string? slot, string? storageSlot)
    {
        if (_deferredLoadoutAccessories == null)
            return false;

        var ev = new LoadoutAccessoryCheckEvent();
        RaiseLocalEvent(item, ref ev);
        if (!ev.IsAccessory)
            return false;

        _deferredLoadoutAccessories.Add((item, slot, storageSlot));
        return true;
    }

    private void EquipDeferredLoadoutAccessories(EntityUid owner, List<(EntityUid Item, string? Slot, string? StorageSlot)> deferred)
    {
        foreach (var (item, slot, storageSlot) in deferred)
        {
            if (TerminatingOrDeleted(item))
                continue;

            var ev = new LoadoutAccessoryAttachEvent(owner);
            RaiseLocalEvent(item, ref ev);
            if (ev.Attached)
                continue;

            if (slot != null)
            {
                InventorySystem.TryEquip(owner, item, slot, silent: true, force: true);
                continue;
            }

            InventorySystem.TryGetSlotEntity(owner, storageSlot!, out var storageEntity);
            InsertSelectedLoadoutStorage(owner, storageEntity, item);
        }
    }

    #endregion
}
