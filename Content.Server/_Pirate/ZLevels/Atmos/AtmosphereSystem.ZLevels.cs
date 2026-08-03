using Content.Server.Atmos.Components;
using Content.Shared._Pirate.ZLevels.Core.Components;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Maps;
using Robust.Shared.Map.Components;
using System.Numerics;

namespace Content.Server.Atmos.EntitySystems;

public sealed partial class AtmosphereSystem
{
    private readonly HashSet<EntityUid> _zAtmosLinkedMaps = new();
    private readonly HashSet<ZAtmosTilePair> _zAtmosVerticalSharePairs = new();
    private readonly HashSet<ZAtmosTilePair> _zAtmosVerticalTransferPairs = new();
    private readonly Dictionary<ZAtmosTileKey, int> _zAtmosVerticalTransferTileRefs = new();

    private readonly record struct ZAtmosTileKey(EntityUid GridUid, Vector2i Tile);

    private readonly record struct ZAtmosTilePair(ZAtmosTileKey A, ZAtmosTileKey B)
    {
        public static ZAtmosTilePair Create(EntityUid gridA, Vector2i tileA, EntityUid gridB, Vector2i tileB)
        {
            var keyA = new ZAtmosTileKey(gridA, tileA);
            var keyB = new ZAtmosTileKey(gridB, tileB);

            if (gridA.Id < gridB.Id)
                return new ZAtmosTilePair(keyA, keyB);

            if (gridA.Id > gridB.Id)
                return new ZAtmosTilePair(keyB, keyA);

            // Same grid: deterministic lexicographic tile order so the pair is canonical.
            var aFirst = tileA.X < tileB.X || (tileA.X == tileB.X && tileA.Y <= tileB.Y);
            return aFirst
                ? new ZAtmosTilePair(keyA, keyB)
                : new ZAtmosTilePair(keyB, keyA);
        }
    }

    private void InitializeZAtmos()
    {
        SubscribeLocalEvent<CEZLinkedGridComponent, ComponentStartup>(OnZLinkedGridStartup);
        SubscribeLocalEvent<CEZLinkedGridComponent, ComponentShutdown>(OnZLinkedGridShutdown);
        SubscribeLocalEvent<CEZLinkedGridComponent, EntParentChangedMessage>(OnZLinkedGridParentChanged);
    }

    private void RunZAtmosProcessing()
    {
        _zAtmosVerticalSharePairs.Clear();
    }

    private void OnZLinkedGridStartup(Entity<CEZLinkedGridComponent> ent, ref ComponentStartup args)
    {
        TrackZAtmosMap(ent.Owner);
        InvalidateZAtmosOpenings(ent.Owner);
    }

    private void OnZLinkedGridShutdown(Entity<CEZLinkedGridComponent> ent, ref ComponentShutdown args)
    {
        UntrackZAtmosMap(ent.Owner);

        // Mark surviving peer tiles that paired with us as invalid before dropping the candidates,
        // so any stale protected-air the disappearing grid was anchoring gets recomputed.
        InvalidateSurvivingZAtmosPeers(ent.Owner);

        RemoveZAtmosTransferCandidatesForGrid(ent.Owner);
    }

    private void InvalidateSurvivingZAtmosPeers(EntityUid removedGridUid)
    {
        if (_zAtmosVerticalTransferPairs.Count == 0)
            return;

        foreach (var pair in _zAtmosVerticalTransferPairs)
        {
            ZAtmosTileKey? survivor = null;
            if (pair.A.GridUid == removedGridUid && pair.B.GridUid != removedGridUid)
                survivor = pair.B;
            else if (pair.B.GridUid == removedGridUid && pair.A.GridUid != removedGridUid)
                survivor = pair.A;

            if (survivor is not { } key ||
                !TryComp<GridAtmosphereComponent>(key.GridUid, out var atmos))
            {
                continue;
            }

            InvalidateZAtmosTileAndNeighbors(atmos, key.Tile);
        }
    }

