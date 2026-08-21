// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using Content.Shared.Power;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;
using Robust.Shared.GameObjects;

namespace Content.Shared._Pirate.Defibrillator;

/// <summary>
/// Tracks the charge level of a belt defibrillator's power cell and exposes it through appearance data,
/// so clients can render charge / missing-cell overlays.
/// </summary>
public sealed class SharedDefibrillatorChargeVisualsSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly SharedBatterySystem _battery = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DefibrillatorChargeVisualsComponent, PowerCellChangedEvent>(OnCellChanged);
        SubscribeLocalEvent<DefibrillatorChargeVisualsComponent, ChargeChangedEvent>(OnChargeChanged);
        SubscribeLocalEvent<DefibrillatorChargeVisualsComponent, ComponentStartup>(OnStartup);
    }

    private void OnCellChanged(Entity<DefibrillatorChargeVisualsComponent> ent, ref PowerCellChangedEvent args)
    {
        UpdateVisuals(ent);
    }

    private void OnChargeChanged(Entity<DefibrillatorChargeVisualsComponent> ent, ref ChargeChangedEvent args)
    {
        UpdateVisuals(ent);
    }

    private void OnStartup(Entity<DefibrillatorChargeVisualsComponent> ent, ref ComponentStartup args)
    {
        UpdateVisuals(ent);
    }

    private void UpdateVisuals(Entity<DefibrillatorChargeVisualsComponent> ent)
    {
        if (TryComp<PowerCellSlotComponent>(ent, out var slot)
            && _powerCell.TryGetBatteryFromSlotOrEntity((ent.Owner, slot), out var battery))
        {
            _appearance.SetData(ent.Owner, DefibrillatorChargeVisuals.NoCell, false);

            var max = battery.Value.Comp.MaxCharge;
            if (max <= 0)
            {
                _appearance.SetData(ent.Owner, DefibrillatorChargeVisuals.ChargeLevel, 0);
                return;
            }

            // Mirrors the SS13 behavior: level = ceil(charge / max * 4) -> 100/75/50/25.
            var level = (int) Math.Ceiling(_battery.GetCharge(battery.Value.AsNullable()) / max * 4);
            _appearance.SetData(ent.Owner, DefibrillatorChargeVisuals.ChargeLevel, level);
        }
        else
        {
            _appearance.SetData(ent.Owner, DefibrillatorChargeVisuals.NoCell, true);
            _appearance.SetData(ent.Owner, DefibrillatorChargeVisuals.ChargeLevel, 0);
        }
    }
}
