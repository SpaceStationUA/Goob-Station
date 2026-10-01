// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared._Pirate.ZLevels.Core.Components;
using Robust.Shared.Map.Components;

namespace Content.Server._Pirate.ZLevels.Core;

public sealed partial class CEZLevelsSystem
{
    // Cache membership in query order, while reading bounds, transforms and tiles live. The spatial query
    // skips chunkless holes inside grid bounds, which this resolver still accepts as its final fallback.
    private readonly Dictionary<EntityUid, List<Entity<MapGridComponent, TransformComponent>>> _lookupGrids = new();
    private readonly List<EntityUid> _emptyLookupMaps = new();
    private readonly HashSet<EntityUid> _lookupAncestors = new();
    private bool _lookupGridsDirty = true;

    private void InitializeGridLookup()
    {
        EntityManager.ComponentAdded += OnLookupComponentAdded;
        EntityManager.ComponentRemoved += OnLookupComponentRemoved;
        SubscribeLocalEvent<EntParentChangedMessage>(OnLookupParentChanged);
        SubscribeLocalEvent<MapGridComponent, EntityPausedEvent>(OnLookupGridPaused);
        SubscribeLocalEvent<MapGridComponent, EntityUnpausedEvent>(OnLookupGridUnpaused);
    }

    private void OnLookupComponentAdded(AddedComponentEventArgs args)
    {
        if (args.BaseArgs.Component is MapGridComponent)
            _lookupGridsDirty = true;
    }

    private void OnLookupComponentRemoved(RemovedComponentEventArgs args)
    {
        if (args.BaseArgs.Component is MapGridComponent)
            _lookupGridsDirty = true;
    }

    // The shared resolver's query skips paused grids.
    private void OnLookupGridPaused(Entity<MapGridComponent> ent, ref EntityPausedEvent args) => _lookupGridsDirty = true;
    private void OnLookupGridUnpaused(Entity<MapGridComponent> ent, ref EntityUnpausedEvent args) => _lookupGridsDirty = true;

    // Moving a grid's ancestor can change its map without changing the grid's direct parent.
    private void OnLookupParentChanged(ref EntParentChangedMessage args)
    {
        if (_lookupAncestors.Contains(args.Entity))
            _lookupGridsDirty = true;
    }

    protected override void OnGridParentChanged(Entity<MapGridComponent> ent, ref EntParentChangedMessage args)
    {
        // Invalidate before the inherited handler can resolve support for attached bodies.
        _lookupGridsDirty = true;
        base.OnGridParentChanged(ent, ref args);
    }

    private void RebuildLookupGrids()
    {
        foreach (var list in _lookupGrids.Values)
            list.Clear();
        _lookupAncestors.Clear();
        var query = EntityQueryEnumerator<MapGridComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var grid, out var xform))
        {
            var parent = xform.ParentUid;
            while (parent.IsValid() && TryComp<TransformComponent>(parent, out var parentXform))
            {
                if (!_lookupAncestors.Add(parent))
                    break;
                parent = parentXform.ParentUid;
            }

            if (xform.MapUid is not { } mapUid)
                continue;
            if (!_lookupGrids.TryGetValue(mapUid, out var list))
                _lookupGrids[mapUid] = list = new();
            // Keep the query order: it breaks equal-area ties in the shared resolver.
            list.Add((uid, grid, xform));
        }
        _emptyLookupMaps.Clear();
        foreach (var (map, list) in _lookupGrids)
        {
            if (list.Count == 0)
                _emptyLookupMaps.Add(map);
        }
        foreach (var map in _emptyLookupMaps)
            _lookupGrids.Remove(map);
        _lookupGridsDirty = false;
    }

    protected override bool TryAttachToCarrierGrid(EntityUid ent, CEZPhysicsComponent zPhys, ref TransformComponent xform)
    {
        // Keep the shared early-outs ahead of indexed carrier searches; stair debugging keeps the full path.
        if (!ZDebugStairsEnabled &&
            (xform.GridUid != null || zPhys.CurrentGroundFromBelowLevel || zPhys.LocalPosition < 0f ||
             !ShouldStayAttachedToCarrierGrid(zPhys)))
            return false;

        return base.TryAttachToCarrierGrid(ent, zPhys, ref xform);
    }

    // Query order defines the first grid, regardless of its size or position.
    protected override bool TryResolveAnyGridOnMap(EntityUid mapUid, out EntityUid gridUid, out MapGridComponent gridComp)
    {
        if (_lookupGridsDirty)
            RebuildLookupGrids();
        if (_lookupGrids.TryGetValue(mapUid, out var grids))
        {
            (gridUid, gridComp, _) = grids[0];
            return true;
        }

        if (TryComp<MapGridComponent>(mapUid, out var mapAsGrid))
        {
            gridUid = mapUid;
            gridComp = mapAsGrid;
            return true;
        }

        gridUid = EntityUid.Invalid;
        gridComp = default!;
        return false;
    }

    protected override bool TryResolveGridAtWorldPositionOnMap(EntityUid mapUid, Vector2 worldPos,
        out EntityUid gridUid, out MapGridComponent gridComp)
    {
        if (_lookupGridsDirty)
            RebuildLookupGrids();
        gridUid = EntityUid.Invalid;
        gridComp = default!;
        if (!_lookupGrids.TryGetValue(mapUid, out var grids))
            return false;

        // Strict area comparisons preserve query order when candidate areas tie.
        EntityUid nonEmptyUid = EntityUid.Invalid, tileUid = EntityUid.Invalid, boundsUid = EntityUid.Invalid;
        MapGridComponent? nonEmptyGrid = null, tileGrid = null, boundsGrid = null;
        var nonEmptyArea = float.MaxValue;
        var tileArea = float.MaxValue;
        var boundsArea = float.MaxValue;
        foreach (var (uid, grid, xform) in grids)
        {
            var (pos, rot) = _transform.GetWorldPositionRotation(xform);
            var bounds = new Box2Rotated(grid.LocalAABB.Translated(pos), rot, pos).CalcBoundingBox();
            if (!bounds.Contains(worldPos))
                continue;
            var area = bounds.Size.X * bounds.Size.Y;
            if (_map.TryGetTileRef(uid, grid, worldPos, out var tile))
            {
                if (!tile.Tile.IsEmpty && area < nonEmptyArea)
                {
                    nonEmptyArea = area;
                    nonEmptyUid = uid;
                    nonEmptyGrid = grid;
                }
                else if (area < tileArea)
                {
                    tileArea = area;
                    tileUid = uid;
                    tileGrid = grid;
                }
            }
            if (area < boundsArea)
            {
                boundsArea = area;
                boundsUid = uid;
                boundsGrid = grid;
            }
        }
        if (nonEmptyGrid != null)
            (gridUid, gridComp) = (nonEmptyUid, nonEmptyGrid);
        else if (tileGrid != null)
            (gridUid, gridComp) = (tileUid, tileGrid);
        else if (boundsGrid != null)
            (gridUid, gridComp) = (boundsUid, boundsGrid);
        return gridUid != EntityUid.Invalid;
    }

    public override void Shutdown()
    {
        EntityManager.ComponentAdded -= OnLookupComponentAdded;
        EntityManager.ComponentRemoved -= OnLookupComponentRemoved;
        _lookupGrids.Clear();
        _lookupAncestors.Clear();
        base.Shutdown();
    }
}
