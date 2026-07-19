using System.Numerics;
using Content.Server._Pirate.ZLevels.Core;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared._Pirate.ZLevels.Core.Components;
using Content.Shared._Pirate.ZLevels.Shuttles;
using Content.Shared._Pirate.ZLevels.Shuttles.Components;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Components;
using Content.Shared.Timing;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Pirate.ZLevels.Shuttles;

/// <summary>
/// Lets a shuttle console fly the whole linked deck group one z-level up or down through the
/// station's z-network. Mirrors the FTL feel (startup + exit cooldowns, sounds) but performs no
/// hyperspace jump - each deck is relocated to the adjacent z-map while its z-linkage is kept
/// intact, so the depth-0 leader stays the leader and grid sync keeps the stack rigid.
/// </summary>
public sealed class CEZShuttleTraversalSystem : EntitySystem
{
    [Dependency] private readonly CEZLevelsSystem _zLevels = default!;
    [Dependency] private readonly CEZShuttleRoofSystem _roof = default!;
    [Dependency] private readonly ShuttleConsoleSystem _console = default!;
    [Dependency] private readonly ShuttleSystem _shuttle = default!;
    [Dependency] private readonly DockingSystem _dock = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<TransformComponent> _xformQuery;

    private readonly List<EntityUid> _due = new();
    private List<Entity<MapGridComponent>> _intersecting = new();
    private readonly List<(int Depth, EntityUid Grid)> _decksScratch = new();
    private readonly HashSet<EntityUid> _ownScratch = new();

    // The fly buttons depend on the shuttle's live position (collision with grids on the adjacent
    // level), but console BUI state is otherwise only pushed on discrete events - so the buttons
    // would lag behind movement. Re-push state for open, flight-capable consoles on a short timer.
    private readonly HashSet<EntityUid> _refreshSeen = new();
    private TimeSpan _nextButtonRefresh;
    private static readonly TimeSpan ButtonRefreshInterval = TimeSpan.FromSeconds(0.5);

    // Mirror the FTL feel: a startup wind-up before the move, then an exit cooldown after.
    private static readonly TimeSpan StartupTime = TimeSpan.FromSeconds(5.5);
    private static readonly TimeSpan ExitTime = TimeSpan.FromSeconds(3);

    private readonly SoundSpecifier _startupSound =
        new SoundPathSpecifier("/Audio/Effects/Shuttle/hyperspace_begin.ogg")
        {
            Params = AudioParams.Default.WithVolume(-5f),
        };

    private readonly SoundSpecifier _arrivalSound =
        new SoundPathSpecifier("/Audio/Effects/Shuttle/hyperspace_end.ogg")
        {
            Params = AudioParams.Default.WithVolume(-5f),
        };

    public override void Initialize()
    {
        base.Initialize();

        _gridQuery = GetEntityQuery<MapGridComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();
    }

    /// <summary>
    /// Begins a traversal on <paramref name="root"/> (the depth-0 leader shuttle grid) in the given
    /// direction (+1 = up, -1 = down). Returns false if the shuttle is busy or cannot reach/clear
    /// the adjacent level.
    /// </summary>
    public bool TryStartTraversal(EntityUid root, int direction)
    {
        // Don't start a second traversal while one is already running, then check reachability.
        if (HasComp<CEZShuttleTraversalComponent>(root) || !CanReach(root, direction))
            return false;

        var comp = AddComp<CEZShuttleTraversalComponent>(root);
        comp.State = CEZTraversalState.Starting;
        comp.Direction = direction;
        comp.StateTime = StartEndTime.FromStartDuration(_timing.CurTime, StartupTime);

        _audio.PlayPvs(_startupSound, root);
        PlayForDecks(root, _startupSound);
        _console.RefreshShuttleConsoles(root);
        return true;
    }

    /// <summary>True while a traversal is in progress (used to block FTL).</summary>
    public bool IsTraversing(EntityUid root) => HasComp<CEZShuttleTraversalComponent>(root);

