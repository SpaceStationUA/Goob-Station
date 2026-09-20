using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared._Pirate.CCVars;
using Content.Shared.GameTicking;
using Content.Shared.Tag;
using Robust.Shared.Configuration;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Pirate.Server.Shipyard;

/// <summary>Loads temporary vessel grids and docks them to a station grid.</summary>
public sealed class ShipyardSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly MapDeleterShuttleSystem _mapDeleterShuttle = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly ShuttleSystem _shuttle = default!;

    private readonly HashSet<MapId> _shipyardMaps = new();

    public ProtoId<TagPrototype> DockTag = "DockShipyard";
    public bool Enabled;

    public override void Initialize()
    {
        base.Initialize();
        Subs.CVar(_config, PirateVars.Shipyard, value => Enabled = value, true);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ =>
        {
            foreach (var mapId in new List<MapId>(_shipyardMaps))
            {
                if (_map.MapExists(mapId))
                    _map.DeleteMap(mapId);
            }

            _shipyardMaps.Clear();
        });
    }

    public bool TryCreateShuttle(ResPath path, [NotNullWhen(true)] out Entity<ShuttleComponent>? shuttle)
    {
        shuttle = null;
        if (!Enabled)
            return false;

        var map = _map.CreateMap(out var mapId);
        _shipyardMaps.Add(mapId);
        if (!_mapLoader.TryLoadGrid(mapId, path, out var grid))
        {
            Log.Error($"Failed to load shipyard vessel {path}");
            _map.DeleteMap(mapId);
            _shipyardMaps.Remove(mapId);
            return false;
        }

        var gridUid = grid.Value.Owner;
        if (!TryComp<ShuttleComponent>(gridUid, out var comp))
        {
            Log.Error($"Shipyard vessel {path} grid was missing ShuttleComponent");
            _map.DeleteMap(mapId);
            _shipyardMaps.Remove(mapId);
            return false;
        }

        _map.SetPaused(map, false);
        _mapDeleterShuttle.Enable(gridUid, map);
        shuttle = (gridUid, comp);
        return true;
    }

    public bool TrySendShuttle(EntityUid destinationGrid, ResPath path, int delay,
        [NotNullWhen(true)] out Entity<ShuttleComponent>? shuttle, Action? onFailure = null)
    {
        shuttle = null;
        if (!TryComp<MapGridComponent>(destinationGrid, out _))
            return false;

        if (!TryCreateShuttle(path, out shuttle))
            return false;

        var shuttleUid = shuttle.Value.Owner;
        var sourceMapId = Transform(shuttleUid).MapID;
        var sourceMapUid = _map.GetMap(sourceMapId);
        var failureHandled = false;
        void HandleFailure()
        {
            if (failureHandled)
                return;

            failureHandled = true;
            _mapDeleterShuttle.Disable(shuttleUid);
            _mapDeleterShuttle.DeleteOwnedMap(sourceMapUid);
            _shipyardMaps.Remove(sourceMapId);
            onFailure?.Invoke();
        }

        void HandleFtlFailure()
        {
            if (failureHandled)
                return;

            failureHandled = true;
            _mapDeleterShuttle.Disable(shuttleUid);
            _shipyardMaps.Remove(sourceMapId);
            onFailure?.Invoke();
            Timer.Spawn(TimeSpan.Zero, () => _mapDeleterShuttle.DeleteOwnedMap(sourceMapUid));
        }

        void HandleTermination()
        {
            if (failureHandled)
                return;

            failureHandled = true;
            _shipyardMaps.Remove(sourceMapId);
            onFailure?.Invoke();
        }

        _mapDeleterShuttle.SetFailureCallback(shuttleUid, HandleFtlFailure);
        _mapDeleterShuttle.SetTerminationCallback(shuttleUid, HandleTermination);
        _mapDeleterShuttle.SetCompletionCallback(shuttleUid, () => _shipyardMaps.Remove(sourceMapId));

        var expectedDestinationMap = Transform(destinationGrid).MapUid;
        if (expectedDestinationMap is not { } destinationMap)
        {
            HandleFailure();
            return false;
        }

        bool DockShuttle()
        {
            if (!Exists(shuttleUid) || !TryComp<ShuttleComponent>(shuttleUid, out var shuttleComp) ||
                !Exists(destinationGrid) || !HasComp<MapGridComponent>(destinationGrid))
            {
                HandleFailure();
                return false;
            }

            _mapDeleterShuttle.SetExpectedMap(shuttleUid, destinationMap);
            if (!_shuttle.FTLToDock(shuttleUid, shuttleComp, destinationGrid, priorityTag: DockTag))
            {
                HandleFtlFailure();
                return false;
            }

            return true;
        }

        if (delay <= 0)
            return DockShuttle();

        Timer.Spawn(TimeSpan.FromSeconds(delay), () => DockShuttle());
        return true;

    }

}
