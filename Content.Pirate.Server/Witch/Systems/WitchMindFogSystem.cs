using System;
using Content.Pirate.Shared.Witch.Components;
using Robust.Shared.Maths;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Pirate.Server.Witch.Systems;

public sealed class WitchMindFogSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private static readonly Direction[] Directions =
    [
        Direction.North,
        Direction.South,
        Direction.West,
        Direction.East,
    ];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WitchMindFogComponent, ComponentStartup>(OnStartup);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<WitchMindFogComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_timing.CurTime < comp.NextShuffle)
                continue;

            ShuffleMapping((uid, comp));
        }
    }

    private void OnStartup(Entity<WitchMindFogComponent> ent, ref ComponentStartup args)
    {
        ShuffleMapping(ent);
    }

    private void ShuffleMapping(Entity<WitchMindFogComponent> ent)
    {
        // Mind fog should feel unreliable, not like a clean one-to-one inversion.
        // We therefore allow duplicate mappings so two inputs can collapse into one direction.
        ent.Comp.UpDirection = PickDirection();
        ent.Comp.DownDirection = PickDirection();
        ent.Comp.LeftDirection = PickDirection();
        ent.Comp.RightDirection = PickDirection();

        if (ent.Comp.UpDirection == Direction.North &&
            ent.Comp.DownDirection == Direction.South &&
            ent.Comp.LeftDirection == Direction.West &&
            ent.Comp.RightDirection == Direction.East)
        {
            ent.Comp.RightDirection = PickDirection(excluding: Direction.East);
        }

        ent.Comp.NextShuffle = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.ShuffleInterval);

        Dirty(ent);
    }

    private Direction PickDirection(Direction? excluding = null)
    {
        Direction direction;
        do
        {
            direction = _random.Pick(Directions);
        } while (excluding != null && direction == excluding);

        return direction;
    }
}
