// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;
using Robust.Shared.Network;

namespace Content.Shared._Pirate.Defibrillator;

/// <summary>
/// Slowly recharges the power cell installed in a premium belt defibrillator
/// (CMO / combat / NT), so it tops itself up on its own. Server-only; the client
/// learns about it through the normal battery state sync.
/// </summary>
public sealed partial class DefibrillatorSelfRechargeSystem : EntitySystem
{
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly SharedBatterySystem _battery = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DefibrillatorSelfRechargeComponent, PowerCellChangedEvent>(OnPowerCellChanged);
    }

    private void OnPowerCellChanged(Entity<DefibrillatorSelfRechargeComponent> ent, ref PowerCellChangedEvent args)
    {
        // A new battery was inserted (or the old one removed). Reset the per-entity
        // accumulator so we only recharge using time elapsed since this battery
        // was installed — preventing a burst of charge from stale accumulated time.
        ent.Comp.AccumulatedTime = 0f;
    }

    public override void Update(float frameTime)
    {
        if (!_net.IsServer)
            return;

        var query = EntityQueryEnumerator<DefibrillatorSelfRechargeComponent, PowerCellSlotComponent>();
        while (query.MoveNext(out var uid, out var recharge, out var slot))
        {
            recharge.AccumulatedTime += frameTime;
            if (recharge.AccumulatedTime < 1f)
                continue; // tick once per second

            var seconds = recharge.AccumulatedTime;
            recharge.AccumulatedTime = 0f;

            if (!_powerCell.TryGetBatteryFromSlot((uid, slot), out var battery))
                continue;

            var current = _battery.GetCharge(battery.Value.AsNullable());
            _battery.SetCharge(battery.Value.AsNullable(), current + recharge.RechargePerSecond * seconds);
        }
    }
}
