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

    public bool TryCreateShuttle(ResPath path, EntityUid expectedMap, [NotNullWhen(true)] out Entity<ShuttleComponent>? shuttle)
    {
        shuttle = null;
        if (!Enabled || !expectedMap.IsValid() || TerminatingOrDeleted(expectedMap))
            return false;

        var expectedTransform = Transform(expectedMap);
        if (expectedTransform.MapID == MapId.Nullspace ||
            !_map.MapExists(expectedTransform.MapID) || _map.GetMap(expectedTransform.MapID) != expectedMap)
        {
            return false;
        }

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
        _mapDeleterShuttle.Enable(gridUid, map, expectedMap);
        shuttle = (gridUid, comp);
        return true;
    }

    public bool TrySendShuttle(EntityUid destinationGrid, ResPath path, int delay,
        [NotNullWhen(true)] out Entity<ShuttleComponent>? shuttle, Action? onFailure = null)
    {
        shuttle = null;
        if (!TryComp<MapGridComponent>(destinationGrid, out _) || TerminatingOrDeleted(destinationGrid))
            return false;

        var destinationTransform = Transform(destinationGrid);
        if (destinationTransform.MapID == MapId.Nullspace || destinationTransform.MapUid is not { } destinationMap ||
            !_map.MapExists(destinationTransform.MapID))
        {
            return false;
        }

        if (!TryCreateShuttle(path, destinationMap, out shuttle))
            return false;

        var shuttleUid = shuttle.Value.Owner;
        var sourceMapId = Transform(shuttleUid).MapID;
        var sourceMapUid = _map.GetMap(sourceMapId);
        var failureHandled = false;

        void HandlePreJumpFailure()
        {
            if (failureHandled)
                return;

            failureHandled = true;
            _mapDeleterShuttle.Disable(shuttleUid);
            _mapDeleterShuttle.DeleteOwnedMap(sourceMapUid);
            _shipyardMaps.Remove(sourceMapId);
            onFailure?.Invoke();
        }
        void HandleTerminalFailure()
        {
            if (failureHandled)
                return;

            failureHandled = true;
            _mapDeleterShuttle.DeleteOwnedMap(sourceMapUid);
            _shipyardMaps.Remove(sourceMapId);
            onFailure?.Invoke();
        }

        void HandleTermination()
        {
            if (failureHandled)
                return;

            failureHandled = true;
            onFailure?.Invoke();
        }
        void HandleCompletion()
        {
            _shipyardMaps.Remove(sourceMapId);
        }

        _mapDeleterShuttle.SetCallbacks(shuttleUid, HandleTerminalFailure, HandleCompletion, HandleTermination);

        bool DockShuttle()
        {
            if (!_mapDeleterShuttle.IsPending(shuttleUid))
                return false;

            if (TerminatingOrDeleted(shuttleUid) || TerminatingOrDeleted(destinationGrid) ||
                !TryComp<ShuttleComponent>(shuttleUid, out var shuttleComp) ||
                !TryComp<MapGridComponent>(destinationGrid, out _) ||
                TerminatingOrDeleted(sourceMapUid) ||
                !TryComp<TransformComponent>(sourceMapUid, out var sourceTransform) ||
                sourceTransform.MapID == MapId.Nullspace ||
                !_map.MapExists(sourceTransform.MapID) ||
                _map.GetMap(sourceTransform.MapID) != sourceMapUid)
            {
                if (!_mapDeleterShuttle.IsArmed(shuttleUid))
                    HandlePreJumpFailure();
                return false;
            }

            if (!_shuttle.FTLToDock(shuttleUid, shuttleComp, destinationGrid, priorityTag: DockTag))
            {
                HandlePreJumpFailure();
                return false;
            }

            switch (_mapDeleterShuttle.Arm(shuttleUid))
            {
                case MapDeleterShuttleSystem.ArmStatus.Armed:
                    return true;
                case MapDeleterShuttleSystem.ArmStatus.AlreadyHandled:
                    // Completion or termination atomically consumed the pending state before Arm;
                    // its callback already owns cleanup/refund handling.
                    Log.Debug($"Shipyard shuttle {ToPrettyString(shuttleUid)} cleanup state was already handled before arming.");
                    return true;
                case MapDeleterShuttleSystem.ArmStatus.Missing:
                    if (!_mapDeleterShuttle.RestoreAndArm(shuttleUid, sourceMapUid, destinationMap,
                            HandleTerminalFailure, HandleCompletion, HandleTermination))
                    {
                        Log.Error($"Shipyard shuttle {ToPrettyString(shuttleUid)} could not recover missing cleanup state after starting FTL.");
                    }

                    // FTLToDock accepted the jump. Never use the pre-jump failure path here.
                    return true;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        if (delay <= 0)
            return DockShuttle();

        Timer.Spawn(TimeSpan.FromSeconds(delay), () => DockShuttle());
        return true;
    }
}