    /// <summary>
    /// Fills the z-traversal portion of a console's <see cref="ShuttleMapInterfaceState"/>.
    /// </summary>
    public void WriteConsoleState(EntityUid root, ShuttleMapInterfaceState state)
    {
        if (TryComp<CEZShuttleTraversalComponent>(root, out var comp))
        {
            state.ZTraversalState = comp.State;
            state.ZTraversalTime = comp.StateTime;
        }
        else
        {
            state.ZTraversalState = CEZTraversalState.Available;
            state.ZTraversalTime = default;
        }

        GetFlyOptions(root, out var canUp, out var canDown);
        state.CanFlyUp = canUp;
        state.CanFlyDown = canDown;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        // Collect first; DoTraversal reparents grids and rebuilds the roof, which we don't want to
        // do while enumerating.
        _due.Clear();
        var query = EntityQueryEnumerator<CEZShuttleTraversalComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (now >= comp.StateTime.End)
                _due.Add(uid);
        }

        foreach (var uid in _due)
        {
            if (!TryComp<CEZShuttleTraversalComponent>(uid, out var comp))
                continue;

            if (comp.State == CEZTraversalState.Starting)
            {
                // The destination z-map could have become blocked or unreachable during the startup
                // wind-up (another grid moved in, lost a thruster, etc.). Re-check right before the
                // move commits, and treat a failed (non-atomic) move the same way - abort without
                // changing state if it's no longer valid.
                if (!CanReach(uid, comp.Direction) || !DoTraversal(uid, comp.Direction))
                {
                    RemComp<CEZShuttleTraversalComponent>(uid);
                    _console.RefreshShuttleConsoles(uid);
                    continue;
                }

                comp.State = CEZTraversalState.Cooldown;
                comp.StateTime = StartEndTime.FromStartDuration(now, ExitTime);

                _audio.PlayPvs(_arrivalSound, uid);
                PlayForDecks(uid, _arrivalSound);
                _console.RefreshShuttleConsoles(uid);
            }
            else
            {
                RemComp<CEZShuttleTraversalComponent>(uid);
                _console.RefreshShuttleConsoles(uid);
            }
        }

