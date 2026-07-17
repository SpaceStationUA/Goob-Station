using Content.Server.Shuttles.Components;
using Content.Server._Pirate.ZLevels.Core.Components;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared._Pirate.ZLevels.Core.Components;
using Content.Shared.Station.Components;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;

namespace Content.Server._Pirate.ZLevels.Spawning;

/// <summary>
/// Resolves the station floor grids used by multiz spawners.
/// </summary>
public sealed class CEZLevelFloorGridsSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly StationSystem _station = default!;

    /// <summary>
    /// Returns the configured external-space spawn floor for a multiz station.
    /// Falls back to the station's normal main-grid selection for non-multiz stations or invalid depths.
    /// </summary>
    public EntityUid? GetStationDefaultGrid(EntityUid station)
    {
        if (!TryComp<StationDataComponent>(station, out var data))
            return null;

        if (TryComp<CEStationZLevelsComponent>(station, out var zLevels))
        {
            foreach (var grid in data.Grids)
            {
                if (TryComp<CEZLinkedGridComponent>(grid, out var linked) &&
                    linked.Depth == zLevels.DefaultSpawnDepth)
                {
                    return grid;
                }
            }
        }

        foreach (var grid in data.Grids)
        {
            if (HasComp<BecomesStationComponent>(grid))
                return grid;
        }

        return _station.GetLargestGrid((station, data));
    }

    /// <summary>
    /// Returns the station's main grid plus linked z-peer grids.
    /// </summary>
    public List<EntityUid> GetStationFloorGrids(EntityUid station)
    {
        if (!TryComp<StationDataComponent>(station, out var data))
            return new List<EntityUid>();

        // Any linked station grid can resolve the full z-network.
        foreach (var grid in data.Grids)
        {
            if (HasComp<CEZLinkedGridComponent>(grid))
                return GetFloorGrids(grid);
        }

        var main = GetStationDefaultGrid(station);
        return main is { } mainGrid ? GetFloorGrids(mainGrid) : new List<EntityUid>();
    }

    /// <summary>
    /// Returns <paramref name="mainGrid"/> plus its z-peer floor grids.
    /// </summary>
    public List<EntityUid> GetFloorGrids(EntityUid mainGrid)
    {
        var grids = new List<EntityUid> { mainGrid };
        if (!TryComp<CEZLinkedGridComponent>(mainGrid, out var linked))
            return grids;

        foreach (var peer in linked.PeerGrids.Values)
        {
            if (peer != mainGrid)
                grids.Add(peer);
        }

        return grids;
    }

    /// <summary>
    /// Finds a station floor with a matching priority dock.
    /// </summary>
    public EntityUid? FindStationFloorWithPriorityDock(EntityUid station, string? tag)
    {
        if (tag == null)
            return null;

        var floors = GetStationFloorGrids(station);
        if (floors.Count == 0)
            return null;

        var query = EntityQueryEnumerator<PriorityDockComponent, TransformComponent>();
        while (query.MoveNext(out _, out var priority, out var xform))
        {
            if (priority.Tag != tag || xform.GridUid is not { } grid)
                continue;

            if (floors.Contains(grid))
                return grid;
        }

        return null;
    }

    /// <summary>
    /// Returns a random z-peer floor grid weighted by grid area.
    /// </summary>
    public EntityUid GetRandomFloorGrid(EntityUid anyFloorGrid)
    {
        var floors = GetFloorGrids(anyFloorGrid);
        if (floors.Count <= 1)
            return anyFloorGrid;

        var weighted = new List<(EntityUid Grid, float Cumulative)>(floors.Count);
        var total = 0f;
        foreach (var grid in floors)
        {
            if (!TryComp<MapGridComponent>(grid, out var gridComp))
                continue;

            var area = gridComp.LocalAABB.Width * gridComp.LocalAABB.Height;
            if (area <= 0f)
                continue;

            total += area;
            weighted.Add((grid, total));
        }

        if (total <= 0f)
            return anyFloorGrid;

        var roll = _random.NextFloat() * total;
        foreach (var (grid, cumulative) in weighted)
        {
            if (roll <= cumulative)
                return grid;
        }

        return anyFloorGrid;
    }
}
