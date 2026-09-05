// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Pirate.ListeningPost.Components;
using Content.Server.Pinpointer;
using Content.Server.Power.Components;
using Content.Server.Station.Systems;
using Content.Server.SurveillanceCamera;
using Content.Shared._Pirate.ListeningPost;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.SurveillanceCamera;
using Content.Shared.SurveillanceCamera.Components;
using Robust.Shared.Map;

namespace Content.Server._Pirate.ListeningPost.Systems;

public sealed class LongRangeSurveillanceServerSystem : EntitySystem
{
    private const float UpdateRate = 3f;

    [Dependency] private readonly LongRangeTargetStationSystem _targetStation = default!;
    [Dependency] private readonly NavMapSystem _navMap = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly SurveillanceCameraMonitorSystem _monitors = default!;

    private float _updateAccumulator;

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<LongRangeSurveillanceMonitorComponent>(SurveillanceCameraMonitorUiKey.Key, subs =>
        {
            subs.Event<SurveillanceCameraMonitorSwitchMessage>(OnSwitchCamera);
        });
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _updateAccumulator += frameTime;
        if (_updateAccumulator < UpdateRate)
            return;

        _updateAccumulator %= UpdateRate;

        var servers = EntityQueryEnumerator<LongRangeSurveillanceServerComponent, TransformComponent>();
        while (servers.MoveNext(out var server, out _, out var serverXform))
        {
            if (TryComp<ApcPowerReceiverComponent>(server, out var power) && !power.Powered)
                continue;

            if (_targetStation.ResolveTargetStation(serverXform.MapID) is not { } target)
                continue;

            var (station, grid) = target;

            var cameras = CollectStationCameras(station);
            FeedLocalConsoles(serverXform.MapID, grid, cameras);
        }
    }

    private Dictionary<string, (string, (NetEntity, NetCoordinates))> CollectStationCameras(EntityUid station)
    {
        var cameras = new Dictionary<string, (string, (NetEntity, NetCoordinates))>();

        var query = EntityQueryEnumerator<SurveillanceCameraComponent, DeviceNetworkComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var camera, out var deviceNet, out var xform))
        {
            if (!camera.Active || camera.Mobile)
                continue;

            if (string.IsNullOrEmpty(deviceNet.Address) || _station.GetOwningStation(uid) != station)
                continue;

            var name = camera.UseEntityNameAsCameraId ? MetaData(uid).EntityName : camera.CameraId;
            cameras[deviceNet.Address] = (name, (GetNetEntity(uid), GetNetCoordinates(xform.Coordinates)));
        }

        return cameras;
    }

    private void FeedLocalConsoles(
        MapId map,
        EntityUid targetGrid,
        Dictionary<string, (string, (NetEntity, NetCoordinates))> cameras)
    {
        var consoles = EntityQueryEnumerator<LongRangeSurveillanceMonitorComponent, SurveillanceCameraMonitorComponent, TransformComponent>();
        while (consoles.MoveNext(out var console, out var longRange, out var monitor, out var consoleXform))
        {
            if (consoleXform.MapID != map)
                continue;

            if (longRange.TargetGrid != targetGrid)
            {
                longRange.TargetGrid = targetGrid;
                Dirty(console, longRange);
            }

            _navMap.EnsureNavMap(targetGrid);

            monitor.KnownCameras.Clear();
            foreach (var (address, data) in cameras)
            {
                monitor.KnownCameras.Add(address, data);
            }

            if (monitor.ActiveCameraAddress.Length > 0 && !cameras.ContainsKey(monitor.ActiveCameraAddress))
                _monitors.DisconnectCamera(console, true, monitor);
            else
                _monitors.UpdateUserInterface(console, monitor);
        }
    }

    private void OnSwitchCamera(
        Entity<LongRangeSurveillanceMonitorComponent> ent,
        ref SurveillanceCameraMonitorSwitchMessage args)
    {
        if (!TryComp<SurveillanceCameraMonitorComponent>(ent, out var monitor))
            return;

        if (!monitor.KnownCameras.TryGetValue(args.Address, out var data))
            return;

        var camera = GetEntity(data.Item2.Item1);
        if (!HasComp<SurveillanceCameraComponent>(camera))
            return;

        _monitors.ConnectDirectly(ent, camera, args.Address, monitor);
    }
}
