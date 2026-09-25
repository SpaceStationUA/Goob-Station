// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Pirate.ListeningPost.Components;
using Content.Server._Pirate.SurveillanceCamera;
using Content.Server.Pinpointer;
using Content.Server.Power.Components;
using Content.Server.Station.Systems;
using Content.Server.SurveillanceCamera;
using Content.Shared._Pirate.ListeningPost;
using Content.Shared._Pirate.SurveillanceCamera;
using Content.Shared.Clothing.Components;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.StationAi;
using Content.Shared.SurveillanceCamera;
using Content.Shared.SurveillanceCamera.Components;
using Robust.Shared.Map;
using Robust.Server.GameObjects;
using Robust.Server.GameStates;
using Robust.Shared.Player;

namespace Content.Server._Pirate.ListeningPost.Systems;

public sealed class LongRangeSurveillanceServerSystem : EntitySystem
{
    private const float UpdateRate = 3f;

    [Dependency] private readonly LongRangeTargetStationSystem _targetStation = default!;
    [Dependency] private readonly NavMapSystem _navMap = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly SurveillanceCameraMonitorSystem _monitors = default!;
    [Dependency] private readonly TransformSystem _transforms = default!;
    [Dependency] private readonly PvsOverrideSystem _pvsOverride = default!;

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
            var mobileCameras = CollectMobileCameras();
            FeedLocalConsoles(serverXform.MapID, grid, cameras, mobileCameras);
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
        Dictionary<string, (string, (NetEntity, NetCoordinates))> cameras,
        Dictionary<string, (string, (NetEntity, NetCoordinates))> mobileCameras)
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

            foreach (var (address, oldData) in monitor.KnownMobileCameras)
            {
                if (mobileCameras.ContainsKey(address) ||
                    !TryGetEntity(oldData.Item2.Item1, out var oldCamera))
                    continue;

                foreach (var viewer in monitor.Viewers)
                    if (TryComp<ActorComponent>(viewer, out var actor))
                        _pvsOverride.RemoveSessionOverride(oldCamera.Value, actor.PlayerSession);
            }

            foreach (var (address, data) in mobileCameras)
            {
                if (monitor.KnownMobileCameras.ContainsKey(address))
                    continue;

                foreach (var viewer in monitor.Viewers)
                    if (TryComp<ActorComponent>(viewer, out var actor))
                        _pvsOverride.AddSessionOverride(GetEntity(data.Item2.Item1), actor.PlayerSession);
            }

            monitor.KnownMobileCameras.Clear();
            foreach (var (address, data) in mobileCameras)
                monitor.KnownMobileCameras.Add(address, data);

            if (monitor.ActiveCameraAddress.Length > 0 &&
                !cameras.ContainsKey(monitor.ActiveCameraAddress) &&
                !mobileCameras.ContainsKey(monitor.ActiveCameraAddress))
                _monitors.DisconnectCamera(console, true, monitor);
            else
                _monitors.UpdateUserInterface(console, monitor);
        }
    }

    private Dictionary<string, (string, (NetEntity, NetCoordinates))> CollectMobileCameras()
    {
        var cameras = new Dictionary<string, (string, (NetEntity, NetCoordinates))>();
        var query = EntityQueryEnumerator<SurveillanceCameraComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var camera, out var xform))
        {
            if (!camera.Active || !camera.Mobile || xform.GridUid == null || !IsSyndicateMobileCamera(uid))
                continue;

            var address = $"syndicate-mobile:{GetNetEntity(uid)}";
            var name = camera.UseEntityNameAsCameraId ? MetaData(uid).EntityName : camera.CameraId;
            var coordinates = _transforms.ToCoordinates(uid, _transforms.ToMapCoordinates(xform.Coordinates));
            cameras[address] = (name, (GetNetEntity(uid), GetNetCoordinates(coordinates)));
        }

        return cameras;
    }

    private bool IsSyndicateMobileCamera(EntityUid camera)
        => HasComp<SyndicatePdaCameraComponent>(camera) ||
           (HasComp<CameraActiveVisionComponent>(camera) && HasComp<StationAiVisionComponent>(camera)) ||
           (HasComp<ClothingComponent>(camera) && HasComp<SurveillanceCameraMicrophoneComponent>(camera));

    private void OnSwitchCamera(
        Entity<LongRangeSurveillanceMonitorComponent> ent,
        ref SurveillanceCameraMonitorSwitchMessage args)
        => TrySwitchCamera(ent, args.Address);

    public bool TrySwitchCamera(EntityUid console, string address)
    {
        if (!HasComp<LongRangeSurveillanceMonitorComponent>(console) ||
            !TryComp<SurveillanceCameraMonitorComponent>(console, out var monitor))
            return false;

        var mobile = monitor.KnownMobileCameras.TryGetValue(address, out var data);
        if (!mobile && !monitor.KnownCameras.TryGetValue(address, out data))
            return false;

        if (!TryGetEntity(data.Item2.Item1, out var camera) ||
            !TryComp<SurveillanceCameraComponent>(camera, out var component) || !component.Active ||
            (mobile && (!component.Mobile || !IsSyndicateMobileCamera(camera.Value))))
            return false;

        _monitors.ConnectDirectly(console, camera.Value, address, monitor);
        return true;
    }
}
