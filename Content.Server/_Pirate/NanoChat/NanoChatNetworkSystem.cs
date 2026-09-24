// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.Communications;
using Content.Server.Power.Components;
using Content.Server._Pirate.NanoChatMonitor;
using Content.Shared._DV.NanoChat;
using Content.Shared._Pirate.NanoChat;
using Content.Shared._Pirate.NanoChatMonitor;
using Content.Shared._Pirate.ZLevels.Core.EntitySystems;
using Content.Shared.Power;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._Pirate.NanoChat;

public sealed class NanoChatNetworkSystem : EntitySystem
{
    [Dependency] private readonly CESharedZLevelsSystem _zLevels = default!;

    [Dependency] private readonly SharedNanoChatLogHostSystem _logHost = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TelecomServerComponent, PowerChangedEvent>(OnServerPowerChanged);
        SubscribeLocalEvent<TelecomTransmitterComponent, PowerChangedEvent>(OnTransmitterPowerChanged);
        SubscribeLocalEvent<SyndicateNanoChatRelayComponent, PowerChangedEvent>(OnRelayPowerChanged);
    }

    private void OnServerPowerChanged(Entity<TelecomServerComponent> ent, ref PowerChangedEvent args)
    {
        RaiseNetworkChanged();
    }

    private void OnTransmitterPowerChanged(Entity<TelecomTransmitterComponent> ent, ref PowerChangedEvent args)
    {
        RaiseNetworkChanged();
    }

    private void OnRelayPowerChanged(Entity<SyndicateNanoChatRelayComponent> ent, ref PowerChangedEvent args)
    {
        RaiseNetworkChanged();
    }

    private void RaiseNetworkChanged()
    {
        var ev = new NanoChatNetworkChangedEvent();
        RaiseLocalEvent(ref ev);
    }

    #region Topology

    public NanoChatNetworkTopology BuildTopology()
    {
        var topology = new NanoChatNetworkTopology();

        var servers = EntityQueryEnumerator<TelecomServerComponent, TransformComponent>();
        while (servers.MoveNext(out var uid, out _, out var xform))
        {
            if (!IsPowered(uid))
                continue;

            if (HasComp<SyndicateNanoChatRelayComponent>(uid))
                continue;

            topology.AddServer(uid, _zLevels.GetCoverageMapIds(uid, xform));
        }

        var transmitters = EntityQueryEnumerator<TelecomTransmitterComponent, TransformComponent>();
        while (transmitters.MoveNext(out var uid, out _, out var xform))
        {
            if (!IsPowered(uid))
                continue;

            topology.AddTransmitter(xform.MapID);
        }

        topology.Seal();
        return topology;
    }

    public bool TryGetDomain(NanoChatNetworkTopology topology, EntityUid uid, out MapId domain)
    {
        domain = default;

        if (Deleted(uid))
            return false;

        return topology.TryGetDomain(Transform(uid).MapID, out domain);
    }

    public bool HasOrdinaryCoverage(NanoChatNetworkTopology topology, EntityUid uid)
    {
        return TryGetDomain(topology, uid, out _);
    }

    public bool CanOrdinaryReach(NanoChatNetworkTopology topology, EntityUid a, EntityUid b)
    {
        return TryGetDomain(topology, a, out var domainA) &&
               TryGetDomain(topology, b, out var domainB) &&
               topology.AreLinked(domainA, domainB);
    }

    #endregion

    #region Syndicate

    public bool IsRelayActive()
    {
        var query = EntityQueryEnumerator<SyndicateNanoChatRelayComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (IsPowered(uid))
                return true;
        }

        return false;
    }

    public bool IsSyndicateDevice(EntityUid? uid)
    {
        return uid is { } device && !Deleted(device) &&
               HasComp<SyndicateNanoChatPdaComponent>(device) && !HasComp<OrdinaryNanoChatPdaComponent>(device);
    }

    public bool IsSyndicateCard(EntityUid? card)
    {
        if (card is not { } uid || Deleted(uid))
            return false;

        if (!TryComp<NanoChatCardComponent>(uid, out var comp))
            return false;

        return IsSyndicateDevice(comp.PdaUid);
    }

    public bool CanDeliver(
        NanoChatNetworkTopology topology,
        EntityUid sender,
        bool senderSyndicate,
        EntityUid recipient,
        bool recipientSyndicate,
        bool relayActive)
    {
        if (senderSyndicate || recipientSyndicate)
        {
            if (!relayActive)
                return false;

            if (!senderSyndicate && !HasOrdinaryCoverage(topology, sender))
                return false;

            if (!recipientSyndicate && !HasOrdinaryCoverage(topology, recipient))
                return false;

            return true;
        }

        return CanOrdinaryReach(topology, sender, recipient);
    }

    #endregion

    #region Logging

    public void GetRecordingServers(
        NanoChatNetworkTopology topology,
        EntityUid? senderDevice,
        IReadOnlyList<EntityUid> recipientDevices,
        List<EntityUid> results)
    {
        results.Clear();

        var query = EntityQueryEnumerator<NanoChatMonitorComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (!IsPowered(uid))
                continue;

            if (HasComp<SyndicateNanoChatRelayComponent>(uid))
            {
                results.Add(uid);
                continue;
            }

            if (!KeepsNanoChatLog(uid))
                continue;

            if (Covers(topology, uid, senderDevice) || CoversAny(topology, uid, recipientDevices))
                results.Add(uid);
        }
    }

    public void GetFederatedServers(EntityUid viewer, List<EntityUid> results)
    {
        results.Clear();

        if (!IsPowered(viewer))
            return;

        results.Add(viewer);

        if (HasComp<SyndicateNanoChatRelayComponent>(viewer))
            return;

        var topology = BuildTopology();
        if (!TryGetDomain(topology, viewer, out var viewerDomain))
            return;

        var query = EntityQueryEnumerator<NanoChatMonitorComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (uid == viewer || !IsPowered(uid))
                continue;

            if (HasComp<SyndicateNanoChatRelayComponent>(uid) || !KeepsNanoChatLog(uid))
                continue;

            if (TryGetDomain(topology, uid, out var domain) && topology.AreLinked(viewerDomain, domain))
                results.Add(uid);
        }
    }

    public bool KeepsNanoChatLog(EntityUid uid)
    {
        return HasComp<TelecomServerComponent>(uid) && _logHost.HostsLog(uid);
    }

    private bool Covers(NanoChatNetworkTopology topology, EntityUid server, EntityUid? device)
    {
        if (device is not { } uid || Deleted(uid))
            return false;

        return topology.IsCoveredBy(server, Transform(uid).MapID);
    }

    private bool CoversAny(NanoChatNetworkTopology topology, EntityUid server, IReadOnlyList<EntityUid> devices)
    {
        foreach (var device in devices)
        {
            if (Covers(topology, server, device))
                return true;
        }

        return false;
    }

    #endregion

    private bool IsPowered(EntityUid uid)
    {
        return !TryComp<ApcPowerReceiverComponent>(uid, out var power) || power.Powered;
    }
}

