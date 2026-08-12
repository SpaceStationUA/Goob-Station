/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Numerics;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Server._Pirate.ZLevels.Core;
using Content.Server._Pirate.ZLevels.Shuttles;
using Content.Shared._FarHorizons.Planets;
using Content.Shared._FarHorizons.Planets.Descent;
using Content.Shared._FarHorizons.StarSystem;
using Content.Shared._FarHorizons.StarSystem.Helpers;
using Content.Shared._Pirate.ZLevels.Core.Components;
using Content.Shared.Damage;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Components;
using Content.Shared.Stunnable;
using Content.Shared.Timing;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._FarHorizons.Planets;

/// <summary>
/// Runs the planet descent sequence. The console spinup theatre hands over here once its
/// clock elapses; the sequence then moves the docked set onto a bare pseudo-map
/// (Descending → Vanishing), warps it into the planet's z-network ground layer, and
/// fades in (Arriving). Ascents run the same machine in reverse back to the planet's
/// space map. The whole docked set rides along, so multiple ships can independently
/// descend onto the same planet surface.
/// </summary>
public sealed partial class CEDescentSystem : CESharedDescentSystem
{
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly ShuttleSystem _shuttle = default!;
    [Dependency] private readonly ThrusterSystem _thruster = default!;
    [Dependency] private readonly DockingSystem _dockSystem = default!;
    [Dependency] private readonly ShuttleConsoleSystem _console = default!;
    [Dependency] private readonly CEZLevelsSystem _zLevels = default!;
    [Dependency] private readonly CEPlanetSystem _planetSystem = default!;
    [Dependency] private readonly CEZShuttleRoofSystem _roof = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private EntityQuery<MapGridComponent> _gridQuery = default!;
    private EntityQuery<PhysicsComponent> _physQuery = default!;
    private EntityQuery<TransformComponent> _xformQuery = default!;

    private const float MinAscentOrbitDistance = 32f;
    private const float MaxAscentOrbitDistance = 96f;

    /// <summary>Per-shuttle cached console flags so the periodic refresh only pushes state on change.</summary>
    private readonly Dictionary<EntityUid, (bool CanDescend, bool CanAscend)> _consoleFlagsCache = new();

    private readonly HashSet<EntityUid> _thrusterDockScan = new();

    public override void Initialize()
    {
        base.Initialize();

        _gridQuery = GetEntityQuery<MapGridComponent>();
        _physQuery = GetEntityQuery<PhysicsComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();

        SubscribeLocalEvent<ShuttleConsoleComponent, CEDescentRequestMessage>(OnConsoleDescentRequest);
        SubscribeLocalEvent<ThrusterComponent, DamageChangedEvent>(OnThrusterDamaged);
        SubscribeLocalEvent<CEDescentComponent, ComponentShutdown>(OnDescentShutdown);
    }

