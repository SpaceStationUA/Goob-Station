// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Pirate.ZLevels.Spawning;
using Content.Server._Pirate.ZLevels.View;
using Content.Server.Actions;
using Content.Server.Pinpointer;
using Content.Server.Power.Components;
using Content.Server.Station.Systems;
using Content.Shared._Pirate.SurveillanceCamera;
using Content.Shared._Shitmed.Antags.Abductor;
using Content.Shared.Eye;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Pinpointer;
using Content.Shared.Power;
using Content.Shared.Silicons.StationAi;
using Content.Shared.UserInterface;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using System.Linq;

namespace Content.Server._Pirate.SurveillanceCamera;

/// <summary>Opens a movable, camera-coverage-limited eye at a selected station beacon.</summary>
public sealed class RemoteStationEyeMonitorSystem : EntitySystem
{
    private static readonly EntProtoId EyePrototype = "SyndicateRemoteSurveillanceEye";
    private static readonly EntProtoId ExitAction = "ActionRemoteStationEyeExit";
    private static readonly EntProtoId ViewUpAction = "ActionStationAiViewUp";
    private static readonly EntProtoId ViewDownAction = "ActionStationAiViewDown";

    [Dependency] private readonly StationSystem _stations = default!;
    [Dependency] private readonly CEZLevelFloorGridsSystem _floors = default!;
    [Dependency] private readonly CEZLevelEyeSystem _zEye = default!;
    [Dependency] private readonly NavMapSystem _navMap = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedEyeSystem _eyes = default!;
    [Dependency] private readonly SharedMoverController _mover = default!;
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RemoteStationEyeMonitorComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
        SubscribeLocalEvent<RemoteStationEyeMonitorComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<RemoteStationEyeMonitorComponent, ComponentShutdown>(OnMonitorShutdown);
        SubscribeLocalEvent<RemoteStationEyeViewerComponent, ComponentShutdown>(OnViewerShutdown);
        SubscribeLocalEvent<RemoteStationEyeExitEvent>(OnExit);
        Subs.BuiEvents<RemoteStationEyeMonitorComponent>(AbductorCameraConsoleUIKey.Key,
            subs => subs.Event<AbductorBeaconChosenBuiMsg>(OnBeaconChosen));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<RemoteStationEyeViewerComponent>();
        while (query.MoveNext(out var viewer, out var state))
        {
            if (!Exists(state.Monitor) || !Exists(state.Eye) || !IsPowered(state.Monitor) ||
                !TryComp<EyeComponent>(viewer, out var eye) || eye.Target != state.Eye)
                StopViewing(viewer);
        }
    }

    private void OnGetVerbs(Entity<RemoteStationEyeMonitorComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !IsPowered(ent))
            return;

        var viewer = args.User;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("syndicate-monitor-direct-surveillance-mode"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/vv.svg.192dpi.png")),
            Act = () => OpenDirectSurveillance(ent.Owner, viewer),
        });
    }

    /// <summary>Opens the observation picker without replacing the monitor's usual camera UI.</summary>
    public bool OpenDirectSurveillance(EntityUid monitor, EntityUid viewer)
    {
        if (!HasComp<RemoteStationEyeMonitorComponent>(monitor) || !IsPowered(monitor) ||
            !_interaction.InRangeUnobstructed(viewer, monitor))
            return false;

        UpdateStationList(monitor);
        return _ui.TryOpenUi(monitor, AbductorCameraConsoleUIKey.Key, viewer);
    }

    private void UpdateStationList(EntityUid monitor)
    {
        var result = new Dictionary<int, StationBeacons>();
        foreach (var station in _stations.GetStations())
        {
            if (_floors.GetStationDefaultGrid(station) is not { } defaultGrid)
                continue;

            var beacons = new List<SharedNavMapSystem.NavMapBeacon>();
            foreach (var grid in _floors.GetStationFloorGrids(station))
            {
                _navMap.EnsureNavMap(grid);
                if (TryComp<NavMapComponent>(grid, out var navMap))
                    beacons.AddRange(navMap.Beacons.Values);
            }

            // Stations without map beacons can still be observed from their main grid.
            if (beacons.Count == 0 && TryComp<MapGridComponent>(defaultGrid, out var mapGrid))
                beacons.Add(new SharedNavMapSystem.NavMapBeacon(
                    GetNetEntity(defaultGrid), Color.Cyan, Loc.GetString("syndicate-monitor-station-center"), mapGrid.LocalAABB.Center));

            result.Add(station.Id, new StationBeacons
            {
                StationId = station.Id,
                Name = MetaData(station).EntityName,
                Beacons = beacons,
            });
        }

        _ui.SetUiState(monitor, AbductorCameraConsoleUIKey.Key,
            new AbductorCameraConsoleBuiState { Stations = result });
    }

    private void OnBeaconChosen(Entity<RemoteStationEyeMonitorComponent> ent, ref AbductorBeaconChosenBuiMsg args)
    {
        TryObserve(ent, args.Actor, args.Beacon.NetEnt);
    }

    public bool TryObserve(EntityUid monitor, EntityUid viewer, NetEntity beaconNet)
    {
        if (!HasComp<RemoteStationEyeMonitorComponent>(monitor) || !IsPowered(monitor) ||
            !TryComp<EyeComponent>(viewer, out var actorEye))
            return false;

        if (!HasComp<RemoteStationEyeViewerComponent>(viewer) &&
            (HasComp<StationAiOverlayComponent>(viewer) || HasComp<RelayInputMoverComponent>(viewer)))
            return false;

        if (!TryGetBeaconCoordinates(beaconNet, out var coordinates))
            return false;

        StopViewing(viewer);
        var eye = SpawnAtPosition(EyePrototype, coordinates);
        var state = EnsureComp<RemoteStationEyeViewerComponent>(viewer);
        state.Monitor = monitor;
        state.Eye = eye;
        state.PreviousEyeTarget = actorEye.Target;
        state.PreviousVisibilityMask = actorEye.VisibilityMask;
        state.PreviousDrawFov = actorEye.DrawFov;
        state.HadInteractionBlock = TryComp<BlockMovementComponent>(viewer, out var block);
        state.PreviousBlockInteraction = block?.BlockInteraction ?? false;
        state.PreviousBlockUse = block?.BlockUse ?? false;
        block ??= EnsureComp<BlockMovementComponent>(viewer);
        block.BlockInteraction = true;
        block.BlockUse = true;
        Dirty(viewer, block);
        Comp<RemoteStationEyeMonitorComponent>(monitor).ViewerEyes[viewer] = eye;

        _eyes.SetVisibilityMask(viewer, actorEye.VisibilityMask | (int) VisibilityFlags.Abductor, actorEye);
        _eyes.SetTarget(viewer, eye, actorEye);
        _eyes.SetDrawFov(viewer, false);
        _eyes.SetRotation(viewer, Angle.Zero, actorEye);
        AddComp(viewer, new StationAiOverlayComponent
        {
            AllowCrossGrid = true,
            AllowUnseenMachineAccess = false,
            IncludeSyndicateCameras = true,
        });
        _mover.SetRelay(viewer, eye);
        _actions.AddAction(viewer, ref state.ExitAction, ExitAction);
        _zEye.ConfigureActions(viewer, ViewUpAction, ViewDownAction);
        return true;
    }

    private bool TryGetBeaconCoordinates(NetEntity beaconNet, out EntityCoordinates coordinates)
    {
        coordinates = default;
        if (!TryGetEntity(beaconNet, out var beacon))
            return false;

        foreach (var station in _stations.GetStations())
        {
            if (_floors.GetStationDefaultGrid(station) is not { } defaultGrid)
                continue;

            if (beacon == defaultGrid && TryComp<MapGridComponent>(defaultGrid, out var mapGrid))
            {
                coordinates = new EntityCoordinates(defaultGrid, mapGrid.LocalAABB.Center);
                return true;
            }

            foreach (var grid in _floors.GetStationFloorGrids(station))
            {
                if (TryComp<NavMapComponent>(grid, out var navMap) &&
                    navMap.Beacons.ContainsKey(beaconNet) &&
                    TryComp<TransformComponent>(beacon, out var xform) && xform.GridUid == grid)
                {
                    coordinates = xform.Coordinates;
                    return true;
                }
            }
        }

        return false;
    }

    private void OnExit(RemoteStationEyeExitEvent args)
    {
        if (!HasComp<RemoteStationEyeViewerComponent>(args.Performer))
            return;

        StopViewing(args.Performer);
        args.Handled = true;
    }

    private void OnPowerChanged(Entity<RemoteStationEyeMonitorComponent> ent, ref PowerChangedEvent args)
    {
        if (args.Powered)
            return;

        _ui.CloseUi(ent.Owner, AbductorCameraConsoleUIKey.Key);
        StopAllViewers(ent);
    }

    private void OnMonitorShutdown(Entity<RemoteStationEyeMonitorComponent> ent, ref ComponentShutdown args)
        => StopAllViewers(ent);

    private void OnViewerShutdown(Entity<RemoteStationEyeViewerComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp<RemoteStationEyeMonitorComponent>(ent.Comp.Monitor, out var monitor))
            monitor.ViewerEyes.Remove(ent.Owner);

        if (Exists(ent.Comp.Eye))
            QueueDel(ent.Comp.Eye);
    }

    private void StopAllViewers(Entity<RemoteStationEyeMonitorComponent> ent)
    {
        foreach (var viewer in ent.Comp.ViewerEyes.Keys.ToArray())
            StopViewing(viewer);
    }

    public void StopViewing(EntityUid viewer)
    {
        if (!TryComp<RemoteStationEyeViewerComponent>(viewer, out var state))
            return;

        if (TryComp<RemoteStationEyeMonitorComponent>(state.Monitor, out var monitor))
            monitor.ViewerEyes.Remove(viewer);

        if (TryComp<RelayInputMoverComponent>(viewer, out var relay) && relay.RelayEntity == state.Eye)
            RemComp<RelayInputMoverComponent>(viewer);
        if (TryComp<EyeComponent>(viewer, out var actorEye))
        {
            if (actorEye.Target == state.Eye)
            {
                EntityUid? previousTarget = state.PreviousEyeTarget is { } target && Exists(target) ? target : null;
                _eyes.SetTarget(viewer, previousTarget, actorEye);
            }

            _eyes.SetDrawFov(viewer, state.PreviousDrawFov);
            _eyes.SetVisibilityMask(viewer, state.PreviousVisibilityMask, actorEye);
        }

        if (state.HadInteractionBlock && TryComp<BlockMovementComponent>(viewer, out var block))
        {
            block.BlockInteraction = state.PreviousBlockInteraction;
            block.BlockUse = state.PreviousBlockUse;
            Dirty(viewer, block);
        }
        else if (!state.HadInteractionBlock)
        {
            RemComp<BlockMovementComponent>(viewer);
        }

        RemComp<StationAiOverlayComponent>(viewer);
        _zEye.RemoveActions(viewer);
        _actions.RemoveAction(viewer, state.ExitAction);
        RemComp<RemoteStationEyeViewerComponent>(viewer);
    }

    private bool IsPowered(EntityUid uid)
        => TryComp<ApcPowerReceiverComponent>(uid, out var receiver) && receiver.Powered;
}