        if (now >= _nextButtonRefresh)
        {
            _nextButtonRefresh = now + ButtonRefreshInterval;
            RefreshOpenFlightConsoles();
        }
    }

    /// <summary>
    /// Re-pushes BUI state for open shuttle consoles whose shuttle can fly a level (has a frontier),
    /// so the collision-dependent fly buttons track the shuttle's movement instead of only updating
    /// on unrelated events. Skips stations and grounded shuttles, which have no frontier to move into.
    /// </summary>
    private void RefreshOpenFlightConsoles()
    {
        _refreshSeen.Clear();

        var query = EntityQueryEnumerator<ShuttleConsoleComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.GridUid is not { } grid || !_ui.IsUiOpen(uid, ShuttleConsoleUiKey.Key))
                continue;

            var root = _shuttle.ResolveFTLShuttle(grid);
            if (!_refreshSeen.Add(root))
                continue;

            if (!TryGetRealDecks(root, out var decks) || decks.Count == 0)
                continue;

            if (!TryGetFrontier(decks, 1, out _) && !TryGetFrontier(decks, -1, out _))
                continue;

            _console.RefreshShuttleConsoles(root);
        }
    }

    /// <summary>
    /// Computes whether the shuttle can currently start flying up and/or down, for the button gate.
    /// Shares the deck list, own-grid set and footprint AABB across both directions.
    /// </summary>
    private void GetFlyOptions(EntityUid root, out bool canUp, out bool canDown)
    {
        canUp = false;
        canDown = false;

        // Don't start a second traversal while one is already running.
        if (HasComp<CEZShuttleTraversalComponent>(root))
            return;

        if (!TryGetMoveContext(root, out var decks, out var own, out var aabb))
            return;

        canUp = CanReachDirection(decks, own, aabb, 1);
        canDown = CanReachDirection(decks, own, aabb, -1);
    }

    /// <summary>
    /// Single-direction reachability used for the pre-move revalidation. Excludes the
    /// already-traversing guard so it can run during an in-progress traversal.
    /// </summary>
    private bool CanReach(EntityUid root, int direction)
    {
        return TryGetMoveContext(root, out var decks, out var own, out var aabb) &&
               CanReachDirection(decks, own, aabb, direction);
    }

    /// <summary>
    /// Direction-independent gate plus the data shared by every reachability check: shuttle enabled,
    /// not mid-FTL, has decks with a working thruster. Outputs the deck list, own-grid set, and
    /// footprint AABB so callers compute them once. An FTL drive is explicitly NOT required.
    /// </summary>
    private bool TryGetMoveContext(
        EntityUid root,
        out List<(int Depth, EntityUid Grid)> decks,
        out HashSet<EntityUid> own,
        out Box2? aabb)
    {
        decks = default!;
        own = default!;
        aabb = null;

        if (!TryComp<ShuttleComponent>(root, out var shuttle) || !shuttle.Enabled)
            return false;

        if (HasComp<FTLComponent>(root))
            return false;

        if (!TryGetRealDecks(root, out decks) || decks.Count == 0)
            return false;

        // Flying requires actual propulsion - at least one working thruster on some deck - so inert
        // grids and stations can't be flown.
        if (!HasThruster(decks))
            return false;

        own = GetOwnGrids(root);
        aabb = GetShuttleWorldAabb(decks);
        return true;
    }

    private bool CanReachDirection(
        List<(int Depth, EntityUid Grid)> decks,
        HashSet<EntityUid> own,
        Box2? aabb,
        int direction)
    {
        return TryGetFrontier(decks, direction, out var frontierMap) && !IsBlocked(own, aabb, frontierMap);
    }

    /// <summary>
    /// True if any deck has at least one working (powered + anchored + functional) thruster, linear
    /// or angular. Uses the aggregated <see cref="ShuttleComponent"/> thruster lists.
    /// </summary>
    private bool HasThruster(List<(int Depth, EntityUid Grid)> decks)
    {
        foreach (var (_, deck) in decks)
        {
            if (!TryComp<ShuttleComponent>(deck, out var shuttle))
                continue;

            if (shuttle.AngularThrusters.Count > 0)
                return true;

            foreach (var thrusters in shuttle.LinearThrusters)
            {
                if (thrusters.Count > 0)
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns the real decks of the shuttle, sorted by depth ascending, excluding the fake roof.
    /// Returns a shared scratch list to avoid per-call allocations on the refresh hot path: callers
    /// must consume the result before the next <see cref="TryGetRealDecks"/> call and never retain it.
    /// </summary>
    private bool TryGetRealDecks(EntityUid root, out List<(int Depth, EntityUid Grid)> decks)
    {
        _decksScratch.Clear();
        decks = _decksScratch;

        if (!TryComp<CEZLinkedGridComponent>(root, out var linked))
        {
            decks.Add((0, root));
            return true;
        }

        decks.Add((linked.Depth, root));
        foreach (var (depth, peer) in linked.PeerGrids)
        {
            if (HasComp<CEZShuttleRoofComponent>(peer))
                continue;

            decks.Add((depth, peer));
        }

        decks.Sort((a, b) => a.Item1.CompareTo(b.Item1));
        return true;
    }

    /// <summary>
    /// Resolves the destination "frontier" map: one level above the top real deck (up) or below the
    /// bottom real deck (down). The fake roof is ignored, so a roof sitting above the top deck never
    /// blocks flying up.
    /// </summary>
    private bool TryGetFrontier(List<(int Depth, EntityUid Grid)> decks, int direction, out EntityUid frontierMap)
    {
        frontierMap = default;

        if (decks.Count == 0)
            return false;

        var edgeDeck = direction > 0 ? decks[^1].Grid : decks[0].Grid;
        if (_xformQuery.GetComponent(edgeDeck).MapUid is not { } edgeMap)
            return false;

        if (!_zLevels.TryMapOffset(edgeMap, direction, out var target))
            return false;

        frontierMap = target.Value.Owner;
        return true;
    }

    /// <summary>
    /// Checks the destination map for any non-own grid intersecting the shuttle's footprint
    /// <paramref name="aabb"/>. Uses a map-scoped broadphase query rather than scanning every grid in
    /// the world. If the footprint couldn't be determined (<paramref name="aabb"/> is null), treats
    /// it as blocked.
    /// </summary>
    private bool IsBlocked(HashSet<EntityUid> own, Box2? aabb, EntityUid frontierMap)
    {
        if (aabb is not { } box)
            return true;

        var mapId = Comp<MapComponent>(frontierMap).MapId;

        _intersecting.Clear();
        _mapManager.FindGridsIntersecting(mapId, box, ref _intersecting, approx: true, includeMap: false);

        foreach (var grid in _intersecting)
        {
            if (!own.Contains(grid.Owner))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Every grid belonging to the shuttle (all decks plus the fake roof). Returns a shared scratch
    /// set to avoid per-call allocations: callers must consume it before the next GetOwnGrids call.
    /// </summary>
    private HashSet<EntityUid> GetOwnGrids(EntityUid root)
    {
        _ownScratch.Clear();
        _ownScratch.Add(root);
        if (TryComp<CEZLinkedGridComponent>(root, out var linked))
        {
            foreach (var (_, peer) in linked.PeerGrids)
                _ownScratch.Add(peer);
        }

        return _ownScratch;
    }

    /// <summary>Union of the decks' world AABBs, or null if none could be computed.</summary>
    private Box2? GetShuttleWorldAabb(List<(int Depth, EntityUid Grid)> decks)
    {
        Box2? union = null;
        foreach (var (_, grid) in decks)
        {
            if (!_gridQuery.TryGetComponent(grid, out var gridComp))
                continue;

            var pos = _transform.GetWorldPosition(grid);
            var rot = _transform.GetWorldRotation(grid);
            var box = new Box2Rotated(gridComp.LocalAABB.Translated(pos), rot, pos).CalcBoundingBox();
            union = union is { } u ? u.Union(box) : box;
        }

        return union;
    }

    /// <summary>
    /// Relocates every real deck to the adjacent z-map, preserving world position/rotation and
    /// leaving the z-linkage untouched. The roof is dropped first and rebuilt afterwards. Returns
    /// false (moving nothing) if any deck lacks an adjacent map, so the stack can't be split.
    /// </summary>
    private bool DoTraversal(EntityUid root, int direction)
    {
        if (!TryGetRealDecks(root, out var decks))
            return false;

        // Resolve every deck's destination up front so the move is atomic: if any deck has no
        // adjacent map we bail before relocating anything, rather than stranding part of the stack.
        var moves = new List<(EntityUid Deck, TransformComponent Xform, EntityUid TargetMap)>(decks.Count);
        foreach (var (_, deck) in decks)
        {
            var xform = _xformQuery.GetComponent(deck);
            if (xform.MapUid is not { } curMap || !_zLevels.TryMapOffset(curMap, direction, out var target))
                return false;

            moves.Add((deck, xform, target.Value.Owner));
        }

        // Suppress reentrant rebuilds for the duration of the move. Each SetCoordinates relocates a
        // grid (and aboard viewers) via a recursive ChangeMapId; letting the roof system or the
        // viewer-probe system spawn/reparent grids mid-recursion mutates the children collection
        // currently being enumerated and throws. Both rebuild themselves right after the move.
        _roof.SuppressAutoUpdates = true;
        _zLevels.SuppressViewerMapChange = true;
        try
        {
            // Drop the fake roof so it doesn't linger on the old frontier map / stale-link a peer.
            _roof.RemoveRoof(root);

            // Detach from any docks first - moving a docked grid across maps tears its docking joints
            // apart mid-relocation (the dock ports are grid children), which corrupts the transform
            // recursion and leaves grids at invalid positions. This mirrors what FTL does on launch.
            foreach (var (deck, _, _) in moves)
                _dock.UndockDocks(deck);

            foreach (var (deck, xform, targetMap) in moves)
            {
                var worldPos = _transform.GetWorldPosition(deck);
                var worldRot = _transform.GetWorldRotation(deck);

                _transform.SetCoordinates(deck, xform, new EntityCoordinates(targetMap, worldPos), rotation: worldRot);

                if (TryComp<PhysicsComponent>(deck, out var body))
                {
                    _physics.SetLinearVelocity(deck, Vector2.Zero, body: body);
                    _physics.SetAngularVelocity(deck, 0f, body: body);
                }
            }
        }
        finally
        {
            _roof.SuppressAutoUpdates = false;
            _zLevels.SuppressViewerMapChange = false;
        }

        // Rebuild the roof for the new top deck now the move is complete (no-op if there's no level
        // above it now).
        _roof.EnsureRoof(root);
        return true;
    }

    private void PlayForDecks(EntityUid root, SoundSpecifier sound)
    {
        if (!TryComp<CEZLinkedGridComponent>(root, out var linked))
            return;

        foreach (var (_, peer) in linked.PeerGrids)
        {
            if (HasComp<CEZShuttleRoofComponent>(peer))
                continue;

            _audio.PlayPvs(sound, peer);
        }
    }
}
