using System.Numerics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Map.Events;

namespace Content.Shared._Pirate.ZLevels.Core.EntitySystems;

/// <summary>
/// Public surface over <see cref="CMUZLevelOpeningCache"/>.
/// </summary>
public abstract partial class CESharedZLevelsSystem
{
    private void InitOpeningCache()
    {
        // Avoid retaining removed grid EntityUids.
        SubscribeLocalEvent<MapGridComponent, GridRemovalEvent>(OnGridRemovalForCache);
    }

    private void ShutdownOpeningCache()
    {
        _openingCache.Clear();
    }

    private void OnGridRemovalForCache(EntityUid uid, MapGridComponent comp, GridRemovalEvent args)
    {
        _openingCache.RemoveGrid(uid);
    }

    /// <summary>Invalidates the changed chunks for a grid.</summary>
    protected void InvalidateOpeningCache(Entity<MapGridComponent> grid, ReadOnlySpan<TileChangedEntry> changes)
    {
        _openingCache.InvalidateTiles(grid, changes);
    }

    /// <summary>True if the given map's primary grid (planet-style) has an opening tile at <paramref name="worldPos"/>.</summary>
    public bool IsOpeningAt(EntityUid mapUid, Vector2 worldPos)
    {
        if (!_gridQuery.TryComp(mapUid, out var grid))
            return true;

        return CMUZLevelOpeningCache.IsOpeningTile(mapUid, grid, worldPos, _map, TilDefMan);
    }

    /// <summary>
    /// Finds the nearest opening within <paramref name="radius"/>.
    /// </summary>
    public bool TryFindOpeningNear(EntityUid mapUid, Vector2 position, float radius, out Vector2 openingPosition)
    {
        openingPosition = default;

        // No grid on the map -> the map itself is one continuous opening (e.g. space).
        if (!_gridQuery.TryComp(mapUid, out _))
        {
            openingPosition = position;
            return true;
        }

        if (!_mapQuery.TryComp(mapUid, out var mapComp))
            return false;

        return _openingCache.TryFindNearestOpeningCenterNear(
            mapComp.MapId,
            position,
            radius,
            out openingPosition,
            _openingGridScratch,
            _mapMan,
            _map,
            _transform,
            TilDefMan,
            edgeOnly: false);
    }

    /// <summary>
    /// Checks whether a tile-bounded region contains any opening.
    /// </summary>
    public bool HasOpeningInTileBounds(Entity<MapGridComponent> grid, Vector2i start, Vector2i end)
    {
        return _openingCache.HasOpeningInTileBounds(grid, start, end, _map, TilDefMan);
    }

    /// <summary>
    /// Finds a nearby sound opening, excluding off-grid space.
    /// </summary>
    public bool TryFindRealOpeningNear(EntityUid mapUid, Vector2 position, float radius, out Vector2 openingPosition)
    {
        openingPosition = default;

        if (!_mapQuery.TryComp(mapUid, out var mapComp))
            return false;

        var bounds = Box2.CenteredAround(position, new Vector2(radius * 2f, radius * 2f));
        _openingGridScratch.Clear();
        var grids = _openingGridScratch;
        _mapMan.FindGridsIntersecting(mapComp.MapId, bounds, ref grids, approx: true, includeMap: true);

        if (grids.Count == 0)
            return false;

        var radiusSq = radius * radius;
        var bestDistSq = float.PositiveInfinity;
        var found = false;
        var tileRadius = (int) MathF.Ceiling(radius) + 1;

        foreach (var grid in grids)
        {
            var gridWorld = _transform.GetWorldMatrix(grid.Owner);
            if (!Matrix3x2.Invert(gridWorld, out var gridInv))
                continue;

            var local = Vector2.Transform(position, gridInv);
            var centerTile = new Vector2i((int) MathF.Floor(local.X), (int) MathF.Floor(local.Y));

            for (var dx = -tileRadius; dx <= tileRadius; dx++)
            {
                for (var dy = -tileRadius; dy <= tileRadius; dy++)
                {
                    var tile = centerTile + new Vector2i(dx, dy);
                    if (!CMUZLevelOpeningCache.IsSoundOpening(grid, tile, _map, TilDefMan))
                        continue;

                    var tileCenter = Vector2.Transform(
                        new Vector2(tile.X + 0.5f, tile.Y + 0.5f),
                        gridWorld);
                    var distSq = Vector2.DistanceSquared(position, tileCenter);
                    if (distSq > radiusSq || distSq >= bestDistSq)
                        continue;

                    bestDistSq = distSq;
                    openingPosition = tileCenter;
                    found = true;
                }
            }
        }

        return found;
    }

    /// <summary>
    /// Enumerates opening centers near <paramref name="sourcePosition"/>.
    /// </summary>
    public void FindOpeningCentersNear(
        MapId mapId,
        Vector2 sourcePosition,
        float radius,
        List<(Vector2 Center, float Distance)> openings,
        List<Entity<MapGridComponent>> gridScratch,
        bool edgeOnly = true)
    {
        _openingCache.FindOpeningCentersNear(
            mapId,
            sourcePosition,
            radius,
            openings,
            gridScratch,
            _mapMan,
            _map,
            _transform,
            TilDefMan,
            edgeOnly);
    }

