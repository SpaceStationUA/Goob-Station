// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Pirate.ListeningPost.Components;
using Content.Server.Medical.CrewMonitoring;
using Content.Server.Medical.SuitSensors;
using Content.Server.Power.Components;
using Content.Shared._Pirate.ListeningPost;
using Content.Shared.DeviceNetwork;
using Content.Shared.DeviceNetwork.Events;
using Content.Shared.Medical.CrewMonitoring;
using Content.Shared.Medical.SuitSensor;
using Content.Shared.Medical.SuitSensors;
using Content.Shared.Station.Components;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._Pirate.ListeningPost.Systems;

public sealed class LongRangeCrewMonitoringServerSystem : EntitySystem
{
    private const float UpdateRate = 3f;

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly LongRangeCrewMonitorSystem _longRangeMonitor = default!;
    [Dependency] private readonly SuitSensorSystem _suitSensors = default!;

    private float _updateAccumulator;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _updateAccumulator += frameTime;
        if (_updateAccumulator < UpdateRate)
            return;

        _updateAccumulator %= UpdateRate;

        var servers = EntityQueryEnumerator<LongRangeCrewMonitoringServerComponent, TransformComponent>();
        while (servers.MoveNext(out var server, out _, out var serverXform))
        {
            if (TryComp<ApcPowerReceiverComponent>(server, out var power) && !power.Powered)
                continue;

            if (_longRangeMonitor.FindLargestStationGridInMap(serverXform.MapID) is not { } stationGrid ||
                !TryComp<StationMemberComponent>(stationGrid, out var stationMember))
            {
                continue;
            }

            var statuses = CollectSensorStatuses(stationMember.Station);
            SendToLongRangeConsoles(server, serverXform.MapID, statuses);
        }
    }

    private Dictionary<string, SuitSensorStatus> CollectSensorStatuses(EntityUid station)
    {
        var statuses = new Dictionary<string, SuitSensorStatus>();
        var sensors = EntityQueryEnumerator<SuitSensorComponent>();
        while (sensors.MoveNext(out var sensor, out var sensorComp))
        {
            if (sensorComp.StationId != station || _suitSensors.GetSensorState((sensor, sensorComp)) is not { } status)
                continue;

            status.Timestamp = _timing.CurTime;
            statuses[GetNetEntity(sensor).ToString()] = status;
        }

        return statuses;
    }

    private void SendToLongRangeConsoles(
        EntityUid server,
        MapId map,
        Dictionary<string, SuitSensorStatus> statuses)
    {
        var payload = new NetworkPayload
        {
            [DeviceNetworkConstants.Command] = DeviceNetworkConstants.CmdUpdatedState,
            [SuitSensorConstants.NET_STATUS_COLLECTION] = statuses,
        };

        var consoles = EntityQueryEnumerator<LongRangeCrewMonitorComponent, CrewMonitoringConsoleComponent, TransformComponent>();
        while (consoles.MoveNext(out var console, out _, out _, out var consoleXform))
        {
            if (consoleXform.MapID != map)
                continue;

            var ev = new DeviceNetworkPacketEvent(0, null, 0, string.Empty, server, payload);
            RaiseLocalEvent(console, ev);
        }
    }
}
