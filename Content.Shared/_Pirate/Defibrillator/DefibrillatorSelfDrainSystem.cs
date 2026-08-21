// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;
using Robust.Shared.Network;

namespace Content.Shared._Pirate.Defibrillator;

/// <summary>
/// Slowly drains the power cell installed in a belt defibrillator, so the charge
/// "drips" away on its own over time. Server-only; the client learns about it
/// through the normal battery state sync (which also updates the charge overlays).
/// </summary>
public sealed partial class DefibrillatorSelfDrainSystem : EntitySystem
{
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
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

        var query = EntityQueryEnumerator<DefibrillatorSelfDrainComponent, PowerCellSlotComponent>();
        while (query.MoveNext(out var uid, out var drain, out var slot))
        {
            if (!_powerCell.TryGetBatteryFromSlot((uid, slot), out var battery))
                continue;

            _powerCell.TryUseCharge((uid, slot), drain.DrainPerSecond * seconds);
        }
    }
}
