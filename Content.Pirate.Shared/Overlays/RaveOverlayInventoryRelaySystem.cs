using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;

namespace Content.Pirate.Shared.Overlays;

public sealed class RaveOverlayInventoryRelaySystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InventoryComponent, RefreshEquipmentHudEvent<RaveOverlayComponent>>(OnRefreshEquipmentHud);
    }

    private void OnRefreshEquipmentHud(
        Entity<InventoryComponent> inventory,
        ref RefreshEquipmentHudEvent<RaveOverlayComponent> args)
    {
        _inventory.RelayEvent(inventory, ref args);
    }
}
