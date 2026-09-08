// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using Content.Server._Pirate.ZLevels.Spawning;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Chat.Systems;
using Content.Server.Fax;
using Content.Server.GameTicking;
using Content.Server.Pinpointer;
using Content.Server.Power.Components;
using Content.Server.Radio.EntitySystems;
using Content.Shared.Station.Components;
using Content.Shared._Pirate.ListeningPost.DropConsole;
using Content.Shared.DeviceLinking;
using Content.Shared.Fax.Components;
using Content.Shared.Maps;
using Content.Shared.Station;
using Content.Shared.Whitelist;
using Content.Shared.Store;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Content.Shared.Paper;
using Robust.Shared.Utility;

namespace Content.Server._Pirate.ListeningPost.Systems;

public sealed class SyndicateDropConsoleSystem : EntitySystem
{
    private static readonly EntProtoId DispatcherPrototype = "SyndicateDropDispatcher";

    private static readonly ProtoId<CurrencyPrototype> Telecrystal = "Telecrystal";

    private const float PriceScanRange = 1f;

    private const LookupFlags PriceScanFlags = LookupFlags.Uncontained;

    private Dictionary<string, int>? _telecrystalValues;

    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
    [Dependency] private readonly CEZLevelFloorGridsSystem _zFloors = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly FaxSystem _fax = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly LongRangeTargetStationSystem _targetStation = default!;
    [Dependency] private readonly NavMapSystem _navMap = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SyndicateDropPadSystem _pad = default!;
    [Dependency] private readonly SharedStationSystem _station = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<SyndicateDropConsoleComponent>(SyndicateDropConsoleUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
            subs.Event<SyndicateDropConsoleSetModeMessage>(OnSetMode);
            subs.Event<SyndicateDropConsoleSelectTileMessage>(OnSelectTile);
            subs.Event<SyndicateDropConsoleNudgeTargetMessage>(OnNudgeTarget);
            subs.Event<SyndicateDropConsoleClearTargetMessage>(OnClearTarget);
            subs.Event<SyndicateDropConsoleLaunchMessage>(OnLaunch);
            subs.Event<SyndicateDropConsolePodSendMessage>(OnPodSend);
        });
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var consoles = EntityQueryEnumerator<SyndicateDropConsoleComponent>();
        while (consoles.MoveNext(out var uid, out var console))
        {
            console.Operational = !TryComp<ApcPowerReceiverComponent>(uid, out var power) || power.Powered;
        }

        if (!TryGetDispatcher(out var dispatcher))
            return;

        var comp = dispatcher.Value.Comp;
        LinkConsoles(dispatcher.Value.Owner);

        if (comp.TargetGrid == null || Deleted(comp.TargetGrid))
            TryAcquireStation(comp);

        if (_timing.CurTime < comp.NextDrop)
            return;

        if (comp.Manual && comp.Charges >= comp.MaxCharges)
            return;

        ScheduleNextDrop(comp);

        if (comp.Manual)
            comp.Charges++;
        else
            TryLaunchDrop(comp, SyndicateDropMode.Automatic);

        UpdateUis();
    }


    private bool TryGetDispatcher([NotNullWhen(true)] out Entity<SyndicateDropDispatcherComponent>? dispatcher)
    {
        if (TryFindDispatcher(out dispatcher))
            return true;

        if (ResolveTargetGrid() == null)
        {
            dispatcher = null;
            return false;
        }

        var spawned = Spawn(DispatcherPrototype, MapCoordinates.Nullspace);

        if (!TryComp<SyndicateDropDispatcherComponent>(spawned, out var spawnedComp))
        {
            Log.Error($"{DispatcherPrototype} is missing SyndicateDropDispatcher.");
            Del(spawned);
            dispatcher = null;
            return false;
        }

        ScheduleNextDrop(spawnedComp);
        TryAcquireStation(spawnedComp);

        dispatcher = (spawned, spawnedComp);
        return true;
    }

    private bool TryFindDispatcher([NotNullWhen(true)] out Entity<SyndicateDropDispatcherComponent>? dispatcher)
    {
        var query = EntityQueryEnumerator<SyndicateDropDispatcherComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            dispatcher = (uid, comp);
            return true;
        }

        dispatcher = null;
        return false;
    }

    private void LinkConsoles(EntityUid dispatcher)
    {
        var query = EntityQueryEnumerator<SyndicateDropConsoleComponent>();
        while (query.MoveNext(out _, out var console))
        {
            console.Dispatcher = dispatcher;
        }
    }

    public int AddCharges(int charges)
    {
        if (charges <= 0 || !TryGetDispatcher(out var dispatcher))
            return 0;

        var comp = dispatcher.Value.Comp;
        var granted = Math.Min(charges, comp.MaxCharges - comp.Charges);

        if (granted <= 0)
            return 0;

        comp.Charges += granted;
        UpdateUis();

        return granted;
    }

    private void OnUiOpened(Entity<SyndicateDropConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (!TryGetDispatcher(out var dispatcher))
            return;

        if (dispatcher.Value.Comp.TargetGrid == null || Deleted(dispatcher.Value.Comp.TargetGrid))
            TryAcquireStation(dispatcher.Value.Comp);

        UpdateUis();
    }

    private void OnSetMode(Entity<SyndicateDropConsoleComponent> ent, ref SyndicateDropConsoleSetModeMessage args)
    {
        if (!TryGetDispatcher(out var dispatcher))
            return;

        dispatcher.Value.Comp.Manual = args.Manual;
        UpdateUis();
    }

    private void OnSelectTile(Entity<SyndicateDropConsoleComponent> ent, ref SyndicateDropConsoleSelectTileMessage args)
    {
        if (!TryGetDispatcher(out var dispatcher))
            return;

        var comp = dispatcher.Value.Comp;
        var grid = GetEntity(args.Grid);

        if (!IsTargetableGrid(comp, grid, out var gridComp))
            return;

        comp.SelectedGrid = grid;
        comp.SelectedTile = _map.TileIndicesFor(grid, gridComp, new EntityCoordinates(grid, args.LocalPosition));
        UpdateUis();
    }

    private void OnNudgeTarget(Entity<SyndicateDropConsoleComponent> ent, ref SyndicateDropConsoleNudgeTargetMessage args)
    {
        if (!TryGetDispatcher(out var dispatcher))
            return;

        var comp = dispatcher.Value.Comp;
        var displayed = GetEntity(args.DisplayedGrid);

        if (!IsTargetableGrid(comp, displayed, out var displayedComp))
            return;

        if (comp.SelectedGrid != displayed || comp.SelectedTile == null)
        {
            var centre = displayedComp.LocalAABB.Center;
            comp.SelectedGrid = displayed;
            comp.SelectedTile = _map.TileIndicesFor(displayed, displayedComp, new EntityCoordinates(displayed, centre));
        }

        comp.SelectedTile += args.Delta;
        UpdateUis();
    }

    private void OnClearTarget(Entity<SyndicateDropConsoleComponent> ent, ref SyndicateDropConsoleClearTargetMessage args)
    {
        if (!TryGetDispatcher(out var dispatcher))
            return;

        dispatcher.Value.Comp.SelectedGrid = null;
        dispatcher.Value.Comp.SelectedTile = null;
        UpdateUis();
    }

    private void OnLaunch(Entity<SyndicateDropConsoleComponent> ent, ref SyndicateDropConsoleLaunchMessage args)
    {
        if (!ent.Comp.Operational || !TryGetDispatcher(out var dispatcher))
            return;

        var comp = dispatcher.Value.Comp;

        if (comp.Charges <= 0 || !TryLaunchDrop(comp, SyndicateDropMode.Manual))
            return;

        comp.Charges--;
        UpdateUis();
    }

    private void OnPodSend(Entity<SyndicateDropConsoleComponent> ent, ref SyndicateDropConsolePodSendMessage args)
    {
        if (!ent.Comp.Operational || !TryGetDispatcher(out var dispatcher))
            return;

        if (GetLinkedPad(ent.Owner) is not { } pad || !_pad.IsOperational(pad))
            return;

        TryPodDeliver(dispatcher.Value.Comp, pad);
        UpdateUis();
    }

    private Entity<SyndicateDropPadComponent>? GetLinkedPad(EntityUid console)
    {
        if (!TryComp<DeviceLinkSourceComponent>(console, out var source))
            return null;

        foreach (var sink in source.LinkedPorts.Keys)
        {
            if (TryComp<SyndicateDropPadComponent>(sink, out var pad))
                return (sink, pad);
        }

        return null;
    }

    private void UpdateUis()
    {
        if (!TryGetDispatcher(out var dispatcher))
            return;

        var comp = dispatcher.Value.Comp;
        var query = EntityQueryEnumerator<SyndicateDropConsoleComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (!_ui.IsUiOpen(uid, SyndicateDropConsoleUiKey.Key))
                continue;

            var state = new SyndicateDropConsoleUiState(
                comp.Manual,
                comp.NextDrop,
                GetNetEntity(comp.TargetGrid),
                GetNetEntity(comp.SelectedGrid),
                comp.SelectedTile,
                comp.Charges,
                comp.MaxCharges,
                new List<SyndicateDropRecord>(comp.DropHistory),
                GetLinkedPad(uid) != null,
                comp.PodCooldownEnd);

            _ui.SetUiState(uid, SyndicateDropConsoleUiKey.Key, state);
        }
    }



    private void ScheduleNextDrop(SyndicateDropDispatcherComponent comp)
    {
        var min = comp.MinInterval;
        var max = comp.MaxInterval < min ? min : comp.MaxInterval;
        comp.NextDrop = _timing.CurTime + TimeSpan.FromSeconds(_random.NextDouble(min.TotalSeconds, max.TotalSeconds));
    }

    private void TryAcquireStation(SyndicateDropDispatcherComponent comp)
    {
        if (ResolveTargetGrid() is not var (station, grid))
            return;

        if (comp.TargetGrid == grid)
            return;

        comp.TargetStation = station;
        comp.TargetGrid = grid;

        comp.SelectedGrid = null;
        comp.SelectedTile = null;
    }

    private (EntityUid Station, EntityUid Grid)? ResolveTargetGrid()
    {
        MapId? preferredMap = null;

        var consoles = EntityQueryEnumerator<SyndicateDropConsoleComponent>();
        while (consoles.MoveNext(out var uid, out _))
        {
            var map = _transform.GetMapId(uid);
            if (_station.GetStationInMap(map) == null)
                continue;

            preferredMap = map;
            break;
        }

        return _targetStation.ResolveTargetStation(preferredMap);
    }

    private bool IsTargetableGrid(SyndicateDropDispatcherComponent comp,
        EntityUid grid,
        [NotNullWhen(true)] out MapGridComponent? gridComp)
    {
        gridComp = null;

        if (comp.TargetGrid is not { } target || !TryComp(grid, out gridComp))
            return false;

        if (!_zFloors.GetFloorGrids(target).Contains(grid))
        {
            gridComp = null;
            return false;
        }

        return true;
    }



    private bool TryLaunchDrop(SyndicateDropDispatcherComponent comp, SyndicateDropMode mode)
    {
        if (!TryGetDropCoordinates(comp, mode, out var coords))
            return false;

        var before = _lookup.GetEntitiesInRange(coords.Value, PriceScanRange, PriceScanFlags);
        var drop = Spawn(comp.DropPrototype, coords.Value);
        var landed = _lookup.GetEntitiesInRange(coords.Value, PriceScanRange, PriceScanFlags);
        landed.ExceptWith(before);

        AnnounceDrop(comp, drop, mode, GetTelecrystalValue(landed));

        return true;
    }

    private bool TryPodDeliver(SyndicateDropDispatcherComponent comp, Entity<SyndicateDropPadComponent> pad)
    {
        if (_pad.GetPayload(pad) is not { } payload)
        {
            _pad.PlayError(pad);
            return false;
        }

        if (!TryGetDropCoordinates(comp, SyndicateDropMode.Pod, out var coords))
        {
            _pad.PlayError(pad);
            return false;
        }

        var traceable = _timing.CurTime < comp.PodCooldownEnd;

        _pad.PlaySend(pad);

        var xform = Transform(payload);
        _transform.Unanchor(payload, xform);
        _transform.SetCoordinates(payload, coords.Value);

        if (_whitelist.IsWhitelistPass(pad.Comp.AnchorOnArrival, payload))
        {
            if (pad.Comp.ArrivalComponents is { } components)
                EntityManager.AddComponents(payload, components, removeExisting: false);

            _transform.AnchorEntity((payload, xform));
        }

        if (comp.PodArrivalEffect is { } effect)
            Spawn(effect, coords.Value);

        AnnounceDrop(comp, payload, SyndicateDropMode.Pod, 0);

        if (traceable)
            ReportIntercept(comp, pad);

        comp.PodCooldownEnd = _timing.CurTime + TimeSpan.FromSeconds(
            _random.NextDouble(comp.MinPodCooldown.TotalSeconds, comp.MaxPodCooldown.TotalSeconds));

        return true;
    }

    private void ReportIntercept(SyndicateDropDispatcherComponent comp, EntityUid pad)
    {
        var position = GetGpsPosition(pad);
        var coordinates = Loc.GetString("syndicate-drop-console-coordinates",
            ("x", position.X),
            ("y", position.Y));
        var stamp = comp.InterceptFaxStamp.Get(_proto, EntityManager.ComponentFactory);

        var inGame = Filter.Empty().AddWhere(_gameTicker.UserHasJoinedGame);
        _chat.DispatchFilteredAnnouncement(inGame,
            Loc.GetString(comp.InterceptAnnouncement),
            playSound: true,
            announcementSound: comp.InterceptAnnouncementSound,
            colorOverride: comp.InterceptAnnouncementColor);

        var printout = new FaxPrintout(
            Loc.GetString(comp.InterceptFaxBody,
                ("location", GetSourceMapName(pad)),
                ("coordinates", coordinates)),
            Loc.GetString(comp.InterceptFaxTitle),
            null,
            null,
            stamp.StampState,
            new List<StampDisplayInfo>
            {
                new()
                {
                    StampedName = stamp.StampedName,
                    StampedColor = stamp.StampedColor,
                    StampLargeIcon = stamp.StampLargeIcon,
                },
            });

        foreach (var fax in GetCommandFaxes(comp))
        {
            _fax.Receive(fax, printout);
        }
    }

    private string GetSourceMapName(EntityUid pad)
    {
        var xform = Transform(pad);
        return xform.MapUid is { } map ? Name(map) : Loc.GetString("syndicate-drop-console-intercept-unknown-source");
    }

    private List<EntityUid> GetCommandFaxes(SyndicateDropDispatcherComponent comp)
    {
        var faxes = new List<EntityUid>();

        if (comp.TargetGrid is not { } target)
            return faxes;

        var floors = _zFloors.GetFloorGrids(target);
        var query = EntityQueryEnumerator<FaxMachineComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var fax, out var xform))
        {
            if (xform.GridUid is not { } grid || !floors.Contains(grid))
                continue;

            foreach (var keyword in comp.InterceptFaxKeywords)
            {
                if (!fax.FaxName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    continue;

                faxes.Add(uid);
                break;
            }
        }

        return faxes;
    }

    private void AnnounceDrop(SyndicateDropDispatcherComponent comp,
        EntityUid drop,
        SyndicateDropMode mode,
        int price)
    {
        var inGame = Filter.Empty().AddWhere(_gameTicker.UserHasJoinedGame);
        _chat.DispatchFilteredAnnouncement(inGame,
            Loc.GetString(comp.StationAnnouncement),
            playSound: true,
            announcementSound: comp.AnnouncementSound,
            colorOverride: comp.AnnouncementColor);

        var location = FormattedMessage.RemoveMarkupOrThrow(_navMap.GetNearestBeaconString(drop));
        var mapPosition = GetGpsPosition(drop);
        var coordinates = Loc.GetString("syndicate-drop-console-coordinates",
            ("x", mapPosition.X),
            ("y", mapPosition.Y));

        var announcer = Spawn(comp.AnnouncerPrototype, Transform(drop).Coordinates);

        _radio.SendRadioMessage(announcer,
            Loc.GetString(comp.RadioMessage, ("location", location), ("coordinates", coordinates)),
            comp.RadioChannel,
            announcer);

        QueueDel(announcer);

        RecordDrop(comp, drop, mapPosition, price, mode);
    }

    private void RecordDrop(SyndicateDropDispatcherComponent comp,
        EntityUid drop,
        Vector2i mapPosition,
        int price,
        SyndicateDropMode mode)
    {
        var xform = Transform(drop);
        var tile = Vector2i.Zero;

        if (xform.GridUid is { } grid && TryComp<MapGridComponent>(grid, out var gridComp))
            tile = _map.TileIndicesFor(grid, gridComp, xform.Coordinates);

        comp.DropHistory.Insert(0, new SyndicateDropRecord(
            GetNetEntity(xform.GridUid ?? EntityUid.Invalid),
            tile,
            mapPosition,
            price,
            mode,
            _gameTicker.RoundDuration()));

        if (comp.DropHistory.Count > comp.MaxDropHistory)
            comp.DropHistory.RemoveRange(comp.MaxDropHistory, comp.DropHistory.Count - comp.MaxDropHistory);
    }

    private Vector2i GetGpsPosition(EntityUid uid)
    {
        var position = _transform.GetMapCoordinates(uid).Position;
        return new Vector2i((int) position.X, (int) position.Y);
    }

    private bool TryGetDropCoordinates(SyndicateDropDispatcherComponent comp,
        SyndicateDropMode mode,
        [NotNullWhen(true)] out EntityCoordinates? coords)
    {
        coords = null;

        if (mode != SyndicateDropMode.Automatic &&
            comp.SelectedGrid is { } selected &&
            comp.SelectedTile is { } tile &&
            TryComp<MapGridComponent>(selected, out var selectedGrid) &&
            TryFindValidTileNear((selected, selectedGrid), tile, comp.TargetSearchRadius, out var picked))
        {
            coords = _map.GridTileToLocal(selected, selectedGrid, picked);
            return true;
        }

        return TryFindRandomStationTile(comp, out coords);
    }

    private bool TryFindRandomStationTile(SyndicateDropDispatcherComponent comp, [NotNullWhen(true)] out EntityCoordinates? coords)
    {
        coords = null;

        if (comp.TargetGrid is not { } target || Deleted(target))
            return false;

        var floors = _zFloors.GetFloorGrids(target);
        var chosen = _zFloors.GetRandomFloorGrid(target);
        floors.Remove(chosen);
        floors.Insert(0, chosen);

        foreach (var floor in floors)
        {
            if (!TryComp<MapGridComponent>(floor, out var floorComp))
                continue;

            var aabb = floorComp.LocalAABB;

            for (var i = 0; i < 10; i++)
            {
                var tile = new Vector2i(_random.Next((int) aabb.Left, (int) aabb.Right),
                    _random.Next((int) aabb.Bottom, (int) aabb.Top));

                if (!IsValidDropTile((floor, floorComp), tile))
                    continue;

                coords = _map.GridTileToLocal(floor, floorComp, tile);
                return true;
            }
        }

        return false;
    }

    private bool TryFindValidTileNear(Entity<MapGridComponent> grid, Vector2i origin, int radius, out Vector2i result)
    {
        result = origin;

        if (IsValidDropTile(grid, origin))
            return true;

        for (var ring = 1; ring <= radius; ring++)
        {
            for (var x = -ring; x <= ring; x++)
            {
                for (var y = -ring; y <= ring; y++)
                {
                    if (Math.Abs(x) != ring && Math.Abs(y) != ring)
                        continue;

                    var candidate = origin + new Vector2i(x, y);

                    if (!IsValidDropTile(grid, candidate))
                        continue;

                    result = candidate;
                    return true;
                }
            }
        }

        return false;
    }

    private bool IsValidDropTile(Entity<MapGridComponent> grid, Vector2i tile)
    {
        if (!_map.TryGetTile(grid.Comp, tile, out var selectedTile) || selectedTile.IsEmpty || _turf.IsSpace(selectedTile))
            return false;

        return !_atmosphere.IsTileSpace(grid.Owner, Transform(grid.Owner).MapUid, tile)
               && !_atmosphere.IsTileAirBlockedCached(grid.Owner, tile);
    }



    private int GetTelecrystalValue(HashSet<EntityUid> entities)
    {
        var total = 0;
        foreach (var uid in entities)
        {
            total += GetTelecrystalValue(uid);
        }

        return total;
    }

    private int GetTelecrystalValue(EntityUid uid)
    {
        var total = 0;

        if (MetaData(uid).EntityPrototype is { } proto)
            total += GetTelecrystalValue(proto.ID);

        if (TryComp<ContainerManagerComponent>(uid, out var containers))
        {
            foreach (var container in _container.GetAllContainers(uid, containers))
            {
                foreach (var contained in container.ContainedEntities)
                {
                    total += GetTelecrystalValue(contained);
                }
            }
        }

        return total;
    }

    private int GetTelecrystalValue(string prototype)
    {
        _telecrystalValues ??= BuildTelecrystalIndex();

        return _telecrystalValues.GetValueOrDefault(prototype, 0);
    }

    private Dictionary<string, int> BuildTelecrystalIndex()
    {
        var values = new Dictionary<string, int>();

        foreach (var listing in _proto.EnumeratePrototypes<ListingPrototype>())
        {
            if (listing.ProductEntity is not { } product)
                continue;

            if (!listing.Cost.TryGetValue(Telecrystal, out var cost))
                continue;

            var price = cost.Int();

            if (!values.TryGetValue(product.Id, out var existing) || price < existing)
                values[product.Id] = price;
        }

        return values;
    }

}
