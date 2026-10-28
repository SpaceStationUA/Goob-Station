// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;

namespace Content.Shared.Station;

public abstract partial class SharedStationSpawningSystem
{
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
