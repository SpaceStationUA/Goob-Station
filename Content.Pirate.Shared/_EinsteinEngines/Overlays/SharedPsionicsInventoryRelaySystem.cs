using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Overlays;

namespace Content.Pirate.Shared._EinsteinEngines.Overlays;

/// <summary>
/// Pirate / EinsteinEngines: inventory relay wiring for the psionics HUD.
///
/// Core <see cref="InventorySystem"/> cannot reference <see cref="ShowPsionicsRecordIconsComponent"/>
/// (it lives in the Pirate module), so we hook the by-ref
/// <see cref="RefreshEquipmentHudEvent{T}"/> here and delegate to
/// <see cref="InventorySystem.RelayEvent{T}(Entity{InventoryComponent}, ref T)"/>.
///
/// This mirrors the built-in subscriptions in
/// <c>Content.Shared/Inventory/InventorySystem.Relay.cs</c> for other HUDs like
/// <see cref="ShowCriminalRecordIconsComponent"/>, but keeps the dependency
/// one-way (core → Pirate).
///
/// Runs on both client and server, so the client-side
/// <see cref="Content.Client.Overlays.EquipmentHudSystem{T}"/> can activate the
/// psionics HUD when appropriate gear is equipped.
/// </summary>
public sealed class SharedPsionicsInventoryRelaySystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Relay RefreshEquipmentHudEvent<ShowPsionicsRecordIconsComponent> from the
        // owning InventoryComponent (the mob) to its equipped items, exactly like
        // InventorySystem.InitializeRelay does for other equipment HUDs.
        SubscribeLocalEvent<InventoryComponent, RefreshEquipmentHudEvent<ShowPsionicsRecordIconsComponent>>(OnRefreshEquipmentHud);
    }

    private void OnRefreshEquipmentHud(EntityUid uid,
        InventoryComponent component,
        ref RefreshEquipmentHudEvent<ShowPsionicsRecordIconsComponent> args)
    {
        _inventory.RelayEvent((uid, component), ref args);
    }
}
