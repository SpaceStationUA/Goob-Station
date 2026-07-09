using Content.Shared.Maps;
using Content.Shared.Physics;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Pirate.Shared.TerrorSpider.EntitySystems;

public sealed class EntityBeaconSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<EntityBeaconComponent>();
        while (query.MoveNext(out var uid, out var beacon))
        {
            if (beacon.Range >= beacon.RangeLimit)
                continue;

            if (beacon.NextUpdateTime == TimeSpan.Zero)
                beacon.NextUpdateTime = _timing.CurTime;

            if (_timing.CurTime < beacon.NextUpdateTime)
                continue;

            beacon.NextUpdateTime += beacon.Delay;

            if (beacon.CoordinatesToSpawn.Count <= 6)
                CacheSpawnCoordinates(uid, beacon);

            if (beacon.CoordinatesToSpawn.Count <= 6)
                continue;

            var coordinates = _random.Pick(beacon.CoordinatesToSpawn);
            beacon.CoordinatesToSpawn.Remove(coordinates);

            PredictedSpawnAtPosition(_random.Pick(beacon.EntitiesToSpawn), coordinates);
        }
    }

    private void CacheSpawnCoordinates(EntityUid uid, EntityBeaconComponent beacon)
    {
        beacon.Range = Math.Min(beacon.RangeLimit, beacon.Range + 2);

        var xform = Transform(uid);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var center = _map.GetTileRef(gridUid, grid, xform.Coordinates).GridIndices;
        for (var x = center.X - beacon.Range; x <= center.X + beacon.Range; x++)
        {
            for (var y = center.Y - beacon.Range; y <= center.Y + beacon.Range; y++)
            {
                var tile = new Vector2i(x, y);
                if (!_map.TryGetTileRef(gridUid, grid, tile, out var tileRef)
                    || tileRef.Tile.IsEmpty
                    || _turf.IsTileBlocked(tileRef, CollisionGroup.MobMask))
                    continue;

                var coordinates = _map.GridTileToLocal(gridUid, grid, tileRef.GridIndices);
                var hasSpawn = false;
                foreach (var entity in _lookup.GetEntitiesIntersecting(coordinates))
                {
                    if (MetaData(entity).EntityPrototype is { ID: var id } && beacon.EntitiesToSpawn.Contains(id))
                    {
                        hasSpawn = true;
                        break;
                    }
                }

                if (!hasSpawn)
                    beacon.CoordinatesToSpawn.Add(coordinates);
            }
        }
    }
}
