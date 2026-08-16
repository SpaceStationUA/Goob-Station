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

    private float _accumulator;

    public override void Update(float frameTime)
    {
        if (!_net.IsServer)
            return;

        _accumulator += frameTime;
        if (_accumulator < 1f)
            return; // tick once per second

        var seconds = _accumulator;
        _accumulator = 0;

        var query = EntityQueryEnumerator<DefibrillatorSelfRechargeComponent, PowerCellSlotComponent>();
        while (query.MoveNext(out var uid, out var recharge, out var slot))
        {
            if (!_powerCell.TryGetBatteryFromSlot((uid, slot), out var battery))
                continue;

            var current = _battery.GetCharge(battery.Value.AsNullable());
            _battery.SetCharge(battery.Value.AsNullable(), current + recharge.RechargePerSecond * seconds);
        }
    }
}
