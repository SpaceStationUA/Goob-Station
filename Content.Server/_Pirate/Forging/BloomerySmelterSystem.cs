// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.Forging;
using Robust.Shared.Timing;

namespace Content.Server._Pirate.Forging;

/// <summary>
/// Completes each lit bloomery using one scheduled callback, with no damage ticks or entity scans.
/// </summary>
public sealed class BloomerySmelterSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BloomerySmelterComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<BloomerySmelterComponent> ent, ref MapInitEvent args)
    {
        Timer.Spawn(ent.Comp.Duration, () => Complete(ent.Owner));
    }

    public bool Complete(EntityUid uid)
    {
        if (!TryComp<BloomerySmelterComponent>(uid, out var smelter))
            return false;

        var transform = Transform(uid);
        var rotation = transform.LocalRotation;
        var result = Spawn(smelter.Result, transform.Coordinates);
        _transform.SetLocalRotation(result, rotation);
        QueueDel(uid);
        return true;
    }
}
