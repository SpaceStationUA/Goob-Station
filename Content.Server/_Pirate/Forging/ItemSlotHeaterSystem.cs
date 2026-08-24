// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Temperature.Systems;
using Content.Shared._Pirate.Forging;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Examine;
using Content.Shared.Temperature.Components;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server._Pirate.Forging;

/// <summary>
/// Applies heat with one timer chain per occupied heater, never an entity-query update loop.
/// </summary>
public sealed class ItemSlotHeaterSystem : EntitySystem
{
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly TemperatureSystem _temperature = default!;

    private readonly HashSet<EntityUid> _active = new();
    private readonly HashSet<EntityUid> _scheduled = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ItemSlotHeaterComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<ItemSlotHeaterComponent, EntRemovedFromContainerMessage>(OnRemoved);
        SubscribeLocalEvent<ItemSlotHeaterComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<ItemSlotHeaterComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnInserted(Entity<ItemSlotHeaterComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (!_itemSlots.TryGetSlot(ent.Owner, ent.Comp.Slot, out var slot) || slot.Item != args.Entity)
            return;

        _active.Add(ent.Owner);
        Schedule(ent);
    }

    private void OnRemoved(Entity<ItemSlotHeaterComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (_itemSlots.GetItemOrNull(ent.Owner, ent.Comp.Slot) is null)
            _active.Remove(ent.Owner);
    }

    private void OnShutdown(Entity<ItemSlotHeaterComponent> ent, ref ComponentShutdown args)
    {
        _active.Remove(ent.Owner);
        _scheduled.Remove(ent.Owner);
    }

    private void Schedule(Entity<ItemSlotHeaterComponent> ent)
    {
        if (!_scheduled.Add(ent.Owner))
            return;

        Timer.Spawn(ent.Comp.Update, () => Heat(ent.Owner));
    }

    private void Heat(EntityUid uid)
    {
        _scheduled.Remove(uid);
        if (!_active.Contains(uid) || !TryComp<ItemSlotHeaterComponent>(uid, out var heater) ||
            _itemSlots.GetItemOrNull(uid, heater.Slot) is not { } item ||
            !TryComp<TemperatureComponent>(item, out var temperature))
        {
            _active.Remove(uid);
            return;
        }

        var reachedLimit = heater.HeatChange >= 0
            ? temperature.CurrentTemperature >= heater.MaxTemp
            : temperature.CurrentTemperature <= heater.MaxTemp;
        if (!reachedLimit)
            _temperature.ChangeHeat(item, heater.HeatChange, temperature: temperature);

        Schedule((uid, heater));
    }

    private void OnExamined(Entity<ItemSlotHeaterComponent> ent, ref ExaminedEvent args)
    {
        if (_itemSlots.GetItemOrNull(ent.Owner, ent.Comp.Slot) is not { } item ||
            !TryComp<TemperatureComponent>(item, out var temperature))
        {
            return;
        }

        args.PushMarkup(Loc.GetString("item-slot-heater-temp", ("temp", temperature.CurrentTemperature.ToString("F1"))));
    }
}
