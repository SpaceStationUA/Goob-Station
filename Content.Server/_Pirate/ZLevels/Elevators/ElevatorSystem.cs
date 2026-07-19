using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Content.Server.Body.Systems;
using Content.Shared._Pirate.ZLevels.Core.Components;
using Content.Shared._Pirate.ZLevels.Core.EntitySystems;
using Content.Shared._Pirate.ZLevels.Elevators;
using Content.Shared._Pirate.ZLevels.Elevators.Components;
using Content.Shared.Audio;
using Content.Shared.Body.Components;
using Content.Shared.Damage;
using Content.Shared.Doors;
using Content.Shared.Examine;
using Content.Shared.Maps;
using Content.Shared.Tag;
using Content.Shared.Wall;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Server._Pirate.ZLevels.Elevators;

/// <summary>
/// A cab whose floor tiles and riders travel between z-network decks, leaving an open shaft behind.
/// </summary>
public sealed partial class ElevatorSystem : EntitySystem
{
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDef = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly CESharedZLevelsSystem _zLevels = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAmbientSoundSystem _ambient = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    /// <summary>Played when a call/move request is refused.</summary>
    private static readonly SoundSpecifier DenySound = new SoundPathSpecifier("/Audio/Machines/buzz-two.ogg");

    /// <summary>Wall lights carry this tag but lack <see cref="WallMountComponent"/>.</summary>
    private static readonly ProtoId<TagPrototype> WallLightTag = "WallLight";

    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<TransformComponent> _xformQuery;

    public override void Initialize()
    {
        base.Initialize();

        _gridQuery = GetEntityQuery<MapGridComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();

        SubscribeLocalEvent<ElevatorControllerComponent, MapInitEvent>(OnControllerMapInit);
        SubscribeLocalEvent<ElevatorControllerComponent, ComponentShutdown>(OnControllerShutdown);
        SubscribeLocalEvent<ElevatorIndicatorComponent, ExaminedEvent>(OnIndicatorExamine);
        SubscribeLocalEvent<ElevatorDoorComponent, BeforeDoorAutoCloseEvent>(OnDoorAutoClose);

        InitializeUi();
    }

    private void OnControllerMapInit(Entity<ElevatorControllerComponent> ent, ref MapInitEvent args)
    {
        // Skip while paused (mapping) — would bake shaft tiles/speakers into the save. Update() retries
        // once the z-network links this deck's grid.
        if (Paused(ent))
            return;
        TryInitialize(ent);
    }

    private void OnControllerShutdown(Entity<ElevatorControllerComponent> ent, ref ComponentShutdown args)
    {
        if (!TerminatingOrDeleted(ent.Comp.MusicSpeaker))
            QueueDel(ent.Comp.MusicSpeaker);
        ent.Comp.MusicSpeaker = EntityUid.Invalid;
    }

    /// <summary>
    /// Caches footprint, cab floor tile and served decks. Requires the deck grid to be linked into a
    /// z-network (<see cref="CEZLinkedGridComponent"/>); returns false until then.
    /// </summary>
    private bool TryInitialize(Entity<ElevatorControllerComponent> ent)
    {
        var comp = ent.Comp;
        if (comp.Initialized)
            return true;

        var xform = Transform(ent);
        if (xform.GridUid is not { } gridUid || !_gridQuery.TryComp(gridUid, out var grid))
            return false;

        // Linkage may be applied after map-init.
        if (!TryComp<CEZLinkedGridComponent>(gridUid, out var linked))
            return false;

        // Clamp misconfigured geometry/timing rather than fail (failing would retry and re-log each tick).
        if (comp.Width <= 0 || comp.Height <= 0 || comp.PerDeckTravelSeconds <= 0f)
        {
            Log.Warning($"Elevator '{comp.ElevatorId}' has invalid geometry/timing " +
                        $"(width={comp.Width}, height={comp.Height}, perDeckTravelSeconds={comp.PerDeckTravelSeconds}); clamping to minimums.");
            comp.Width = Math.Max(1, comp.Width);
            comp.Height = Math.Max(1, comp.Height);
            if (comp.PerDeckTravelSeconds <= 0f)
                comp.PerDeckTravelSeconds = 1f;
        }

        comp.AnchorGrid = gridUid;
        comp.OriginTile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);

