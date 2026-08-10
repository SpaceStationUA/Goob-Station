using Content.Shared.Inventory;
using Content.Shared.Power;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;

namespace Content.Pirate.Shared.ModularSuit;

public sealed class ModularSuitChargerSystem : EntitySystem
{
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly SharedBatterySystem _battery = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedPowerReceiverSystem _receiver = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ModularSuitComponent, RefreshChargeRateEvent>(OnSuitRefreshChargeRate);

        SubscribeLocalEvent<InsideChargerComponent, ComponentStartup>(OnInsideChargerStartup);
        SubscribeLocalEvent<InsideChargerComponent, ComponentShutdown>(OnInsideChargerShutdown);
    }

    private void OnSuitRefreshChargeRate(Entity<ModularSuitComponent> suit, ref RefreshChargeRateEvent args)
    {
        if (suit.Comp.Wearer is not { } wearer)
            return;

        if (!TryGetChargerFor(wearer, out var charger))
            return;

        args.NewChargeRate += charger.Comp.ChargeRate;
    }

    private void OnInsideChargerStartup(Entity<InsideChargerComponent> ent, ref ComponentStartup args)
    {
        RefreshWornSuit(ent.Owner);
    }

    private void OnInsideChargerShutdown(Entity<InsideChargerComponent> ent, ref ComponentShutdown args)
    {
        RefreshWornSuit(ent.Owner);
    }

    private void RefreshWornSuit(EntityUid wearer)
    {
        if (!TryComp<ModularSuitCarrierComponent>(wearer, out var carrier) || carrier.CurrentSlot == null)
            return;

        if (!_inventory.TryGetSlotEntity(wearer, carrier.CurrentSlot, out var suit))
            return;

        if (!HasComp<ModularSuitComponent>(suit))
            return;

        if (_powerCell.TryGetBatteryFromSlot(suit.Value, out var battery))
            _battery.RefreshChargeRate(battery.Value.AsNullable());
    }

    private bool TryGetChargerFor(EntityUid wearer, out Entity<ChargerComponent> charger)
    {
        charger = default;

        if (!_container.TryGetContainingContainer((wearer, null, null), out var container))
            return false;

        if (!TryComp<ChargerComponent>(container.Owner, out var chargerComp))
            return false;

        if (container.ID != chargerComp.SlotId)
            return false;

        if (!chargerComp.Portable && !_receiver.IsPowered(container.Owner))
            return false;

        if (_whitelist.IsWhitelistFail(chargerComp.Whitelist, wearer))
            return false;

        charger = (container.Owner, chargerComp);
        return true;
    }
}
