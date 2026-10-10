// SPDX-License-Identifier: AGPL-3.0-or-later
// Pirate: server-side RandomTeleportToStation with proper atmosphere and main-grid checks.

using System.Linq;
using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Station.Systems;
using Content.Shared.Physics;
using Content.Shared.Station.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Random;

namespace Content.Server._Pirate.Teleportation;

/// <summary>
/// Server-side system that teleports an entity to a safe tile on the main station grid.
/// Uses <see cref="AtmosphereSystem"/> to skip space/air-blocked tiles, and
/// <see cref="StationSystem.GetLargestGrid"/> to avoid landing on shuttles or debris.
/// </summary>
public sealed class RandomTeleportSystemPirate : EntitySystem
{
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;

    private EntityQuery<PhysicsComponent> _physicsQuery;

    public override void Initialize()
    {
        base.Initialize();
        _physicsQuery = GetEntityQuery<PhysicsComponent>();
    }

    /// <summary>
    /// Teleports <paramref name="uid"/> to a random safe tile on the main station grid.
    /// Returns the new world position, or <c>null</c> if no valid tile was found.
    /// </summary>
    public Vector2? RandomTeleportToStation(EntityUid uid, int triesBase = 50, bool teleportPulledEntities = false)
    {
        if (!EntityManager.EntityExists(uid))
            return null;

        if (!TryComp<TransformComponent>(uid, out var xform))
            return null;

        var mapId = _xform.ToMapCoordinates(xform.Coordinates).MapId;

        // Locate the station on this map
        var stationUid = _station.GetStationInMap(mapId);
        if (stationUid == null)
            return null;

        if (!TryComp<StationDataComponent>(stationUid.Value, out var stationData))
            return null;

        // GetLargestGrid picks the main station body, not shuttles or debris grids
        var mainGridUid = _station.GetLargestGrid((stationUid.Value, stationData));
        if (mainGridUid == null || !TryComp<MapGridComponent>(mainGridUid.Value, out var mainGridComp))
            return null;

        var stationGrid = new Entity<MapGridComponent>(mainGridUid.Value, mainGridComp);
        var mapUid = Transform(stationGrid.Owner).MapUid;

        // Gather all non-empty tiles and sample up to triesBase of them
        var allTiles = _map.GetAllTiles(stationGrid.Owner, stationGrid.Comp, ignoreEmpty: true).ToList();
        if (allTiles.Count == 0)
            return null;

        var indices = Enumerable.Range(0, allTiles.Count).ToArray();
        var maxSamples = Math.Min(triesBase, allTiles.Count);

        MapCoordinates targetCoords = default;
        var foundValid = false;

        for (var i = 0; i < maxSamples; i++)
        {
            // Partial Fisher-Yates — only shuffle as far as we need
            var swapIdx = _random.Next(i, indices.Length);
            (indices[i], indices[swapIdx]) = (indices[swapIdx], indices[i]);

            var tile = allTiles[indices[i]];
            var tileIndex = tile.GridIndices;

            // Reject open-space tiles
            if (_atmosphere.IsTileSpace(stationGrid.Owner, mapUid, tileIndex))
                continue;

            // Reject tiles with no breathable air (inside walls, sealed rooms, etc.)
            if (_atmosphere.IsTileAirBlockedCached(stationGrid.Owner, tileIndex))
                continue;

            // Reject tiles with solid impassable anchored objects
            var blocked = false;
            foreach (var anchored in _map.GetAnchoredEntities(stationGrid.Owner, stationGrid.Comp, tileIndex))
            {
                if (!_physicsQuery.TryGetComponent(anchored, out var body))
                    continue;
                if (body.BodyType == BodyType.Static && body.Hard &&
                    (body.CollisionLayer & (int) CollisionGroup.Impassable) != 0)
                {
                    blocked = true;
                    break;
                }
            }

            if (blocked)
                continue;

            targetCoords = _map.GridTileToWorld(stationGrid.Owner, stationGrid.Comp, tileIndex);
            foundValid = true;
            break;
        }

        if (!foundValid)
            return null;

        // Drop any pulls before moving so we don't drag someone along
        _pulling.StopAllPulls(uid);

        var newPos = targetCoords.Position;
        _xform.SetWorldPosition(uid, newPos);

        if (teleportPulledEntities && TryComp<PullerComponent>(uid, out var puller) && puller.Pulling is { } pulled)
        {
            _xform.SetWorldPosition(pulled, newPos);
            _pulling.TryStartPull(uid, pulled, force: true);
        }

        return newPos;
    }
}
