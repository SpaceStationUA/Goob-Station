// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Server._Pirate.ListeningPost.Components;
using Content.Server._Pirate.Medical.CrewMonitoring;
using Content.Server.DeviceNetwork.Components;
using Content.Server.DeviceNetwork.Systems;
using Content.Server.Medical.CrewMonitoring;
using Content.Server.Medical.SuitSensors;
using Content.Server.Pinpointer;
using Content.Server.Power.Components;
using Content.Server.Station.Systems;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.Medical.CrewMonitoring;
using Content.Shared.Medical.SuitSensor;
using Content.Shared.Medical.SuitSensors;

using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._Pirate.ListeningPost.Systems;

/// <summary>
/// Feeds the normal crew-monitoring server pipeline with a snapshot collected from
/// the station selected for this listening post. The console remains unaware that
/// its selected server is remote.
/// </summary>
public sealed class LongRangeCrewMonitoringServerSystem : EntitySystem
{
    private const float UpdateRate = 3f;

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly LongRangeTargetStationSystem _targetStation = default!;
    [Dependency] private readonly NavMapSystem _navMap = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly SuitSensorSystem _suitSensors = default!;
    [Dependency] private readonly DeviceNetworkSystem _deviceNetwork = default!;

    private float _updateAccumulator;


    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _updateAccumulator += frameTime;
        if (_updateAccumulator < UpdateRate)
            return;

        _updateAccumulator %= UpdateRate;

        var servers = EntityQueryEnumerator<
            LongRangeCrewMonitoringServerComponent,
            CrewMonitoringServerComponent,
            TransformComponent>();
        while (servers.MoveNext(out var server, out _, out var monitoring, out var serverXform))
        {
            if (TryComp<ApcPowerReceiverComponent>(server, out var power) && !power.Powered)
                continue;

            if (!TryComp<DeviceNetworkComponent>(server, out var device))
                continue;

            if (!_deviceNetwork.IsDeviceConnected(server, device))
                _deviceNetwork.ConnectDevice(server, device);

            if (_targetStation.ResolveTargetStation(serverXform.MapID) is not { } target)
                continue;

            var (station, targetGrid) = target;
            _navMap.EnsureNavMap(targetGrid);
            var snapshot = CollectSensorStatuses(station);
            monitoring.ReferenceFrame = new CrewMonitoringReferenceFrame(
                GetNetEntity(targetGrid),
                GetNetCoordinates(new EntityCoordinates(targetGrid, Vector2.Zero)),
                TryComp<WirelessNetworkComponent>(server, out var wireless) ? wireless.Range : 500f,
                Name(targetGrid));
            monitoring.SensorStatus.Clear();
            monitoring.LastSensorSnapshot.Clear();
            foreach (var (key, status) in snapshot)
            {
                monitoring.SensorStatus[key] = status;
                monitoring.LastSensorSnapshot[key] = status;
            }

            monitoring.SnapshotDirty = true;
            var update = new CrewMonitoringServerUpdateEvent(
                CrewMonitoringServerSystem.CopyLastSnapshot(monitoring));
            RaiseLocalEvent(server, ref update);
        }
    }

    private Dictionary<string, SuitSensorStatus> CollectSensorStatuses(EntityUid station)
    {
        var statuses = new Dictionary<string, SuitSensorStatus>();
        var sensors = EntityQueryEnumerator<SuitSensorComponent, TransformComponent>();
        while (sensors.MoveNext(out var sensor, out var sensorComp, out var sensorXform))
        {
            if (_station.GetOwningStation(sensor) != station ||
                _suitSensors.GetSensorState((sensor, sensorComp, sensorXform)) is not { } status)
            {
                continue;
            }

            status.Timestamp = _timing.CurTime;
            status.IsActive = status.Mode != SuitSensorMode.SensorOff;
            statuses[$"sensor-{status.SuitSensorUid}"] = status;
        }

        return statuses;
    }
}