    private void OnZLinkedGridParentChanged(Entity<CEZLinkedGridComponent> ent, ref EntParentChangedMessage args)
    {
        // Only act on real map-to-map moves; ignore initial parenting / teardown where the parent didn't transition between maps.
        var newMapUid = Transform(ent.Owner).MapUid;
        if (args.OldMapId is not { } oldMapUid || oldMapUid == newMapUid)
            return;

        if (!HasOtherZAtmosGridOnMap(oldMapUid, ent.Owner))
            _zAtmosLinkedMaps.Remove(oldMapUid);

        // Vertical transfer pairs/refs were computed against the old projection; drop them and
        // let InvalidateZAtmosOpenings rebuild them on the new map.
        RemoveZAtmosTransferCandidatesForGrid(ent.Owner);

        TrackZAtmosMap(ent.Owner);
        InvalidateZAtmosOpenings(ent.Owner);
    }

    private void TrackZAtmosMap(EntityUid gridUid)
    {
        if (Transform(gridUid).MapUid is { } mapUid)
            _zAtmosLinkedMaps.Add(mapUid);
    }

    private void UntrackZAtmosMap(EntityUid gridUid)
    {
        if (Transform(gridUid).MapUid is not { } mapUid ||
            HasOtherZAtmosGridOnMap(mapUid, gridUid))
        {
            return;
        }

        _zAtmosLinkedMaps.Remove(mapUid);
    }

    private bool HasOtherZAtmosGridOnMap(EntityUid mapUid, EntityUid ignoredGridUid)
    {
        var query = EntityQueryEnumerator<CEZLinkedGridComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (uid != ignoredGridUid && xform.MapUid == mapUid)
                return true;
        }

