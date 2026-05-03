using System.Linq;
using System.Numerics;
using Content.Goobstation.Common.MartialArts;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Physics;
using Content.Shared.Station;
using Content.Shared.Station.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;

namespace Content.Goobstation.Shared.Teleportation.Systems;

public partial class SharedRandomTeleportSystem
{
    [Dependency] private readonly SharedStationSystem _stationSystem = default!;

    /// <summary>
    /// Teleports an entity to a random location on the station.
    /// </summary>
    public Vector2? RandomTeleportToStation(EntityUid uid, int triesBase = 50, bool teleportPulledEntities = false)
    {
        if (!CanTeleport(uid))
            return null;

        if (!EntityManager.EntityExists(uid))
        {
            return null;
        }

        if (!TryComp<TransformComponent>(uid, out var xform))
        {
            return null;
        }

        var entityCoords = _xform.ToMapCoordinates(xform.Coordinates);
        var mapId = entityCoords.MapId;

        // Get the station on this map
        var stationUid = _stationSystem.GetStationInMap(mapId);
        if (stationUid == null)
        {
            return null;
        }

        // Get all grids owned by the station
        if (!TryComp<StationDataComponent>(stationUid.Value, out var stationData))
        {
            return null;
        }

        var stationGrids = stationData.Grids
            .Where(gridUid => TryComp<MapGridComponent>(gridUid, out _))
            .Select(gridUid => new Entity<MapGridComponent>(gridUid, CompOrNull<MapGridComponent>(gridUid)!))
            .ToList();

        if (stationGrids.Count == 0)
        {
            return null;
        }


        // Pick a random station grid
        var stationGrid = stationGrids[_random.Next(stationGrids.Count)];

        var targetCoords = new MapCoordinates();
        var foundValid = false;
        EntityUid? pullableEntity = null;

        if (TryComp<PullerComponent>(uid, out var puller))
        {
            pullableEntity = puller.Pulling;
        }

        var candidateTiles = _map.GetAllTiles(stationGrid.Owner, stationGrid.Comp, ignoreEmpty: true).ToList();
        if (candidateTiles.Count == 0)
        {
            return null;
        }

        var candidateIndices = Enumerable.Range(0, candidateTiles.Count).ToArray();
        var maxSamples = Math.Min(triesBase, candidateTiles.Count);

        for (var i = 0; i < maxSamples; i++)
        {
            var swapIndex = _random.Next(i, candidateIndices.Length);
            (candidateIndices[i], candidateIndices[swapIndex]) = (candidateIndices[swapIndex], candidateIndices[i]);

            var tile = candidateTiles[candidateIndices[i]];
            targetCoords = _map.GridTileToWorld(stationGrid.Owner, stationGrid.Comp, tile.GridIndices);

            var valid = true;
            foreach (var entity in _map.GetAnchoredEntities(stationGrid.Owner, stationGrid.Comp, tile.GridIndices))
            {
                if (!_physicsQuery.TryGetComponent(entity, out var body))
                    continue;

                if (body.BodyType != BodyType.Static || !body.Hard ||
                    (body.CollisionLayer & (int) CollisionGroup.Impassable) == 0)
                    continue;

                valid = false;
                break;
            }

            if (valid)
            {
                foundValid = true;
                break;
            }
        }

        if (!foundValid)
        {
            return null;
        }

        _pullingSystem.StopAllPulls(uid);

        var newPos = targetCoords.Position;
        _xform.SetWorldPosition(uid, newPos);

        if (pullableEntity != null && teleportPulledEntities)
        {
            _xform.SetWorldPosition(pullableEntity.Value, newPos);
            _pullingSystem.TryStartPull(uid, pullableEntity.Value, force: true);
        }

        return newPos;
    }
}
