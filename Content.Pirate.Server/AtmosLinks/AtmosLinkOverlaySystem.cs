// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Globalization;
using Content.Server.Atmos.Monitor.Components;
using Content.Server.Atmos.Piping.Unary.Components;
using Content.Server.DeviceNetwork.Systems;
using Content.Pirate.Shared.AtmosLinks;
using Content.Shared.Atmos.Components;
using Content.Shared.Doors.Components;
using Content.Shared.DeviceNetwork.Components;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Pirate.Server.AtmosLinks;

public sealed class AtmosLinkOverlaySystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly DeviceListSystem _deviceList = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private const float UpdateInterval = 1f;

    private float _accumulator;

    private readonly Dictionary<ICommonSession, AtmosLinkOverlayOptions> _observers = new();

    public override void Initialize()
    {
        base.Initialize();

        _players.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _players.PlayerStatusChanged -= OnPlayerStatusChanged;
        _observers.Clear();
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus != SessionStatus.Disconnected)
            return;

        _observers.Remove(args.Session);
    }

    public bool IsEnabled(ICommonSession session)
    {
        return _observers.ContainsKey(session);
    }

    public AtmosLinkReport Enable(ICommonSession session, AtmosLinkOverlayOptions options)
    {
        _observers[session] = options;
        return SendTo(session, options);
    }

    public void Disable(ICommonSession session)
    {
        if (_observers.Remove(session))
            RaiseNetworkEvent(new AtmosLinkOverlayDisableEvent(), session.Channel);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_observers.Count == 0)
            return;

        _accumulator += frameTime;
        if (_accumulator < UpdateInterval)
            return;

        _accumulator = 0f;

        foreach (var (session, options) in _observers)
        {
            SendTo(session, options);
        }
    }

    private AtmosLinkReport SendTo(ICommonSession session, AtmosLinkOverlayOptions options)
    {
        MapId? map = null;
        if (!options.AllMaps)
            map = GetSessionMap(session);

        var report = BuildReport(map);
        RaiseNetworkEvent(new AtmosLinkOverlayDataEvent(report.Groups, report.Orphans), session.Channel);
        return report;
    }

    private MapId? GetSessionMap(ICommonSession session)
    {
        if (session.AttachedEntity is not { } player || !TryComp<TransformComponent>(player, out var xform))
            return null;

        return xform.MapID == MapId.Nullspace ? null : xform.MapID;
    }

    public AtmosLinkReport BuildReport(MapId? map)
    {
        var report = new AtmosLinkReport();

        // Reverse index of every device list in the world, not just the ones on this map, so a device linked
        // from another grid doesn't get reported as unlinked.
        var linked = new HashSet<EntityUid>();
        var listQuery = AllEntityQuery<DeviceListComponent>();
        while (listQuery.MoveNext(out var listUid, out var listComp))
        {
            foreach (var device in _deviceList.GetAllDevices(listUid, listComp))
            {
                linked.Add(device);
            }
        }

        var query = AllEntityQuery<DeviceNetworkComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var netComp, out var xform))
        {
            if (netComp.NetIdEnum != DeviceNetworkComponent.DeviceNetIdDefaults.AtmosDevices)
                continue;

            if (map != null && xform.MapID != map.Value)
                continue;

            if (xform.MapID == MapId.Nullspace)
                continue;

            var kind = GetKind(uid);

            // Everything else on this net can't be linked to an alarm at all: SMES, thermomachines
            // (freezer/heater/hellfire), volumetric pumps, pipe sensors, the TEG and the atmos monitoring
            // console. Reporting those as "unlinked" would just bury the devices that matter.
            if (kind == AtmosLinkDeviceKind.Other)
                continue;

            report.DeviceCount++;

            var coords = GetNetCoordinates(xform.Coordinates);

            if (TryComp<DeviceListComponent>(uid, out var list))
            {
                var targets = new List<NetCoordinates>();

                foreach (var device in _deviceList.GetAllDevices(uid, list))
                {
                    if (Deleted(device) || !TryComp<TransformComponent>(device, out var deviceXform))
                    {
                        report.Dangling.Add($"{ToPrettyString(uid)} links to a deleted entity ({device})");
                        continue;
                    }

                    targets.Add(GetNetCoordinates(deviceXform.Coordinates));

                    // The state that "synchronizedevicelists" repairs: the list knows about the device but the
                    // device doesn't know about the list, so it silently drops out on the next list update.
                    if (!TryComp<DeviceNetworkComponent>(device, out var deviceNet)
                        || !deviceNet.DeviceLists.Contains(uid))
                    {
                        report.Desynced.Add($"{ToPrettyString(device)} is in {ToPrettyString(uid)} but doesn't know it");
                    }
                }

                report.LinkCount += targets.Count;

                if (targets.Count > 0)
                    report.Groups.Add(new AtmosLinkGroup(coords, kind, targets));
                else
                    AddOrphan(report, uid, xform, coords, kind);

                continue;
            }

            if (!linked.Contains(uid))
                AddOrphan(report, uid, xform, coords, kind);
        }

        return report;
    }

    private void AddOrphan(AtmosLinkReport report,
        EntityUid uid,
        TransformComponent xform,
        NetCoordinates coords,
        AtmosLinkDeviceKind kind)
    {
        report.Orphans.Add(new AtmosLinkOrphan(coords, kind));

        var mapCoords = _transform.GetMapCoordinates(uid, xform);

        // Invariant so the "tp" line stays copy-pasteable into the console on locales that would
        // otherwise print "23,5".
        var x = mapCoords.X.ToString("F1", CultureInfo.InvariantCulture);
        var y = mapCoords.Y.ToString("F1", CultureInfo.InvariantCulture);

        report.OrphanLines.Add($"{ToPrettyString(uid)} - tp {x} {y} {(int) mapCoords.MapId}");
    }

    private AtmosLinkDeviceKind GetKind(EntityUid uid)
    {
        if (HasComp<AirAlarmComponent>(uid))
            return AtmosLinkDeviceKind.AirAlarm;

        if (HasComp<FireAlarmComponent>(uid))
            return AtmosLinkDeviceKind.FireAlarm;

        if (HasComp<GasVentPumpComponent>(uid))
            return AtmosLinkDeviceKind.Vent;

        if (HasComp<GasVentScrubberComponent>(uid))
            return AtmosLinkDeviceKind.Scrubber;

        if (HasComp<FirelockComponent>(uid))
            return AtmosLinkDeviceKind.Firelock;

        // Pipe sensors carry an AtmosMonitor just like a room air sensor, but they feed the atmos
        // monitoring console instead of an alarm, so they must not be mistaken for one.
        if (HasComp<GasPipeSensorComponent>(uid))
            return AtmosLinkDeviceKind.Other;

        if (HasComp<AtmosMonitorComponent>(uid))
            return AtmosLinkDeviceKind.Sensor;

        return AtmosLinkDeviceKind.Other;
    }
}

public sealed class AtmosLinkReport
{
    public readonly List<AtmosLinkGroup> Groups = new();
    public readonly List<AtmosLinkOrphan> Orphans = new();

    public readonly List<string> OrphanLines = new();

    public readonly List<string> Dangling = new();

    public readonly List<string> Desynced = new();

    public int DeviceCount;
    public int LinkCount;
}

public struct AtmosLinkOverlayOptions
{
    public bool AllMaps;
}