        // Capture the home-deck floor as the cab pattern (read-only). No tiles are written at init, so
        // the mapper's layout is left as authored until the cab actually travels.
        comp.ResolvedCabFloorTiles.Clear();
        Tile? cabOverride = comp.CabFloorTile == null ? null : new Tile(_tileDef[comp.CabFloorTile].TileId);
        comp.ResolvedShaftFloorTile = new Tile(_tileDef[comp.ShaftFloorTile].TileId);
        foreach (var tile in FootprintTiles(comp))
        {
            if (!_map.TryGetTileRef(gridUid, grid, tile, out var tileRef))
                continue;

            var offset = tile - comp.OriginTile;
            comp.ResolvedCabFloorTiles[offset] = cabOverride ?? (tileRef.Tile.IsEmpty ? comp.ResolvedShaftFloorTile : tileRef.Tile);
        }

        comp.CurrentDepth = linked.Depth;
        comp.TargetDepth = linked.Depth;

        DiscoverServedDepths(ent, linked.Depth);

        comp.Initialized = true;

        // One looping speaker that rides with the cab (see StepCab); ambient sound is per-map, so this
        // is what riders hear without per-deck speakers.
        if (TryGetDeckGrid(comp, comp.CurrentDepth, out var startGrid, out var startGridComp))
        {
            comp.MusicSpeaker = Spawn(comp.MusicSpeakerProto, FootprintCenter(comp, startGrid, startGridComp));
            _ambient.SetRange(comp.MusicSpeaker, MathF.Max(4f, MathF.Max(comp.Width, comp.Height)));
            _ambient.SetAmbience(comp.MusicSpeaker, true);
        }

