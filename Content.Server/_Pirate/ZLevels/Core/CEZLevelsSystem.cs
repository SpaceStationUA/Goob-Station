/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Server._Pirate.ZLevels.Core.Components;
using Content.Server._Pirate.ZLevels.Shuttles;
using Content.Server.GameTicking.Rules;
using Content.Shared.Station.Components;
using Content.Server.Station.Events;
using Content.Server.Station.Systems;
using Content.Server.Shuttles.Systems;
using Content.Shared._Pirate.ZLevels.Core.EntitySystems;
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization.Systems;

namespace Content.Server._Pirate.ZLevels.Core;

public sealed partial class CEZLevelsSystem : CESharedZLevelsSystem
{
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly CEZShuttleRoofSystem _shuttleRoof = default!;
    private bool _serverInitialized;

    public override void Initialize()
    {
        if (_serverInitialized)
            return;

        base.Initialize();
        _serverInitialized = true;
        InitView();
        InitGridSync();
        InitItems(); // Pirate: multiz
        InitTransitionBudget();

        // Run before round-start variation so floor grids are linked before it spreads across them.
        SubscribeLocalEvent<CEStationZLevelsComponent, StationPostInitEvent>(OnStationPostInit,
            before:
            [
                typeof(RoundstartStationVariationRuleSystem),
                typeof(ShuttleSystem),
                typeof(ArrivalsSystem),
                typeof(EmergencyShuttleSystem),
            ]);
    }

    private void OnStationPostInit(Entity<CEStationZLevelsComponent> ent, ref StationPostInitEvent args)
    {
        if (ent.Comp.MapsAbove.Count == 0 && ent.Comp.MapsBelow.Count == 0)
            return;

        // Validate base map before creating the network so a missing base grid doesn't leak a network entity.
        EntityUid? mainMap = null;
        EntityUid? mainGrid = null;
        if (TryComp<StationDataComponent>(ent, out var stationData))
        {
            foreach (var grid in stationData.Grids)
            {
                if (Transform(grid).MapUid is not { } gridMap)
                    continue;

                mainMap = gridMap;
                mainGrid = grid;
                break;
            }
        }

        if (mainMap is null || mainGrid is null)
        {
            Log.Error($"Station {ToPrettyString(ent.Owner)} has no grids to base z-levels off of; skipping z-network setup.");
            return;
        }

        var stationName = MetaData(ent).EntityName;
        var stationNetwork = CreateZNetwork(ent.Comp.ZLevelsComponentOverrides);
        _meta.SetEntityName(stationNetwork, $"Station z-Network: {stationName}");

        Dictionary<EntityUid, int> dict = new();
        dict.Add(mainMap.Value, 0);

        var gridsByDepth = new Dictionary<int, EntityUid>
        {
            [0] = mainGrid.Value,
        };

        // Track maps loaded by this call (excludes the pre-existing mainMap) so we can roll them
        // back if z-network population fails.
        var loadedMaps = new List<EntityUid>();

        // Maps are deliberately left uninitialised here — see the comment before TryAddMapsIntoZNetwork.
        var depth = ent.Comp.MapsBelow.Count * -1;
        foreach (var mapBelow in ent.Comp.MapsBelow)
        {
            if (!_mapLoader.TryLoadMap(mapBelow, out var mapEnt, out var mapGrids)) // Pirate: multiz
            {
                Log.Error($"Failed to load map for Station zNetwork at depth {depth}!");
                depth++; // advance regardless so surviving maps keep their intended Z index
                continue;
            }

            Log.Info($"Created map {mapEnt.Value.Comp.MapId} for Station zNetwork at level {depth}");
            _meta.SetEntityName(mapEnt.Value, $"{stationName} [{depth}]");
            foreach (var grid in mapGrids!) // Pirate: multiz
            {
                _station.AddGridToStation(ent, grid);
                gridsByDepth.TryAdd(depth, grid);
            }
            dict.Add(mapEnt.Value, depth);
            loadedMaps.Add(mapEnt.Value);
            depth++;
        }

        depth = 1;
        foreach (var mapAbove in ent.Comp.MapsAbove)
        {
            if (!_mapLoader.TryLoadMap(mapAbove, out var mapEnt, out var mapGrids)) // Pirate: multiz
            {
                Log.Error($"Failed to load map for Station zNetwork at depth {depth}!");
                depth++; // advance regardless so surviving maps keep their intended Z index
                continue;
            }

            Log.Info($"Created map {mapEnt.Value.Comp.MapId} for Station zNetwork at level {depth}");
            _meta.SetEntityName(mapEnt.Value, $"{stationName} [{depth}]");
            foreach (var grid in mapGrids!) // Pirate: multiz
            {
                _station.AddGridToStation(ent, grid);
                gridsByDepth.TryAdd(depth, grid);
            }
            dict.Add(mapEnt.Value, depth);
            loadedMaps.Add(mapEnt.Value);
            depth++;
        }

        // Register every map (including the depth-0 main map) while all are still uninitialised.
        // OnAddedIntoZNetwork preemptively inits a map if any peer is already init — initialising
        // the above/below maps inline first would init the main map here too and trip GameTicker's
        // `DebugTools.Assert(!_map.IsInitialized(mapId))` in LoadMaps.
        if (!TryAddMapsIntoZNetwork(stationNetwork, dict))
        {
            Log.Error($"Failed to populate station z-network {ToPrettyString(stationNetwork)}; tearing it down.");
            QueueDel(stationNetwork);
            foreach (var loaded in loadedMaps)
                QueueDel(loaded);
            return;
        }

        ent.Comp.ZNetworkEntity = stationNetwork;
        LinkGridsDirectly(stationNetwork, gridsByDepth);

        // Init the additional maps now the network is linked. The depth-0 main map is intentionally
        // left uninitialised — GameTicker.LoadMaps asserts that and inits it itself before spawning.
        foreach (var loaded in loadedMaps)
            _map.InitializeMap(loaded);

        // Grid-fill shuttles may have landed before upper maps existed.
        _shuttleRoof.RebuildStationRoofs(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        UpdateView(frameTime);
        UpdateGridSync(frameTime);
        UpdateItems(frameTime);
    }
}
