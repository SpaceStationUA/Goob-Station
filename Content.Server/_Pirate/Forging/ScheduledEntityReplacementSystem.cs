// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.Forging;
using Robust.Shared.Timing;

namespace Content.Server._Pirate.Forging;

public sealed class ScheduledEntityReplacementSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ScheduledEntityReplacementComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<ScheduledEntityReplacementComponent> ent, ref MapInitEvent args)
    {
        Timer.Spawn(ent.Comp.Duration, () => Complete(ent.Owner));
    }

    public bool Complete(EntityUid uid)
    {
        if (!TryComp<ScheduledEntityReplacementComponent>(uid, out var replacement))
            return false;

        var transform = Transform(uid);
        var result = Spawn(replacement.Result, transform.Coordinates);
        _transform.SetLocalRotation(result, transform.LocalRotation);
        QueueDel(uid);
        return true;
    }
}
