// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Power;
using Content.Shared.PowerCell;
using Content.Shared.Weapons.Ranged.Components;

namespace Content.Shared.Weapons.Ranged.Systems;

public abstract partial class SharedGunSystem
{
    [Dependency] private readonly PowerCellSystem _fireCostCells = default!;

    public void SetBatteryFireCost(Entity<BatteryAmmoProviderComponent> ent, float fireCost)
    {
        ent.Comp.FireCost = fireCost;
        UpdateShots(ent);

        var charge = new GetChargeEvent();
        RaiseLocalEvent(ent, ref charge);
        var chargeRate = _fireCostCells.TryGetBatteryFromEntityOrSlot(ent.Owner, out var battery)
            ? battery.Value.Comp.ChargeRate
            : 0f;
        UpdateNextUpdate(ent, charge.CurrentCharge, charge.MaxCharge, chargeRate);
        Dirty(ent);
    }
}