    /// <summary>
    /// Finds the first shoot-through opening along the cross-Z shot line.
    /// </summary>
    /// <param name="preferOpeningAwayFromSource">Prefer a nearby opening that is not the shooter's tile.</param>
    /// <param name="maxSourceDistanceFromOpeningEdgeTiles">Maximum allowed source-to-opening distance in tiles.</param>
    public bool TryFindZShotOpening(
        EntityUid sourceMap,
        EntityUid targetMap,
        int offset,
        Vector2 from,
        Vector2 to,
        out EntityCoordinates opening,
        bool preferOpeningAwayFromSource = false,
        float maxSourceDistanceFromOpeningEdgeTiles = float.PositiveInfinity)
    {
        opening = default;
        if (offset == 0)
            return false;

        // The higher of the two maps carries the hole.
        var openingMapUid = offset < 0 ? sourceMap : targetMap;

        // Resolve via MapId so multi-grid maps work.
        if (!_mapQuery.TryComp(openingMapUid, out var openingMapComp))
            return false;

        // No grid at the shooter's XY means the column is unobstructed.
        if (!_mapMan.TryFindGridAt(openingMapComp.MapId, from, out var gridUidValue, out var grid))
        {
            opening = new EntityCoordinates(openingMapUid, from);
            return true;
        }

        var openingGrid = gridUidValue;
        // Keep the result grid-anchored for moving shuttles.
        var gridWorldMatrix = _transform.GetWorldMatrix(openingGrid);
        Matrix3x2.Invert(gridWorldMatrix, out var gridInvMatrix);
        var localFromForCmp = Vector2.Transform(from, gridInvMatrix);

        var sourceTile = preferOpeningAwayFromSource
            ? _map.WorldToTile(openingGrid, grid, from)
            : default;
        var fallbackOpeningLocal = Vector2.Zero;
        var hasFallbackOpening = false;
        var maxSourceDistanceFromOpeningCenter = float.IsPositiveInfinity(maxSourceDistanceFromOpeningEdgeTiles)
            ? float.PositiveInfinity
            : grid.TileSize * (0.5f + Math.Max(0f, maxSourceDistanceFromOpeningEdgeTiles));
        var maxSourceDistanceSquared = maxSourceDistanceFromOpeningCenter * maxSourceDistanceFromOpeningCenter;
        var selectedOpeningLocal = Vector2.Zero;

        bool TryUseOpeningTile(Vector2i tile)
        {
            var hasTileRef = _map.TryGetTileRef(openingGrid, grid, tile, out var tileRef);
            var tileIsOpening = !hasTileRef || CMUZLevelOpeningCache.IsShotOpening(tileRef.Tile, TilDefMan);

            if (hasTileRef && !tileIsOpening)
                return false;

            var openingCenterLocal = _map.ToCenterCoordinates(openingGrid, tile, grid).Position;
            var dist2 = Vector2.DistanceSquared(localFromForCmp, openingCenterLocal);
            if (dist2 > maxSourceDistanceSquared)
                return false;

            if (preferOpeningAwayFromSource &&
                tile == sourceTile)
            {
                if (!hasFallbackOpening)
                {
                    fallbackOpeningLocal = openingCenterLocal;
                    hasFallbackOpening = true;
                }

                return false;
            }

            selectedOpeningLocal = openingCenterLocal;
            return true;
        }

        var localFrom = _map.WorldToLocal(openingGrid, grid, from) / grid.TileSize;
        var localTo = _map.WorldToLocal(openingGrid, grid, to) / grid.TileSize;
        var localDelta = localTo - localFrom;
        var currentTile = new Vector2i((int) MathF.Floor(localFrom.X), (int) MathF.Floor(localFrom.Y));
        var endTile = new Vector2i((int) MathF.Floor(localTo.X), (int) MathF.Floor(localTo.Y));

        var stepX = Math.Sign(localDelta.X);
        var stepY = Math.Sign(localDelta.Y);
        var tDeltaX = stepX == 0 ? float.PositiveInfinity : MathF.Abs(1f / localDelta.X);
        var tDeltaY = stepY == 0 ? float.PositiveInfinity : MathF.Abs(1f / localDelta.Y);
        var nextBoundaryX = stepX > 0 ? currentTile.X + 1f : currentTile.X;
        var nextBoundaryY = stepY > 0 ? currentTile.Y + 1f : currentTile.Y;
        var tMaxX = stepX == 0 ? float.PositiveInfinity : (nextBoundaryX - localFrom.X) / localDelta.X;
        var tMaxY = stepY == 0 ? float.PositiveInfinity : (nextBoundaryY - localFrom.Y) / localDelta.Y;

        while (true)
        {
            if (TryUseOpeningTile(currentTile))
            {
                opening = new EntityCoordinates(openingGrid, selectedOpeningLocal);
                return true;
            }

            if (currentTile == endTile)
                break;

            if (tMaxX < tMaxY)
            {
                currentTile += new Vector2i(stepX, 0);
                tMaxX += tDeltaX;
            }
            else if (tMaxY < tMaxX)
            {
                currentTile += new Vector2i(0, stepY);
                tMaxY += tDeltaY;
            }
            else
            {
                // Supercover corner crossings so diagonal shots do not skip adjacent openings.
                var neighborX = currentTile + new Vector2i(stepX, 0);
                var neighborY = currentTile + new Vector2i(0, stepY);
                var neighborXInRange = stepX != 0 && (stepX > 0 ? neighborX.X <= endTile.X : neighborX.X >= endTile.X);
                var neighborYInRange = stepY != 0 && (stepY > 0 ? neighborY.Y <= endTile.Y : neighborY.Y >= endTile.Y);

                if ((neighborXInRange && TryUseOpeningTile(neighborX)) ||
                    (neighborYInRange && TryUseOpeningTile(neighborY)))
                {
                    opening = new EntityCoordinates(openingGrid, selectedOpeningLocal);
                    return true;
                }

                currentTile += new Vector2i(stepX, stepY);
                tMaxX += tDeltaX;
                tMaxY += tDeltaY;
            }
        }

        if (hasFallbackOpening)
        {
            opening = new EntityCoordinates(openingGrid, fallbackOpeningLocal);
            return true;
        }

        return false;
    }
}
