// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Heretic.EntitySystems;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Content.Shared.Random.Helpers;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Pirate.Server.Heretic.Lock;

public sealed class LabyrinthPortalSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly GhoulSystem _ghoul = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    private readonly HashSet<Entity<PhysicsComponent>> _nearbyPhysics = new();
    private TimeSpan _nextSpawn;

    private static readonly TimeSpan SpawnDelay = TimeSpan.FromSeconds(1);

    private const int CollisionMask = (int) (CollisionGroup.Impassable |
                                              CollisionGroup.HighImpassable |
                                              CollisionGroup.LowImpassable |
                                              CollisionGroup.MidImpassable);

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextSpawn)
            return;

        _nextSpawn = now + SpawnDelay;

        var query = EntityQueryEnumerator<LabyrinthPortalComponent, TransformComponent>();
        while (query.MoveNext(out _, out var portal, out var transform))
        {
            if (portal.HereticMind is not { } hereticMind ||
                !TryComp(hereticMind, out MindComponent? mind) ||
                mind.OwnedEntity is not { } heretic ||
                TerminatingOrDeleted(heretic) ||
                _mobState.IsDead(heretic) ||
                !_random.Prob(portal.SpawnChance))
                continue;

            portal.SpawnedMobs.RemoveAll(uid => !Exists(uid));
            if (portal.SpawnedMobs.Count >= portal.MaxMobs)
                continue;

            portal.SpawnChance = MathF.Max(portal.MinSpawnChance,
                portal.SpawnChance - portal.ChanceReduction);

            _nearbyPhysics.Clear();
            _lookup.GetEntitiesInRange(transform.Coordinates, 1.5f, _nearbyPhysics, LookupFlags.Static);
            foreach (var nearby in _nearbyPhysics)
            {
                if (nearby.Comp.Hard && (nearby.Comp.CollisionLayer & CollisionMask) != 0)
                    QueueDel(nearby);
            }

            var table = _prototype.Index(portal.ToSpawn);
            var spawned = Spawn(table.Pick(_random), transform.Coordinates);
            portal.SpawnedMobs.Add(spawned);
            _ghoul.SetBoundHeretic(spawned, heretic);
        }
    }
}
