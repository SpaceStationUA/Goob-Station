// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Pirate.Shared.AlertLevel;
using Content.Pirate.Shared.DeepSpaceScanner;
using Content.Shared._Pirate.ZLevels.Core.EntitySystems;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Components;
using Robust.Server.Audio;
using Robust.Server.Player;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Pirate.Server.DeepSpaceScanner;

public sealed partial class DeepSpaceScannerSystem : EntitySystem
{
    [Dependency] private AlertLevelRestrictionSystem _alertRestriction = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private ITileDefinitionManager _tileDef = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ItemToggleSystem _toggle = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private CESharedZLevelsSystem _zLevels = default!;

    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<PhysicsComponent> _physicsQuery;

    private readonly Dictionary<MapId, bool> _scanMaps = new();
    private readonly HashSet<EntityUid> _actors = new();

    public override void Initialize()
    {
        base.Initialize();

        _gridQuery = GetEntityQuery<MapGridComponent>();
        _physicsQuery = GetEntityQuery<PhysicsComponent>();

        SubscribeLocalEvent<DeepSpaceScannerComponent, BoundUIOpenedEvent>(OnUIOpened);
    }

    private void OnUIOpened(Entity<DeepSpaceScannerComponent> ent, ref BoundUIOpenedEvent args)
    {
        // Reuse a current scan for late viewers; expired scans wait for the next tick.
        if (!args.UiKey.Equals(RadarConsoleUiKey.Key) ||
            ent.Comp.LastScan is not { } scan ||
            _timing.CurTime >= ent.Comp.NextScan)
        {
            return;
        }

        // A scan from another map (other deck, other sector) shows wrong pings: scan again now instead.
        if (Transform(ent).MapID != ent.Comp.LastScanMap)
        {
            ent.Comp.LastScan = null;
            ent.Comp.NextScan = _timing.CurTime;
            return;
        }

        if (_player.TryGetSessionByEntity(args.Actor, out var session))
            RaiseNetworkEvent(scan, session);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<DeepSpaceScannerComponent>();
        while (query.MoveNext(out var uid, out var scanner))
        {
            if (time < scanner.NextScan || !_toggle.IsActivated(uid))
                continue;

            _actors.Clear();
            _actors.UnionWith(_ui.GetActors(uid, RadarConsoleUiKey.Key));
            if (_actors.Count == 0)
                continue;

            // Recheck here because a new user may have a stale cached level.
            if (TryComp<AlertLevelRestrictedComponent>(uid, out var restricted))
            {
                var allowed = true;
                foreach (var actor in _actors)
                {
                    if (!_alertRestriction.IsMet(actor, restricted.Requirement))
                        allowed = false;
                }

                if (!allowed)
                {
                    scanner.LastScan = null;
                    _alertRestriction.Revoke((uid, restricted));
                    continue;
                }
            }

            scanner.NextScan = time + scanner.ScanInterval;
            var scan = Scan((uid, scanner), time);
            scanner.LastScan = scan;
            scanner.LastScanMap = Transform(uid).MapID;
            _appearance.SetData(uid, DeepSpaceScannerVisuals.ScanStart, time);

            foreach (var actor in _actors)
            {
                if (_player.TryGetSessionByEntity(actor, out var session))
                    RaiseNetworkEvent(scan, session);
            }

            if (scan.Pings.Count > 0)
            {
                _audio.PlayPvs(scanner.ScanSound, uid);
            }
            else
            {
                foreach (var actor in _actors)
                {
                    _audio.PlayEntity(scanner.EmptyScanSound, actor, uid);
                }
            }
        }
    }

    public DeepSpaceScanEvent Scan(Entity<DeepSpaceScannerComponent> ent, TimeSpan time)
    {
        var scanner = ent.Comp;
        var xform = Transform(ent);
        var origin = _xform.GetWorldPosition(xform);
        var pings = new List<DeepSpacePing>();
        var scan = new DeepSpaceScanEvent(GetNetEntity(ent), origin, time, pings);

        if (xform.MapUid is not { } mapUid)
            return scan;

        var rangeSquared = scanner.Range * scanner.Range;

        _scanMaps.Clear();
        _scanMaps[xform.MapID] = true;
        for (var offset = -1; offset <= 1; offset += 2)
        {
            if (_zLevels.TryMapOffset(mapUid, offset, out var adjacent) &&
                TryComp<MapComponent>(adjacent.Value, out var adjacentMap))
            {
                _scanMaps.TryAdd(adjacentMap.MapId, false);
            }
        }

        var gridQuery = EntityQueryEnumerator<IFFComponent, MapGridComponent, TransformComponent>();
        while (gridQuery.MoveNext(out var gridUid, out var iff, out _, out var gridXform))
        {
            if ((iff.Flags & IFFFlags.Hide) == 0 ||
                gridXform.MapUid == gridUid || // a planet map is not a ship
                !_scanMaps.TryGetValue(gridXform.MapID, out var ownLevel))
            {
                continue;
            }

            var localCenter = _physicsQuery.TryComp(gridUid, out var body) ? body.LocalCenter : Vector2.Zero;
            var center = Vector2.Transform(localCenter, _xform.GetWorldMatrix(gridXform));
            if ((center - origin).LengthSquared() > rangeSquared)
                continue;

            pings.Add(new DeepSpacePing(center, true, !ownLevel));
        }

        var mobQuery = EntityQueryEnumerator<MobStateComponent, TransformComponent>();
        while (mobQuery.MoveNext(out var mobUid, out var mobState, out var mobXform))
        {
            if (mobState.CurrentState == MobState.Dead ||
                _actors.Contains(mobUid) ||
                !_scanMaps.TryGetValue(mobXform.MapID, out var ownLevel))
            {
                continue;
            }

            var position = _xform.GetWorldPosition(mobXform);
            if ((position - origin).LengthSquared() > rangeSquared || !IsOffStation(scanner, mobXform, position))
                continue;

            pings.Add(new DeepSpacePing(position, false, !ownLevel));
        }

        return scan;
    }

    private bool IsOffStation(DeepSpaceScannerComponent scanner, TransformComponent xform, Vector2 worldPosition)
    {
        if (xform.GridUid is not { } gridUid || !_gridQuery.TryComp(gridUid, out var grid))
            return true;

        if (!_map.TryGetTileRef(gridUid, grid, worldPosition, out var tileRef) || tileRef.Tile.IsEmpty)
            return true;

        return scanner.ExteriorTiles.Contains(_tileDef[tileRef.Tile.TypeId].ID);
    }
}