        // Cab starts on its deck: open that floor's door, shut the rest.
        OpenDoorOnDeck(comp.ElevatorId, comp.CurrentDepth);
        UpdateIndicators(comp.ElevatorId, DisplayFloor(comp, comp.CurrentDepth), ElevatorDirection.Idle);
        return true;
    }

    /// <summary>Walks the z-network up and down from the start deck while the footprint is an open shaft.</summary>
    private void DiscoverServedDepths(Entity<ElevatorControllerComponent> ent, int startDepth)
    {
        var comp = ent.Comp;
        var served = new HashSet<int> { startDepth };

        for (var d = startDepth + 1; ShaftOpenAtDepth(comp, d); d++)
            served.Add(d);
        for (var d = startDepth - 1; ShaftOpenAtDepth(comp, d); d--)
            served.Add(d);

        comp.ServedDepths = served.OrderBy(d => d).ToList();
    }

    /// <summary>
    /// True if a grid exists at <paramref name="depth"/> and every footprint tile is empty or the shaft
    /// floor tile (the mapper may build the shaft either way).
    /// </summary>
    private bool ShaftOpenAtDepth(ElevatorControllerComponent comp, int depth)
    {
        if (!TryGetDeckGrid(comp, depth, out var gridUid, out var grid))
            return false;

        foreach (var tile in FootprintTiles(comp))
        {
            if (!_map.TryGetTileRef(gridUid, grid, tile, out var tileRef))
                return false;
            if (!tileRef.Tile.IsEmpty && tileRef.Tile.TypeId != comp.ResolvedShaftFloorTile.TypeId)
                return false;
        }

        return true;
    }

    /// <summary>Resolves the grid for a deck depth via the controller's linked-grid peer map.</summary>
    private bool TryGetDeckGrid(ElevatorControllerComponent comp, int depth, out EntityUid gridUid, out MapGridComponent grid)
    {
        gridUid = EntityUid.Invalid;
        grid = default!;

        if (!TryComp<CEZLinkedGridComponent>(comp.AnchorGrid, out var linked))
            return false;

        if (depth == linked.Depth)
            gridUid = comp.AnchorGrid;
        else if (!linked.PeerGrids.TryGetValue(depth, out gridUid))
            return false;

        if (!_gridQuery.TryComp(gridUid, out var gridComp))
            return false;

        grid = gridComp;
        return true;
    }

    private IEnumerable<Vector2i> FootprintTiles(ElevatorControllerComponent comp)
    {
        for (var i = 0; i < comp.Width; i++)
        for (var j = 0; j < comp.Height; j++)
            yield return comp.OriginTile + new Vector2i(i, j);
    }

    /// <summary>Lowest served deck. It keeps a solid floor when vacated; decks above reopen to shaft.</summary>
    private int BasementDepth(ElevatorControllerComponent comp)
        => comp.ServedDepths.Count > 0 ? comp.ServedDepths[0] : comp.CurrentDepth;

    /// <summary>
    /// Lays the cab floor pattern on the deck it now occupies, or clears it when vacated. Only the
    /// basement keeps a solid floor; decks above reopen to empty space so the crew falls down the shaft.
    /// </summary>
    private void SetFootprintTiles(ElevatorControllerComponent comp, EntityUid gridUid, MapGridComponent grid, int depth, bool cabPresent)
    {
        var vacatedTile = depth == BasementDepth(comp) ? comp.ResolvedShaftFloorTile : Tile.Empty;
        foreach (var tile in FootprintTiles(comp))
        {
            var offset = tile - comp.OriginTile;
            var newTile = cabPresent && comp.ResolvedCabFloorTiles.TryGetValue(offset, out var cabTile)
                ? cabTile
                : vacatedTile;

            _map.SetTile(gridUid, grid, tile, newTile);
        }
    }

    /// <summary>Local coordinates at the centre of the cab footprint on a given deck grid (for sound).</summary>
    private EntityCoordinates FootprintCenter(ElevatorControllerComponent comp, EntityUid gridUid, MapGridComponent grid)
    {
        var center = comp.OriginTile + new Vector2i(comp.Width / 2, comp.Height / 2);
        return TileCenter(gridUid, grid, center);
    }

    private EntityCoordinates TileCenter(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        var local = (new Vector2(tile.X, tile.Y) + new Vector2(0.5f, 0.5f)) * grid.TileSize;
        return new EntityCoordinates(gridUid, local);
    }

    /// <summary>Spawns the descent telegraph on every footprint tile of the given deck.</summary>
    private void SpawnTravelWarnings(ElevatorControllerComponent comp, int depth)
    {
        if (!TryGetDeckGrid(comp, depth, out var gridUid, out var grid))
            return;

        foreach (var tile in FootprintTiles(comp))
        {
            var warning = Spawn(comp.TravelWarningProto, TileCenter(gridUid, grid, tile));
            if (TryComp<TimedDespawnComponent>(warning, out var despawn))
                despawn.Lifetime = comp.PerDeckTravelSeconds;
        }
    }

    /// <summary>If the cab's next step is downward, telegraph the destination floor (SS13 lift_travel).</summary>
    private void MaybeWarnNextDescent(ElevatorControllerComponent comp)
    {
        if (!comp.WarnsOnDownMovement)
            return;
        var dir = Math.Sign(comp.TargetDepth - comp.CurrentDepth);
        if (dir < 0)
            SpawnTravelWarnings(comp, comp.CurrentDepth + dir);
    }

    /// <summary>Send the cab to a served depth.</summary>
    public bool RequestMove(string elevatorId, int targetDepth)
    {
        if (!TryGetController(elevatorId, out var ent))
            return false;

        var comp = ent.Value.Comp;
        if (!comp.Initialized || comp.Moving)
            return false;
        if (!comp.ServedDepths.Contains(targetDepth) || targetDepth == comp.CurrentDepth)
            return false;

        comp.TargetDepth = targetDepth;
        comp.Moving = true;
        comp.NextStepTime = _timing.CurTime + TimeSpan.FromSeconds(comp.PerDeckTravelSeconds);

        CloseAllDoors(comp.ElevatorId);
        UpdateIndicators(comp.ElevatorId, DisplayFloor(comp, comp.CurrentDepth), ElevatorDirectionFor(comp));
        UpdatePanelUis(comp.ElevatorId);
        MaybeWarnNextDescent(comp);
        PlayLegStartSound(comp);
        return true;
    }

    private bool TryGetController(string elevatorId, [NotNullWhen(true)] out Entity<ElevatorControllerComponent>? ent)
    {
        var query = AllEntityQuery<ElevatorControllerComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.ElevatorId != elevatorId)
                continue;
            ent = (uid, comp);
            return true;
        }

        ent = null;
        return false;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = AllEntityQuery<ElevatorControllerComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            // Stay inert on paused maps. Update runs globally, but mapping maps load paused and
            // uninitialized; initializing here would bake shaft tiles/speakers into the save.
            if (Paused(uid))
                continue;

            if (!comp.Initialized && !TryInitialize((uid, comp)))
                continue;

            if (!comp.Moving || now < comp.NextStepTime)
                continue;

            StepCab((uid, comp));

            if (comp.CurrentDepth == comp.TargetDepth)
            {
                FinishMove((uid, comp));
            }
            else
            {
                comp.NextStepTime = now + TimeSpan.FromSeconds(comp.PerDeckTravelSeconds);
                MaybeWarnNextDescent(comp);
                PlayLegStartSound(comp);
            }
        }
    }

    /// <summary>Moves the cab one deck toward its target: crush, lay floor, carry riders, restore the origin shaft floor.</summary>
    private void StepCab(Entity<ElevatorControllerComponent> ent)
    {
        var comp = ent.Comp;
        var dir = Math.Sign(comp.TargetDepth - comp.CurrentDepth); // +1 up, -1 down
        if (dir == 0)
            return;

        var fromDepth = comp.CurrentDepth;
        var toDepth = comp.CurrentDepth + dir;

        if (!TryGetDeckGrid(comp, fromDepth, out var fromGridUid, out var fromGrid) ||
            !TryGetDeckGrid(comp, toDepth, out var toGridUid, out var toGrid))
        {
            // Lost a deck mid-travel — abort gracefully.
            comp.TargetDepth = comp.CurrentDepth;
            return;
        }

        // 1) Gather riders (anchored structures included) before anything changes.
        var riders = GatherFootprintEntities(comp, fromGridUid, fromGrid);

        // 2) Crush whatever is sitting in the destination footprint.
        CrushDestination(comp, toGridUid, toGrid);

        // 3) Lay the destination floor first, so arriving anchored parts have a tile to re-anchor onto.
        SetFootprintTiles(comp, toGridUid, toGrid, toDepth, true);

        // 4) Carry riders onto the destination deck, preserving grid-local position so each lands over
        //    the same footprint tile. We reparent directly (not via TryMove, whose stair-exit nudge would
        //    drift anchored parts off the footprint). Anchored parts are unanchored, moved, re-anchored.
        //    Must run BEFORE step 5: clearing an upper deck unanchors riders still on it, and a physics-less
        //    platform that arrives unanchored can't be gathered on the next leg.
        foreach (var rider in riders)
        {
            if (!_xformQuery.TryComp(rider, out var rxform))
                continue;

            var wasAnchored = rxform.Anchored;
            if (wasAnchored)
                _transform.Unanchor(rider, rxform);

            var localPos = Vector2.Transform(_transform.GetWorldPosition(rider), _transform.GetInvWorldMatrix(fromGridUid));
            _zLevels.TeleportToZLevelCoordinates(rider, new EntityCoordinates(toGridUid, localPos), toDepth, dir);

            if (wasAnchored && _xformQuery.TryComp(rider, out var movedXform))
                _transform.AnchorEntity(rider, movedXform);
        }

        // 5) Riders have left: vacate the origin deck (basement keeps its floor, upper decks reopen).
        SetFootprintTiles(comp, fromGridUid, fromGrid, fromDepth, false);

        comp.CurrentDepth = toDepth;

        // Carry the speaker so the music follows the cab.
        if (!TerminatingOrDeleted(comp.MusicSpeaker))
            _zLevels.TeleportToZLevelCoordinates(comp.MusicSpeaker, FootprintCenter(comp, toGridUid, toGrid), toDepth, dir);

        UpdateIndicators(comp.ElevatorId, DisplayFloor(comp, comp.CurrentDepth), dir > 0 ? ElevatorDirection.Up : ElevatorDirection.Down);
    }

    private void FinishMove(Entity<ElevatorControllerComponent> ent)
    {
        var comp = ent.Comp;
        comp.Moving = false;

        OpenDoorOnDeck(comp.ElevatorId, comp.CurrentDepth);
        PlayArrivalPing(comp, comp.CurrentDepth);

        UpdateIndicators(comp.ElevatorId, DisplayFloor(comp, comp.CurrentDepth), ElevatorDirection.Idle);
        UpdatePanelUis(comp.ElevatorId);
    }

    /// <summary>Lift sound when a leg starts — on the cab's level and the levels directly above/below it.</summary>
    private void PlayLegStartSound(ElevatorControllerComponent comp)
    {
        if (comp.TravelSound == null)
            return;

        foreach (var depth in new[] { comp.CurrentDepth, comp.CurrentDepth + 1, comp.CurrentDepth - 1 })
        {
            if (TryGetDeckGrid(comp, depth, out var gridUid, out var grid))
                _audio.PlayPvs(comp.TravelSound, FootprintCenter(comp, gridUid, grid));
        }
    }

    /// <summary>Arrival ping, played from a linked control (call button / panel) on that floor.</summary>
    private void PlayArrivalPing(ElevatorControllerComponent comp, int depth)
    {
        if (comp.ArriveSound == null)
            return;

        if (TryGetControlOnDeck(comp.ElevatorId, depth, out var control))
            _audio.PlayPvs(comp.ArriveSound, control);
        else if (TryGetDeckGrid(comp, depth, out var gridUid, out var grid))
            _audio.PlayPvs(comp.ArriveSound, FootprintCenter(comp, gridUid, grid));
    }

    /// <summary>Finds a call button (preferred) or panel of this elevator on the given deck.</summary>
    private bool TryGetControlOnDeck(string elevatorId, int depth, out EntityUid control)
    {
        var buttons = AllEntityQuery<ElevatorCallButtonComponent>();
        while (buttons.MoveNext(out var uid, out var button))
        {
            if (button.ElevatorId == elevatorId && TryGetEntityDeckDepth(uid, out var d) && d == depth)
            {
                control = uid;
                return true;
            }
        }

        var panels = AllEntityQuery<ElevatorPanelComponent>();
        while (panels.MoveNext(out var uid, out var panel))
        {
            if (panel.ElevatorId == elevatorId && TryGetEntityDeckDepth(uid, out var d) && d == depth)
            {
                control = uid;
                return true;
            }
        }

        control = default;
        return false;
    }

    /// <summary>Crushes living mobs / damages structures standing in the destination shaft footprint.</summary>
    private void CrushDestination(ElevatorControllerComponent comp, EntityUid gridUid, MapGridComponent grid)
    {
        var victims = GatherFootprintEntities(comp, gridUid, grid);
        foreach (var victim in victims)
        {
            if (comp.ViolentLanding && HasComp<BodyComponent>(victim))
            {
                _body.GibBody(victim);
                continue;
            }

            if (comp.CrushDamage.DamageDict.Count > 0 && HasComp<DamageableComponent>(victim))
                _damageable.TryChangeDamage(victim, comp.CrushDamage, ignoreResistances: true);
        }
    }

    /// <summary>Mobs, items and anchored structures standing on the cab footprint (see <see cref="CanRideCab"/>).</summary>
    private List<EntityUid> GatherFootprintEntities(ElevatorControllerComponent comp, EntityUid gridUid, MapGridComponent grid)
    {
        var footprint = new HashSet<Vector2i>(FootprintTiles(comp));
        var result = new List<EntityUid>();
        var seen = new HashSet<EntityUid>();

        foreach (var tile in footprint)
        {
            if (!_map.TryGetTileRef(gridUid, grid, tile, out var tileRef))
                continue;

            var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
            while (anchored.MoveNext(out var anchoredUid))
            {
                if (anchoredUid is not { } anchoredEnt)
                    continue;
                if (!seen.Add(anchoredEnt))
                    continue;
                if (!CanRideCab(anchoredEnt, comp))
                    continue;

                var anchoredTile = _map.WorldToTile(gridUid, grid, _transform.GetWorldPosition(anchoredEnt));
                if (!footprint.Contains(anchoredTile))
                    continue;

                result.Add(anchoredEnt);
            }

            // Sundries is needed for loose dropped items (non-colliding, not in the dynamic/static trees).
            foreach (var uid in _lookup.GetEntitiesInTile(tileRef, LookupFlags.Dynamic | LookupFlags.Static | LookupFlags.Sundries))
            {
                if (!seen.Add(uid))
                    continue;
                if (!CanRideCab(uid, comp))
                    continue;

                // Restrict to entities whose tile is on the footprint (the lookup also returns overlaps).
                var entTile = _map.WorldToTile(gridUid, grid, _transform.GetWorldPosition(uid));
                if (!footprint.Contains(entTile))
                    continue;

                result.Add(uid);
            }
        }

        return result;
    }

    /// <summary>
    /// False for things that belong to the deck, not the cab: grids, the controller, the speaker, and
    /// wall-attached fixtures (<see cref="WallMountComponent"/> or the <c>WallLight</c> tag). Everything
    /// else on the footprint rides the cab.
    /// </summary>
    private bool CanRideCab(EntityUid uid, ElevatorControllerComponent comp)
    {
        if (!_xformQuery.TryComp(uid, out var xform))
            return false;

        // Only move hierarchy roots. Children (notably ghosts parented to followed mobs) are already
        // carried recursively with their parent; moving them again detaches them at stale cab coordinates.
        if (xform.ParentUid != xform.GridUid && xform.ParentUid != xform.MapUid)
            return false;

        if (HasComp<MapGridComponent>(uid) ||
            HasComp<ElevatorControllerComponent>(uid) ||
            HasComp<WallMountComponent>(uid) ||
            _tag.HasTag(uid, WallLightTag) ||
            uid == comp.MusicSpeaker)
            return false;

        return true;
    }

    private static ElevatorDirection ElevatorDirectionFor(ElevatorControllerComponent comp)
    {
        if (!comp.Moving || comp.TargetDepth == comp.CurrentDepth)
            return ElevatorDirection.Idle;
        return comp.TargetDepth > comp.CurrentDepth ? ElevatorDirection.Up : ElevatorDirection.Down;
    }
}
