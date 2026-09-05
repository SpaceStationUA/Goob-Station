// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Fluids;
using Robust.Shared.Map;

namespace Content.Shared._Pirate.Fluids;

/// <summary>
/// Event-driven one-shot spawner used when a forged metal melts into a puddle.
/// </summary>
public sealed class PuddleSpawnerSystem : EntitySystem
{
    [Dependency] private readonly SharedPuddleSystem _puddle = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PuddleSpawnerComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<PuddleSpawnerComponent> ent, ref MapInitEvent args)
    {
        var transform = Transform(ent.Owner);
        if (transform.MapID == MapId.Nullspace)
            return;

        _puddle.TrySpillAt(transform.Coordinates, ent.Comp.Solution, out _);
        PredictedQueueDel(ent.Owner);
    }
}