        return false;
    }

    private bool MapSupportsZAtmos(Entity<TransformComponent?> entity, EntityUid? gridUid)
    {
        if (gridUid is { } grid && TryComp<CEZLinkedGridComponent>(grid, out _))
        {
            TrackZAtmosMap(grid);
            return true;
        }

        return entity.Comp?.MapUid is { } mapUid && _zAtmosLinkedMaps.Contains(mapUid);
    }

    private bool ShouldTryZLevelProtectedMixture(Entity<TransformComponent?> entity, EntityUid? gridUid, Vector2i gridTile)
    {
        if (!MapSupportsZAtmos(entity, gridUid))
            return false;

        if (gridUid == null || !gridUid.Value.IsValid())
            return true;

        if (TryComp<CEZPhysicsComponent>(entity.Owner, out var zPhysics) &&
            TryComp<CEZLinkedGridComponent>(gridUid.Value, out var linked) &&
            linked.Depth != zPhysics.CurrentZLevel)
        {
            return true;
        }

        return !TryComp<GridAtmosphereComponent>(gridUid.Value, out var atmos) ||
               !atmos.Tiles.ContainsKey(gridTile);
    }

    private bool TryGetZLevelProtectedTileMixtureForEntity(
        Entity<TransformComponent?> entity,
        bool excite,
        out GasMixture? mixture)
    {
        mixture = null;

        if (!Resolve(entity.Owner, ref entity.Comp))
            return false;

        if (!MapSupportsZAtmos(entity, entity.Comp.GridUid))
            return false;

        var worldPos = _transformSystem.GetWorldPosition(entity.Comp);
        var checkedGrids = new HashSet<EntityUid>();
        var targetDepth = TryComp<CEZPhysicsComponent>(entity.Owner, out var zPhysics)
            ? zPhysics.CurrentZLevel
            : (int?) null;
        var bestDepth = int.MinValue;
        EntityUid bestGridUid = EntityUid.Invalid;
        GridAtmosphereComponent? bestAtmos = null;
        GasTileOverlayComponent? bestOverlay = null;
        MapGridComponent? bestGrid = null;
        TransformComponent? bestXform = null;
        Vector2i bestTile = default;

        if (entity.Comp.GridUid is { } gridUid &&
            checkedGrids.Add(gridUid))
        {
            TrySelectZLevelProtectedTileMixtureCandidate(
                gridUid,
                worldPos,
                targetDepth,
                ref bestDepth,
                ref bestGridUid,
                ref bestAtmos,
                ref bestOverlay,
                ref bestGrid,
                ref bestXform,
                ref bestTile);
        }

        foreach (var grid in _mapManager.GetAllGrids(entity.Comp.MapID))
        {
            if (!checkedGrids.Add(grid.Owner))
                continue;

            TrySelectZLevelProtectedTileMixtureCandidate(
                grid.Owner,
                worldPos,
                targetDepth,
                ref bestDepth,
                ref bestGridUid,
                ref bestAtmos,
                ref bestOverlay,
                ref bestGrid,
                ref bestXform,
                ref bestTile);
        }

        if (!bestGridUid.IsValid() ||
            bestAtmos == null ||
            bestOverlay == null ||
            bestGrid == null ||
            bestXform == null)
        {
            return false;
        }

        var ent = new Entity<GridAtmosphereComponent, GasTileOverlayComponent, MapGridComponent, TransformComponent>(
            bestGridUid,
            bestAtmos,
            bestOverlay,
            bestGrid,
            bestXform);

        var tile = GetOrNewTile(bestGridUid, bestAtmos, bestTile);
        EnsureZLevelProtectedTileAir(ent, tile, GetVolumeForTiles(bestGrid));

        if (excite)
        {
            AddActiveTile(bestAtmos, tile);
            InvalidateVisuals((bestGridUid, bestOverlay), bestTile);
        }

        mixture = tile.Air;
        return true;
    }

    private void TrySelectZLevelProtectedTileMixtureCandidate(
        EntityUid gridUid,
        Vector2 worldPos,
        int? targetDepth,
        ref int bestDepth,
        ref EntityUid bestGridUid,
        ref GridAtmosphereComponent? bestAtmos,
        ref GasTileOverlayComponent? bestOverlay,
        ref MapGridComponent? bestGrid,
        ref TransformComponent? bestXform,
        ref Vector2i bestTile)
    {
        if (!TryComp<CEZLinkedGridComponent>(gridUid, out var linked) ||
            targetDepth is { } depth && linked.Depth != depth ||
            targetDepth == null && linked.Depth <= bestDepth ||
            !TryComp<GridAtmosphereComponent>(gridUid, out var atmos) ||
            !TryComp<GasTileOverlayComponent>(gridUid, out var overlay) ||
            !TryComp<MapGridComponent>(gridUid, out var grid) ||
            !TryComp<TransformComponent>(gridUid, out var xform))
        {
            return;
        }

        var gridTile = _mapSystem.WorldToTile(gridUid, grid, worldPos);
        var ent = new Entity<GridAtmosphereComponent, GasTileOverlayComponent, MapGridComponent, TransformComponent>(
            gridUid,
            atmos,
            overlay,
            grid,
            xform);

        if (!HasZLevelAtmosProtection(ent, gridTile))
            return;

        bestDepth = linked.Depth;
        bestGridUid = gridUid;
        bestAtmos = atmos;
        bestOverlay = overlay;
        bestGrid = grid;
        bestXform = xform;
        bestTile = gridTile;
    }

    private bool HasZLevelTileBelow(
        Entity<GridAtmosphereComponent, GasTileOverlayComponent, MapGridComponent, TransformComponent> ent,
        Vector2i gridTile)
    {
        return HasAnySolidZLevelTileBelow(ent.Owner, ent.Comp3, gridTile); // Pirate: multiz
    }

    private bool HasAnySolidZLevelTileBelow(EntityUid gridUid, MapGridComponent grid, Vector2i gridTile)
    {
        if (!TryComp<CEZLinkedGridComponent>(gridUid, out var linked))
            return false;

        var worldPos = _mapSystem.GridTileToWorldPos(gridUid, grid, gridTile);

        for (var depth = linked.Depth - 1; ; depth--)
        {
            if (!linked.PeerGrids.TryGetValue(depth, out var belowGridUid) ||
                !TryComp<MapGridComponent>(belowGridUid, out var belowGrid))
                break;

            var belowTile = _mapSystem.WorldToTile(belowGridUid, belowGrid, worldPos);
            if (_mapSystem.TryGetTile(belowGrid, belowTile, out var tile) && !tile.IsEmpty)
                return true;
        }

        return false;
    }

    private bool TryGetImmediateLevelBelow(
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i gridTile,
        out EntityUid belowGridUid,
        out MapGridComponent belowGrid,
        out Vector2i belowTile)
    {
        belowGridUid = EntityUid.Invalid;
        belowGrid = default!;
        belowTile = default;

        if (!TryComp<CEZLinkedGridComponent>(gridUid, out var linked) ||
            !linked.PeerGrids.TryGetValue(linked.Depth - 1, out belowGridUid) ||
            !TryComp<MapGridComponent>(belowGridUid, out var foundBelowGrid))
            return false;

        belowGrid = foundBelowGrid;
        var worldPos = _mapSystem.GridTileToWorldPos(gridUid, grid, gridTile);
        belowTile = _mapSystem.WorldToTile(belowGridUid, belowGrid, worldPos);
        return true;
    }

    private bool TryUpdateZLevelProtectedTileAir(
        Entity<GridAtmosphereComponent, GasTileOverlayComponent, MapGridComponent, TransformComponent> ent,
        TileAtmosphere tile,
        float volume)
    {
        if (!HasZLevelAtmosProtection(ent, tile.GridIndices))
            return false;

        EnsureZLevelProtectedTileAir(ent, tile, volume);
        return true;
    }

    private void EnsureZLevelProtectedTileAir(
        Entity<GridAtmosphereComponent, GasTileOverlayComponent, MapGridComponent, TransformComponent> ent,
        TileAtmosphere tile,
        float volume)
    {
        if (tile.Air is { TotalMoles: > Atmospherics.GasMinMoles })
            return;

        #region Pirate: multiz
        if (tile.Air?.Immutable == true)
            tile.Air = null; // GridFixTileVacuum asserts non-immutable; space air must be replaced
        #endregion

        tile.Air ??= new GasMixture(volume) { Temperature = Atmospherics.T20C };
        tile.Air.Clear();
        tile.Air.Temperature = Atmospherics.T20C;
        tile.AirArchived = null;
        tile.ArchivedCycle = 0;
        tile.LastShare = 0f;
        tile.Hotspot = new Hotspot();

        GridFixTileVacuum(tile);
        NotifyDeviceTileChanged((ent.Owner, ent.Comp1, ent.Comp3), tile.GridIndices);
    }

    private void RefreshZAtmosTransferCandidates(
        Entity<GridAtmosphereComponent, GasTileOverlayComponent, MapGridComponent, TransformComponent> ent,
        Vector2i gridTile)
    {
        if (!TryComp<CEZLinkedGridComponent>(ent.Owner, out var linked))
            return;

        if (TryGetZAtmosPeerTile(ent.Owner, ent.Comp3, linked, gridTile, -1, out var belowGridUid, out _, out var belowTile))
            RemoveZAtmosTransferCandidate(ent.Owner, gridTile, belowGridUid, belowTile);

        if (TryGetZAtmosPeerTile(ent.Owner, ent.Comp3, linked, gridTile, 1, out var aboveGridUid, out var aboveGrid, out var aboveTile))
            RemoveZAtmosTransferCandidate(aboveGridUid, aboveTile, ent.Owner, gridTile);

        if (TryGetZLevelAtmosProtectionBelow(ent, gridTile, out belowGridUid, out _, out belowTile))
            AddZAtmosTransferCandidate(ent.Owner, gridTile, belowGridUid, belowTile);

        if (!aboveGridUid.IsValid() ||
            aboveGrid == null ||
            !TryComp<GridAtmosphereComponent>(aboveGridUid, out var aboveAtmos) ||
            !TryComp<GasTileOverlayComponent>(aboveGridUid, out var aboveOverlay) ||
            !TryComp<TransformComponent>(aboveGridUid, out var aboveXform))
        {
            return;
        }

        var aboveEnt = new Entity<GridAtmosphereComponent, GasTileOverlayComponent, MapGridComponent, TransformComponent>(
            aboveGridUid,
            aboveAtmos,
            aboveOverlay,
            aboveGrid,
            aboveXform);

        if (HasZLevelAtmosProtection(aboveEnt, aboveTile))
            AddZAtmosTransferCandidate(aboveGridUid, aboveTile, ent.Owner, gridTile);
    }

    private bool IsZAtmosTransferCandidate(EntityUid gridUid, Vector2i gridTile)
    {
        return _zAtmosVerticalTransferTileRefs.ContainsKey(new ZAtmosTileKey(gridUid, gridTile));
    }

    private void ActivateZAtmosTransferCandidate(GridAtmosphereComponent atmos, TileAtmosphere tile)
    {
        if (IsZAtmosTransferCandidate(tile.GridIndex, tile.GridIndices))
            AddActiveTile(atmos, tile);
    }

    private void AddZAtmosTransferCandidate(EntityUid gridA, Vector2i tileA, EntityUid gridB, Vector2i tileB)
    {
        var pair = ZAtmosTilePair.Create(gridA, tileA, gridB, tileB);
        if (!_zAtmosVerticalTransferPairs.Add(pair))
            return;

        IncrementZAtmosTransferTileRef(pair.A);
        IncrementZAtmosTransferTileRef(pair.B);
    }

    private void RemoveZAtmosTransferCandidate(EntityUid gridA, Vector2i tileA, EntityUid gridB, Vector2i tileB)
    {
        var pair = ZAtmosTilePair.Create(gridA, tileA, gridB, tileB);
        if (!_zAtmosVerticalTransferPairs.Remove(pair))
            return;

        DecrementZAtmosTransferTileRef(pair.A);
        DecrementZAtmosTransferTileRef(pair.B);
    }

    private void RemoveZAtmosTransferCandidatesForGrid(EntityUid gridUid)
    {
        if (_zAtmosVerticalTransferPairs.Count == 0)
            return;

        var removed = new List<ZAtmosTilePair>();
        foreach (var pair in _zAtmosVerticalTransferPairs)
        {
            if (pair.A.GridUid == gridUid || pair.B.GridUid == gridUid)
                removed.Add(pair);
        }

        foreach (var pair in removed)
        {
            _zAtmosVerticalTransferPairs.Remove(pair);
            DecrementZAtmosTransferTileRef(pair.A);
            DecrementZAtmosTransferTileRef(pair.B);
        }
    }

    private void IncrementZAtmosTransferTileRef(ZAtmosTileKey key)
    {
        _zAtmosVerticalTransferTileRefs.TryGetValue(key, out var count);
        _zAtmosVerticalTransferTileRefs[key] = count + 1;
    }

    private void DecrementZAtmosTransferTileRef(ZAtmosTileKey key)
    {
        if (!_zAtmosVerticalTransferTileRefs.TryGetValue(key, out var count))
            return;

        if (count <= 1)
        {
            _zAtmosVerticalTransferTileRefs.Remove(key);
            return;
        }

        _zAtmosVerticalTransferTileRefs[key] = count - 1;
    }

    private void ShareZLevelAtmos(
        Entity<GridAtmosphereComponent, GasTileOverlayComponent, MapGridComponent, TransformComponent> ent,
        TileAtmosphere tile,
        int fireCount)
    {
        if (tile.Air == null ||
            !IsZAtmosTransferCandidate(ent.Owner, tile.GridIndices))
        {
            return;
        }

        TryShareZLevelAtmos(ent, tile, fireCount, 1);
        TryShareZLevelAtmos(ent, tile, fireCount, -1);
    }

    private void TryShareZLevelAtmos(
        Entity<GridAtmosphereComponent, GasTileOverlayComponent, MapGridComponent, TransformComponent> ent,
        TileAtmosphere tile,
        int fireCount,
        int depthOffset)
    {
        if (!TryGetZLevelAtmosTransferTile(ent, tile, depthOffset, out var otherEnt, out var otherTile))
            return;

        var pair = ZAtmosTilePair.Create(ent.Owner, tile.GridIndices, otherEnt.Owner, otherTile.GridIndices);
        if (!_zAtmosVerticalSharePairs.Add(pair))
            return;

        if (tile.ArchivedCycle < fireCount)
            Archive(tile, fireCount);

        if (otherTile.ArchivedCycle < fireCount)
            Archive(otherTile, fireCount);

        if (CompareExchange(tile, otherTile) == GasCompareResult.NoExchange)
            return;

        AddActiveTile(ent.Comp1, tile);
        AddActiveTile(otherEnt.Comp1, otherTile);

        // ExcitedGroups are tracked per-grid; can't span across linked grids without breaking
        // ExcitedGroupMerge's grid-locality assertion. Skip the group attachment here and accept
        // that LastShareCheck cooldown bookkeeping is a no-op on cross-grid z-shared tiles.

        Share(tile, otherTile, CountZLevelAtmosAdjacentTiles(tile));

        InvalidateVisuals(ent, tile);
        InvalidateVisuals(otherEnt, otherTile);
    }

    private bool TryGetZLevelAtmosTransferTile(
        Entity<GridAtmosphereComponent, GasTileOverlayComponent, MapGridComponent, TransformComponent> ent,
        TileAtmosphere tile,
        int depthOffset,
        out Entity<GridAtmosphereComponent, GasTileOverlayComponent, MapGridComponent, TransformComponent> otherEnt,
        out TileAtmosphere otherTile)
    {
        otherEnt = default;
        otherTile = default!;

        if (!TryComp<CEZLinkedGridComponent>(ent.Owner, out var linked) ||
            !linked.PeerGrids.TryGetValue(linked.Depth + depthOffset, out var otherGridUid) ||
            !TryComp<GridAtmosphereComponent>(otherGridUid, out var otherAtmos) ||
            !TryComp<GasTileOverlayComponent>(otherGridUid, out var otherOverlay) ||
            !TryComp<MapGridComponent>(otherGridUid, out var otherGrid) ||
            !TryComp<TransformComponent>(otherGridUid, out var otherXform))
        {
            return false;
        }

        var worldPos = _mapSystem.GridTileToWorldPos(ent.Owner, ent.Comp3, tile.GridIndices);
        var otherGridTile = _mapSystem.WorldToTile(otherGridUid, otherGrid, worldPos);
        otherEnt = new Entity<GridAtmosphereComponent, GasTileOverlayComponent, MapGridComponent, TransformComponent>(
            otherGridUid,
            otherAtmos,
            otherOverlay,
            otherGrid,
            otherXform);

        if (!_zAtmosVerticalTransferPairs.Contains(ZAtmosTilePair.Create(
                ent.Owner,
                tile.GridIndices,
                otherGridUid,
                otherGridTile)))
        {
            return false;
        }

        otherTile = GetOrUpdateZLevelAtmosTile(otherEnt, otherGridTile);

        return CanZLevelAtmosTransfer(tile) &&
               CanZLevelAtmosTransfer(otherTile);
    }

    private TileAtmosphere GetOrUpdateZLevelAtmosTile(
        Entity<GridAtmosphereComponent, GasTileOverlayComponent, MapGridComponent, TransformComponent> ent,
        Vector2i gridTile)
    {
        var tile = GetOrNewTile(ent.Owner, ent.Comp1, gridTile);

        if (tile.Air != null && !tile.MapAtmosphere)
            return tile;

        TryComp(ent.Comp4.MapUid, out MapAtmosphereComponent? mapAtmos);
        UpdateTileData(ent, mapAtmos, tile);
        UpdateAdjacentTiles(ent, tile, activate: true);
        UpdateTileAir(ent, tile, GetVolumeForTiles(ent.Comp3));

        return tile;
    }

    private bool CanZLevelAtmosTransfer(TileAtmosphere tile)
    {
        var data = tile.AirtightData;

        return tile.Air is { Immutable: false } &&
               !tile.Space &&
               !tile.MapAtmosphere &&
               (data.BlockedDirections != AtmosDirection.All || !data.NoAirWhenBlocked);
    }

    private static int CountZLevelAtmosAdjacentTiles(TileAtmosphere tile)
    {
        var adjacent = 1;

        for (var i = 0; i < Atmospherics.Directions; i++)
        {
            var direction = (AtmosDirection) (1 << i);
            if (tile.AdjacentBits.IsFlagSet(direction))
                adjacent++;
        }

        return adjacent;
    }

    private bool HasZLevelAtmosProtection(
        Entity<GridAtmosphereComponent, GasTileOverlayComponent, MapGridComponent, TransformComponent> ent,
        Vector2i gridTile)
    {
        return TryGetZLevelAtmosProtectionBelow(ent, gridTile, out _, out _, out _);
    }

    private bool TryGetZLevelAtmosProtectionBelow(
        Entity<GridAtmosphereComponent, GasTileOverlayComponent, MapGridComponent, TransformComponent> ent,
        Vector2i gridTile,
        out EntityUid belowGridUid,
        out MapGridComponent belowGrid,
        out Vector2i belowTile)
    {
        var hasGridTile = _map.TryGetTile(ent.Comp3, gridTile, out var gridTileRef) && !gridTileRef.IsEmpty;
        var isCandidate = !hasGridTile;
        belowGridUid = EntityUid.Invalid;
        belowGrid = default!;
        belowTile = default;

        if (hasGridTile)
        {
            var contentDef = (ContentTileDefinition) _tileDefinitionManager[gridTileRef.TypeId];
            isCandidate = contentDef.MapAtmosphere;
        }

        if (!isCandidate)
            return false;

        // Pirate: multiz - require a solid tile anywhere below (through consecutive holes),
        // then pair with the immediate level so gas chains level-by-level through the shaft
        if (!HasAnySolidZLevelTileBelow(ent.Owner, ent.Comp3, gridTile))
            return false;

        return TryGetImmediateLevelBelow(ent.Owner, ent.Comp3, gridTile, out belowGridUid, out belowGrid, out belowTile); // Pirate: multiz
    }

    private bool TryGetZAtmosPeerTile(
        EntityUid gridUid,
        MapGridComponent grid,
        CEZLinkedGridComponent linked,
        Vector2i gridTile,
        int depthOffset,
        out EntityUid peerGridUid,
        out MapGridComponent? peerGrid,
        out Vector2i peerTile)
    {
        peerGridUid = EntityUid.Invalid;
        peerGrid = null;
        peerTile = default;

        if (!linked.PeerGrids.TryGetValue(linked.Depth + depthOffset, out peerGridUid) ||
            !TryComp<MapGridComponent>(peerGridUid, out peerGrid))
        {
            return false;
        }

        var worldPos = _mapSystem.GridTileToWorldPos(gridUid, grid, gridTile);
        peerTile = _mapSystem.WorldToTile(peerGridUid, peerGrid, worldPos);
        return true;
    }

    private void InvalidateZAtmosOpenings(EntityUid gridUid)
    {
        if (!TryComp<MapGridComponent>(gridUid, out var grid) ||
            !TryComp<GridAtmosphereComponent>(gridUid, out var atmos))
        {
            return;
        }

        var enumerator = _mapSystem.GetAllTilesEnumerator(gridUid, grid);
        while (enumerator.MoveNext(out var tileRef))
        {
            InvalidateZAtmosTileAndNeighbors(atmos, tileRef.Value.GridIndices);
        }
    }

    private void InvalidateZAtmosPeers(Entity<MapGridComponent> changedGrid, Vector2i changedTile)
    {
        if (!TryComp<CEZLinkedGridComponent>(changedGrid.Owner, out var linked) ||
            !linked.PeerGrids.TryGetValue(linked.Depth + 1, out var aboveGridUid) ||
            !TryComp<MapGridComponent>(aboveGridUid, out var aboveGrid) ||
            !TryComp<GridAtmosphereComponent>(aboveGridUid, out var aboveAtmos))
        {
            return;
        }

        var worldPos = _mapSystem.GridTileToWorldPos(changedGrid.Owner, changedGrid.Comp, changedTile);
        var aboveTile = _mapSystem.WorldToTile(aboveGridUid, aboveGrid, worldPos);

        InvalidateZAtmosTileAndNeighbors(aboveAtmos, aboveTile);
    }

    private void InvalidateZAtmosTileAndNeighbors(GridAtmosphereComponent atmos, Vector2i tile)
    {
        atmos.InvalidatedCoords.Add(tile);

        for (var i = 0; i < Atmospherics.Directions; i++)
        {
            var direction = (AtmosDirection) (1 << i);
            atmos.InvalidatedCoords.Add(tile.Offset(direction));
        }
    }
}