/// <summary>
///     An immutable snapshot of ordinary NanoChat coverage: which maps have service, which of them form
///     one network, and which of those networks can reach outside themselves.
/// </summary>
/// <remarks>
///     Domains are identified by a representative <see cref="MapId" /> the union-find happened to pick,
///     not by anything meaningful. Compare them; never interpret them.
/// </remarks>
public sealed class NanoChatNetworkTopology
{
    private readonly Dictionary<MapId, MapId> _parents = new();

    private readonly Dictionary<EntityUid, HashSet<MapId>> _serverCoverage = new();

    private readonly List<MapId> _transmitterMaps = new();

    private readonly Dictionary<MapId, List<EntityUid>> _domainServers = new();
    private readonly HashSet<MapId> _transmitterDomains = new();

    internal void AddServer(EntityUid server, IReadOnlyCollection<MapId> maps)
    {
        if (maps.Count == 0)
            return;

        _serverCoverage[server] = [.. maps];

        MapId? anchor = null;
        foreach (var map in maps)
        {
            Add(map);

            if (anchor is { } first)
                Union(first, map);
            else
                anchor = map;
        }
    }

    internal void AddTransmitter(MapId map)
    {
        _transmitterMaps.Add(map);
    }

    internal void Seal()
    {
        foreach (var (server, maps) in _serverCoverage)
        {
            foreach (var map in maps)
            {
                var domain = Find(map);
                if (!_domainServers.TryGetValue(domain, out var servers))
                    _domainServers[domain] = servers = new List<EntityUid>();

                servers.Add(server);
                break;
            }
        }

        foreach (var map in _transmitterMaps)
        {
            if (_parents.ContainsKey(map))
                _transmitterDomains.Add(Find(map));
        }
    }

    public bool TryGetDomain(MapId map, out MapId domain)
    {
        domain = default;

        if (!_parents.ContainsKey(map))
            return false;

        domain = Find(map);
        return true;
    }

    public bool HasTransmitter(MapId domain)
    {
        return _transmitterDomains.Contains(domain);
    }

    public bool AreLinked(MapId domainA, MapId domainB)
    {
        if (domainA == domainB)
            return true;

        return HasTransmitter(domainA) && HasTransmitter(domainB);
    }

    public bool IsCoveredBy(EntityUid server, MapId map)
    {
        return _serverCoverage.TryGetValue(server, out var maps) && maps.Contains(map);
    }

    public IReadOnlyList<EntityUid> GetServers(MapId domain)
    {
        return _domainServers.TryGetValue(domain, out var servers) ? servers : [];
    }

    private void Add(MapId map)
    {
        if (!_parents.ContainsKey(map))
            _parents[map] = map;
    }

    private MapId Find(MapId map)
    {
        var root = map;
        while (_parents[root] != root)
        {
            root = _parents[root];
        }

        var walk = map;
        while (_parents[walk] != root)
        {
            var next = _parents[walk];
            _parents[walk] = root;
            walk = next;
        }

        return root;
    }

    private void Union(MapId a, MapId b)
    {
        var rootA = Find(a);
        var rootB = Find(b);

        if (rootA != rootB)
            _parents[rootB] = rootA;
    }
}