    /// <summary>
    /// Safety net: if the sequence dies on any path (warp aborted, map torn down, component
    /// removed out from under it) the ship must not stay disabled — a disabled shuttle silently
    /// blocks FTL and flight.
    /// </summary>
    private void OnDescentShutdown(Entity<CEDescentComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.GridSet.Count > 0)
            ReenableShuttles(ent.Comp.GridSet);
    }

    private void OnConsoleDescentRequest(Entity<ShuttleConsoleComponent> ent, ref CEDescentRequestMessage args)
    {
        if (!_xformQuery.TryGetComponent(ent.Owner, out var consoleXform) ||
            consoleXform.GridUid is not { } gridUid)
            return;

        var root = _shuttle.ResolveFTLShuttle(gridUid);

        // Ascents target the planet that owns the ground layer the shuttle is parked on.
        if (args.Ascent)
        {
            if (_xformQuery.GetComponent(root).MapUid is not { } mapUid ||
                !_planetSystem.TryGetPlanetForMap(mapUid, out var planet) ||
                !TryStartAscent((root, _gridQuery.GetComponent(root)), planet))
            {
                _popup.PopupClient(Loc.GetString("ce-descent-request-denied"), ent.Owner, ent.Owner);
            }

            return;
        }

        // Resolve the best target: the closest landable planet on this map within its zone.
        string? denyReason = null;
        if (TryGetClosestPlanet(root, out var descentPlanet, out _) &&
            TryBeginDescent(root, descentPlanet, out denyReason))
        {
            // The ship is committed to the charge: lock the pilots for its duration.
            var spinup = EnsureComp<CEDescentSpinupComponent>(root);
            AddComp<PreventPilotComponent>(root);
            spinup.PilotLocked.Add(root);
            Dirty(root, spinup);

            RefreshConsoles(root);
            return;
        }

        if (denyReason != null)
            _popup.PopupClient(Loc.GetString(denyReason), ent.Owner, ent.Owner);
    }

    /// <summary>
    /// Collects the whole docked chain of <paramref name="root"/> (peers only — the root
    /// itself is not added). Equivalent of upstream's shuttle helper, which this fork lacks.
    /// </summary>
    private void GetAllDockedShuttles(EntityUid root, HashSet<EntityUid> output)
    {
        var pending = new Queue<EntityUid>();
        pending.Enqueue(root);

        while (pending.TryDequeue(out var grid))
        {
            var query = AllEntityQuery<DockingComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var dock, out var xform))
            {
                if (!dock.Docked || dock.DockedWith == null || xform.GridUid != grid)
                    continue;

                if (!_xformQuery.TryGetComponent(dock.DockedWith.Value, out var otherXform) ||
                    otherXform.GridUid is not { } otherGrid)
                    continue;

                if (otherGrid == root || !output.Add(otherGrid))
                    continue;

                pending.Enqueue(otherGrid);
            }
        }
    }

    /// <summary>
    /// An engine took a hit. If the charging grid owns it, the charge aborts
    /// violently: the spinup is stripped, the drive is stunned into a respool and the
    /// grid stays pilot-locked with cold thrusters until it ends (see
    /// <see cref="AbortCharge"/>).
    /// </summary>
    private void OnThrusterDamaged(Entity<ThrusterComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.DamageDelta == null)
            return;

        if (!ent.Comp.Enabled)
            return;

        if (Transform(ent).GridUid is not { } thrusterGrid)
            return;

        if (HasComp<CEDescentSpinupComponent>(thrusterGrid))
        {
            AbortCharge(thrusterGrid);
            return;
        }

        // The whole docked set charges together, so any member's engines count.
        _thrusterDockScan.Clear();
        GetAllDockedShuttles(thrusterGrid, _thrusterDockScan);
        foreach (var member in _thrusterDockScan)
        {
            if (HasComp<CEDescentSpinupComponent>(member))
            {
                AbortCharge(member);
                return;
            }
        }
    }

    /// <summary>
    /// Kills a running charge and discharges the drive violently. The grid gets a
    /// <see cref="CEDescentStunnedComponent"/> blocking re-requests until
    /// <see cref="CESharedDescentSystem.DriveRespoolTime"/> elapses, and everyone
    /// aboard is knocked flat for <see cref="CESharedDescentSystem.DischargeStunTime"/>.
    /// Only the charging grid itself is hit — everything docked was already kicked
    /// off when the charge began.
    /// </summary>
    public void AbortCharge(EntityUid uid)
    {
        if (!TryComp<CEDescentSpinupComponent>(uid, out var spinup))
            return;

        if (!TerminatingOrDeleted(uid))
        {
            var stunned = EnsureComp<CEDescentStunnedComponent>(uid);
            stunned.Start = Timing.CurTime;
            stunned.End = Timing.CurTime + DriveRespoolTime;
            stunned.PilotLocked = HasComp<PreventPilotComponent>(uid);
            Dirty(uid, stunned);
        }

        RemComp<CEDescentSpinupComponent>(uid);

        // Everyone standing on the grid gets thrown off their feet.
        var mobs = EntityQueryEnumerator<MobStateComponent, TransformComponent>();
        while (mobs.MoveNext(out var mobUid, out _, out var xform))
        {
            if (xform.GridUid == uid)
                _stun.TryAddParalyzeDuration(mobUid, DischargeStunTime);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = Timing.CurTime;

        // Console spinup theatre → hand over to the descent proper once it elapses.
        var spinups = EntityQueryEnumerator<CEDescentSpinupComponent, MapGridComponent>();
        while (spinups.MoveNext(out var uid, out var spinup, out var grid))
        {
            if (now < spinup.End)
                continue;

            RemCompDeferred<CEDescentSpinupComponent>(uid);
            foreach (var locked in spinup.PilotLocked)
            {
                if (TryComp<PreventPilotComponent>(locked, out _))
                    RemComp<PreventPilotComponent>(locked);
            }

            if (TryComp<CEPlanetComponent>(spinup.Planet, out var planet))
                TryStartDescent((uid, grid), (spinup.Planet, planet));
        }

        // Respool stuns tick down and clear themselves. Until they do, the drive is
        // dead: pilot input is locked (PreventPilot, owned by the stun) and the
        // thrusters/gyros are re-forced cold every tick.
        var stuns = EntityQueryEnumerator<CEDescentStunnedComponent>();
        while (stuns.MoveNext(out var uid, out var stunned))
        {
            if (now >= stunned.End)
            {
                RemCompDeferred<CEDescentStunnedComponent>(uid);
                if (stunned.PilotLocked)
                    RemComp<PreventPilotComponent>(uid);
                continue;
            }

            if (TryComp<ShuttleComponent>(uid, out var shuttle))
            {
                _thruster.DisableLinearThrusters(shuttle);
                _thruster.SetAngularThrust(shuttle, false);
            }
        }

        // Descent/ascent state machines.
        var descents = EntityQueryEnumerator<CEDescentComponent>();
        while (descents.MoveNext(out var uid, out var descent))
        {
            if (now < descent.StageStart + StageDuration(descent.Stage, descent.Ascent))
                continue;

            AdvanceStage((uid, descent));
        }

        if (now >= _nextButtonRefresh)
        {
            _nextButtonRefresh = now + ButtonRefreshInterval;
            RefreshConsoleFlags();
        }
    }

    private TimeSpan _nextButtonRefresh;
    private static readonly TimeSpan ButtonRefreshInterval = TimeSpan.FromSeconds(1f);

    private void AdvanceStage(Entity<CEDescentComponent> ent)
    {
        switch (ent.Comp.Stage)
        {
            case CEDescentStage.Descending:
                SetStage(ent, CEDescentStage.Vanishing);
                break;

            case CEDescentStage.Vanishing:
                // The warp happens on this edge; only advance to Arriving when it actually
                // committed, otherwise the stage would loop the warp forever and the fade
                // would stick at full white.
                var warped = ent.Comp.Ascent ? WarpAscent(ent) : Warp(ent);
                if (warped)
                    SetStage(ent, CEDescentStage.Arriving);
                break;

            case CEDescentStage.Arriving:
                Finish(ent);
                break;
        }
    }

    private void SetStage(Entity<CEDescentComponent> ent, CEDescentStage stage)
    {
        ent.Comp.Stage = stage;
        ent.Comp.StageStart = Timing.CurTime;
        Dirty(ent);

        if (ent.Comp.DescentMap is { } mapUid && TryComp<CEDescentMapComponent>(mapUid, out var map))
        {
            map.Stage = stage;
            map.StageStart = Timing.CurTime;
            Dirty(mapUid, map);
        }
    }

    /// <summary>
    /// Starts the descent sequence for <paramref name="grid"/> onto <paramref name="planet"/>.
    /// </summary>
    public bool TryStartDescent(
        Entity<MapGridComponent> grid,
        Entity<CEPlanetComponent> planet)
    {
        if (planet.Comp.Network is not { } networkUid ||
            !HasComp<CEZLevelsNetworkComponent>(networkUid))
            return false;

        if (HasComp<CEDescentComponent>(grid))
            return false;

        if (_xformQuery.GetComponent(grid).MapUid is not { } sourceMap)
            return false;

        // No descents from a descent pseudo-map.
        if (HasComp<CEDescentMapComponent>(sourceMap))
            return false;

        // The whole docked set rides along.
        var gridSet = new HashSet<EntityUid>();
        GetAllDockedShuttles(grid, gridSet);
        gridSet.Add(grid);
        gridSet.RemoveWhere(uid => _xformQuery.GetComponent(uid).MapUid != sourceMap);

        var descent = AddComp<CEDescentComponent>(grid);
        descent.StageStart = Timing.CurTime;
        descent.Planet = planet.Owner;
        descent.Network = networkUid;
        descent.GridSet = gridSet;
        Dirty(grid, descent);

        foreach (var member in gridSet)
            _shuttle.Disable(member);

        BeginDescending((grid.Owner, descent));
        RefreshConsoles(grid.Owner);
        return true;
    }

    /// <summary>
    /// Stage 1 entry: build the pseudo-map and move the docked set onto it in place.
    /// </summary>
    private void BeginDescending(Entity<CEDescentComponent> ent)
    {
        if (_xformQuery.GetComponent(ent).MapUid is not { } originMap)
            return;

        var pseudoMap = _map.CreateMap(out var pseudoMapId, runMapInit: false);
        _meta.SetEntityName(pseudoMap, $"Descent: {ToPrettyString(ent)}");
        _map.InitializeMap(pseudoMapId);

        var mapComp = EnsureComp<CEDescentMapComponent>(pseudoMap);
        mapComp.OriginMap = originMap;
        mapComp.Grid = ent.Owner;
        mapComp.Stage = ent.Comp.Stage;
        mapComp.StageStart = ent.Comp.StageStart;
        mapComp.Ascent = ent.Comp.Ascent;
        Dirty(pseudoMap, mapComp);

        ent.Comp.DescentMap = pseudoMap;
        Dirty(ent);

        MoveGridSet(ent.Comp.GridSet, pseudoMap);
    }

    /// <summary>
    /// Stage 2 for descents: leave the pseudo-map for the planet's ground layer.
    /// </summary>
    private bool Warp(Entity<CEDescentComponent> ent)
    {
        var planetUid = ent.Comp.Planet;
        if (planetUid == null ||
            !TryComp<CEPlanetComponent>(planetUid.Value, out var planet) ||
            planet.Network is not { } networkUid)
        {
            Finish(ent);
            return false;
        }

        // Arrive on the top sky level of the planet's z-stack, keeping the ship's position. The
        // pilot then flies the shuttle down level by level with the console's fly-down controls
        // and picks the landing spot themselves.
        if (!TryGetTopMap(networkUid, out var skyMap))
        {
            Finish(ent);
            return false;
        }

        MoveGridSet(ent.Comp.GridSet, skyMap);
        ReenableShuttles(ent.Comp.GridSet);

        // The roof system only reacts to traversal/FTL moves — the descent moves the ship
        // directly, so rebuild the roof (the ship gets a ceiling again on the sky levels).
        _roof.EnsureRoof(ent.Owner);
        return true;
    }

    /// <summary>
    /// Stage 2 for ascents: leave the pseudo-map for the planet's home space map.
    /// </summary>
    private bool WarpAscent(Entity<CEDescentComponent> ent)
    {
        var planetUid = ent.Comp.Planet;
        if (planetUid == null ||
            !TryComp<CEPlanetComponent>(planetUid.Value, out var planet) ||
            _xformQuery.GetComponent(planetUid.Value).MapUid is not { } spaceMap)
        {
            Finish(ent);
            return false;
        }

        // Breach orbit: drop the ship just outside the planet's body.
        var planetPos = _transform.GetWorldPosition(planetUid.Value);
        var angle = _random.NextAngle();
        var orbitDist = MathF.Max(planet.WorldRadius, MinAscentOrbitDistance) + MaxAscentOrbitDistance;
        var worldPos = planetPos + angle.ToVec() * orbitDist;

        MoveGridSet(ent.Comp.GridSet, spaceMap, worldPos);
        ReenableShuttles(ent.Comp.GridSet);
        _roof.EnsureRoof(ent.Owner);
        return true;
    }

    private void ReenableShuttles(HashSet<EntityUid> gridSet)
    {
        foreach (var member in gridSet)
        {
            _shuttle.Enable(member);
            RefreshConsoles(member);
        }
    }

    private void Finish(Entity<CEDescentComponent> ent)
    {
        if (ent.Comp.DescentMap is { } pseudoMap && Exists(pseudoMap))
            QueueDel(pseudoMap);

        // Failure paths (warp aborted) must not leave the ship disabled — a disabled shuttle
        // silently blocks FTL and flight. On the success path the warp already re-enabled the
        // set, and Enable is idempotent.
        if (ent.Comp.GridSet.Count > 0)
            ReenableShuttles(ent.Comp.GridSet);

        RemComp<CEDescentComponent>(ent);
        RefreshConsoles(ent.Owner);
    }

    /// <summary>
    /// Starts an ascent from a planet's ground layer back to its home space map.
    /// </summary>
    public bool TryStartAscent(Entity<MapGridComponent> grid, Entity<CEPlanetComponent> planet)
    {
        // Takeoff works from any altitude inside the planet's z-stack — sky levels included —
        // not just from the ground layer.
        if (_xformQuery.GetComponent(grid).MapUid is not { } mapUid ||
            !_planetSystem.TryGetPlanetForMap(mapUid, out var _) ||
            planet.Comp.Network is not { } networkUid ||
            !TryComp<CEZLevelMapComponent>(mapUid, out var zMap) ||
            zMap.NetworkUid != networkUid)
            return false;

        if (HasComp<CEDescentComponent>(grid) || HasComp<CEDescentSpinupComponent>(grid))
            return false;

        var gridSet = new HashSet<EntityUid>();
        GetAllDockedShuttles(grid, gridSet);
        gridSet.Add(grid);
        gridSet.RemoveWhere(uid => _xformQuery.GetComponent(uid).MapUid != mapUid);

        var ascent = AddComp<CEDescentComponent>(grid);
        ascent.StageStart = Timing.CurTime;
        ascent.Planet = planet.Owner;
        ascent.Network = networkUid;
        ascent.Ascent = true;
        ascent.GridSet = gridSet;
        Dirty(grid, ascent);

        foreach (var member in gridSet)
            _shuttle.Disable(member);

        BeginDescending((grid.Owner, ascent));
        RefreshConsoles(grid.Owner);
        return true;
    }

    /// <summary>
    /// Relocates a set of grids to <paramref name="targetMap"/> at the given world position,
    /// preserving their world rotation, undocking first and zeroing velocities.
    /// </summary>
    private void MoveGridSet(HashSet<EntityUid> gridSet, EntityUid targetMap, Vector2? worldPos = null)
    {
        var moves = new List<(EntityUid Grid, Vector2 WorldPos, Angle WorldRot)>(gridSet.Count);
        foreach (var grid in gridSet)
        {
            if (!_gridQuery.HasComponent(grid))
                continue;

            moves.Add((grid, _transform.GetWorldPosition(grid), _transform.GetWorldRotation(grid)));
        }

        // Suppress reentrant roof/viewer rebuilds for the duration of the move — each
        // SetCoordinates fires parent-change events that would rebuild mid-recursion.
        _roof.SuppressAutoUpdates = true;
        _zLevels.SuppressViewerMapChange = true;
        try
        {
            foreach (var (grid, _, _) in moves)
                _dockSystem.UndockDocks(grid);

            foreach (var (grid, oldPos, oldRot) in moves)
            {
                var pos = worldPos ?? oldPos;
                var xform = _xformQuery.GetComponent(grid);
                _transform.SetCoordinates(grid, xform, new EntityCoordinates(targetMap, pos), rotation: oldRot);

                if (_physQuery.TryGetComponent(grid, out var body))
                {
                    _physics.SetLinearVelocity(grid, Vector2.Zero, body: body);
                    _physics.SetAngularVelocity(grid, 0f, body: body);
                }
            }
        }
        finally
        {
            _roof.SuppressAutoUpdates = false;
            _zLevels.SuppressViewerMapChange = false;
        }
    }

    private void RefreshConsoles(EntityUid grid)
    {
        _console.RefreshShuttleConsoles(grid);
    }

    /// <summary>
    /// FTL beacon targets that are stellar bodies arrive in orbit around them instead of at their
    /// centre (the bodies are far bigger than a ship). The star is found by matching the beacon's
    /// position against the map's star system. Any other beacon passes through.
    /// </summary>
    public EntityCoordinates ResolvePlanetBeaconTarget(EntityUid beaconEnt, TransformComponent targetXform)
    {
        if (TryComp<CEPlanetComponent>(beaconEnt, out var comp) && comp.WorldRadius > 0f)
        {
            var pos = _transform.GetWorldPosition(targetXform);
            var dir = _random.NextAngle().ToVec();
            var orbitDist = comp.WorldRadius + 40f;
            return new EntityCoordinates(targetXform.MapUid!.Value, pos + dir * orbitDist);
        }

        // The system's star: arrive just outside its disc too.
        if (targetXform.MapUid is { } mapUid &&
            TryComp<StarSystemMapComponent>(mapUid, out var starSystem) &&
            starSystem.StarSystem != null)
        {
            var star = starSystem.StarSystem.Star;
            var starPos = star.Position + starSystem.StarOffset;
            if ((_transform.GetWorldPosition(targetXform) - starPos).Length() < 1f)
            {
                var dir = _random.NextAngle().ToVec();
                var orbitDist = star.Radius * Star.NAV_PIXEL_SIZE + 60f;
                return new EntityCoordinates(mapUid, starPos + dir * orbitDist);
            }
        }

        return new EntityCoordinates(targetXform.MapUid!.Value, _transform.GetWorldPosition(targetXform));
    }

    /// <summary>Finds the highest (top) map of a z-network.</summary>
    private bool TryGetTopMap(EntityUid networkUid, out EntityUid mapUid)
    {
        mapUid = EntityUid.Invalid;
        var found = false;
        var bestDepth = int.MinValue;

        var query = EntityQueryEnumerator<CEZLevelMapComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.NetworkUid != networkUid || comp.Depth <= bestDepth)
                continue;

            bestDepth = comp.Depth;
            mapUid = uid;
            found = true;
        }

        return found;
    }

    /// <summary>
    /// True when the shuttle sits on the top map of a planet z-stack — the ceiling. It can't
    /// fly any higher with the traversal; leaving to orbit is the ascent drive's job.
    /// </summary>
    public bool IsAtPlanetCeiling(EntityUid root)
    {
        if (!_xformQuery.TryGetComponent(root, out var xform) ||
            xform.MapUid is not { } mapUid ||
            !TryComp<CEZLevelMapComponent>(mapUid, out var zMap))
            return false;

        if (!TryGetTopMap(zMap.NetworkUid, out var topMap) || topMap != mapUid)
            return false;

        return _planetSystem.TryGetPlanetForMap(mapUid, out _);
    }

    /// <summary>
    /// The closest landable planet on the same map within its zone band, or null.
    /// </summary>
    public bool TryGetClosestPlanet(EntityUid gridUid, out Entity<CEPlanetComponent> planet, out CEPlanetComponent comp)
    {
        planet = default;
        comp = default!;

        if (_xformQuery.GetComponent(gridUid).MapUid is not { } mapUid)
            return false;

        var gridPos = _transform.GetWorldPosition(gridUid);
        Entity<CEPlanetComponent>? best = null;
        var bestDist = float.MaxValue;

        var query = EntityQueryEnumerator<CEPlanetComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var candidate, out var xform))
        {
            if (xform.MapUid != mapUid || candidate.Network == null)
                continue;

            var dist = (gridPos - _transform.GetWorldPosition(xform)).Length();
            if (dist <= candidate.ZoneRadius && dist < bestDist)
            {
                best = (uid, candidate);
                bestDist = dist;
            }
        }

        if (best == null)
            return false;

        planet = best.Value;
        comp = best.Value.Comp;
        return true;
    }

    /// <summary>
    /// Writes the descent console state (buttons + status) into the map interface state.
    /// </summary>
    public void WriteConsoleState(EntityUid root, ShuttleMapInterfaceState state)
    {
        if (TryComp<CEDescentSpinupComponent>(root, out var spinup))
        {
            state.CEDescentState = CEDescentConsoleState.Spinup;
            state.CEDescentTime = new StartEndTime(spinup.Start, spinup.End);
        }
        else if (TryComp<CEDescentComponent>(root, out var descent))
        {
            state.CEDescentState = CEDescentConsoleState.Descending;
            state.CEDescentTime = StartEndTime.FromStartDuration(descent.StageStart, StageDuration(descent.Stage, descent.Ascent));
        }
        else if (TryComp<CEDescentStunnedComponent>(root, out var stunned))
        {
            state.CEDescentState = CEDescentConsoleState.Stunned;
            state.CEDescentTime = new StartEndTime(stunned.Start, stunned.End);
        }
        else
        {
            state.CEDescentState = CEDescentConsoleState.Available;
            state.CEDescentTime = default;
        }

        state.CanDescend = CanDescend(root);
        state.CanAscend = CanAscend(root);
    }

    private bool CanDescend(EntityUid root)
    {
        if (HasComp<CEDescentSpinupComponent>(root) ||
            HasComp<CEDescentComponent>(root) ||
            HasComp<CEDescentStunnedComponent>(root))
            return false;

        return TryGetClosestPlanet(root, out _, out _);
    }

    private bool CanAscend(EntityUid root)
    {
        if (HasComp<CEDescentComponent>(root))
            return false;

        // Takeoff works from any altitude inside the planet's z-stack, not just the ground layer.
        if (_xformQuery.GetComponent(root).MapUid is not { } mapUid)
            return false;

        return _planetSystem.TryGetPlanetForMap(mapUid, out _);
    }

    /// <summary>
    /// Periodically re-pushes console state for shuttles whose descent flags could have
    /// changed as they drift toward/away from planets.
    /// </summary>
    private void RefreshConsoleFlags()
    {
        var query = EntityQueryEnumerator<ShuttleComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (HasComp<CEDescentSpinupComponent>(uid) ||
                HasComp<CEDescentComponent>(uid) ||
                HasComp<CEDescentStunnedComponent>(uid))
                continue;

            var canDescend = CanDescend(uid);
            var canAscend = CanAscend(uid);

            if (_consoleFlagsCache.TryGetValue(uid, out var cached) &&
                cached.CanDescend == canDescend &&
                cached.CanAscend == canAscend)
                continue;

            _consoleFlagsCache[uid] = (canDescend, canAscend);
            RefreshConsoles(uid);
        }

        if (_consoleFlagsCache.Count > 64)
            _consoleFlagsCache.Clear();
    }
}
