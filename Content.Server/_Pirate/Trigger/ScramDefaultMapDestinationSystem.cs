using Content.Server.GameTicking;
using Content.Server.Station.Components;
using Content.Server._Pirate.ZLevels.Spawning;
using Content.Shared._Pirate.Trigger;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Station;
using Content.Shared.Station.Components;
using Content.Shared.Trigger.Components.Effects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Random;

namespace Content.Server._Pirate.Trigger;

public sealed class ScramDefaultMapDestinationSystem : EntitySystem
{
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly SharedStationSystem _station = default!;
    [Dependency] private readonly CEZLevelFloorGridsSystem _zFloors = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ScramOnTriggerComponent, ScramDefaultMapDestinationEvent>(OnDestinationQuery);
    }

    private void OnDestinationQuery(Entity<ScramOnTriggerComponent> ent, ref ScramDefaultMapDestinationEvent args)
    {
        var mapId = _ticker.DefaultMap;
        if (mapId == MapId.Nullspace || !_mapManager.MapExists(mapId) ||
            _station.GetStationInMap(mapId) is not { } station ||
            !TryComp<StationDataComponent>(station, out var stationData) ||
            !TryComp<PhysicsComponent>(args.Target, out var physics))
            return;

        var currentTile = _turf.GetTileRef(Transform(args.Target).Coordinates);
        var candidates = new List<TileRef>();
        var visitedGrids = new HashSet<EntityUid>();

        // BecomesStation identifies station floors without including docked shuttles in StationData.Grids.
        foreach (var stationGrid in stationData.Grids)
        {
            if (!HasComp<BecomesStationComponent>(stationGrid))
                continue;

            foreach (var gridUid in _zFloors.GetFloorGrids(stationGrid))
            {
                if (!visitedGrids.Add(gridUid) || Transform(gridUid).MapID != mapId ||
                    !TryComp<MapGridComponent>(gridUid, out var grid))
                    continue;

                foreach (var tile in _map.GetAllTiles(gridUid, grid))
                {
                    if (_turf.IsSpace(tile) ||
                        currentTile is { } current && tile.GridUid == current.GridUid && tile.GridIndices == current.GridIndices)
                        continue;

                    candidates.Add(tile);
                }
            }
        }

        _random.Shuffle(candidates);
        foreach (var tile in candidates)
        {
            if (_turf.IsTileBlocked(tile, (CollisionGroup) physics.CollisionMask))
                continue;

            args.Coordinates = _turf.GetTileCenter(tile);
            return;
        }
    }
}
