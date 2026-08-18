/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Linq;
using System.Numerics;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Server.GameTicking.Rules;
using Content.Server._Pirate.ZLevels.Core;
using Content.Server._Pirate.ZLevels.Shuttles;
using Content.Shared._FarHorizons.Planets;
using Content.Shared._Pirate.ZLevels.Shuttles.Components;
using Content.Shared._FarHorizons.Planets.Descent;
using Content.Shared._FarHorizons.Camera; // Far Horizons: arrival shake
using Robust.Shared.Player;
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
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
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
    [Dependency] private readonly NukeopsRuleSystem _nukeops = default!; // Far Horizons: war-ops ascent lock
    [Dependency] private readonly SharedAudioSystem _audio = default!; // Far Horizons: descent cues

    private EntityQuery<MapGridComponent> _gridQuery = default!;
    private EntityQuery<PhysicsComponent> _physQuery = default!;
    private EntityQuery<TransformComponent> _xformQuery = default!;

    private const float MinAscentOrbitDistance = 32f;
    private const float MaxAscentOrbitDistance = 96f;

    // The descent drive keeps the classic hyperspace cue; FTL uses the new NSV set, so the
    // two reads audibly different.
    private readonly SoundSpecifier _descentStartSound = new SoundPathSpecifier("/Audio/Effects/Shuttle/hyperspace_begin.ogg")
    {
        Params = AudioParams.Default.WithVolume(-5f),
    };

    private readonly SoundSpecifier _descentArriveSound = new SoundPathSpecifier("/Audio/Effects/Shuttle/hyperspace_end.ogg")
    {
        Params = AudioParams.Default.WithVolume(-5f),
    };

    /// <summary>Per-shuttle cached console flags so the periodic refresh only pushes state on change.</summary>
    private readonly Dictionary<EntityUid, (bool CanDescend, bool CanAscend)> _consoleFlagsCache = new();

    /// <summary>
    /// Per-grid recently refused descent reasons (reason, server curtime until shown), so the
    /// console UI can flash feedback instead of the refusal passing silently.
    /// </summary>
    private readonly Dictionary<EntityUid, (string Reason, TimeSpan Until)> _descentDenies = new();

    /// <summary>How long a refusal stays visible on the console UI.</summary>
    private static readonly TimeSpan DenyFeedbackTime = TimeSpan.FromSeconds(4);

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
            // War ops holds the nukie shuttle at its outpost until the timer ends — the
            // ascent drive is just as locked as the FTL drive.
            if (_nukeops.IsNukieShuttleHeld(root, out var warReason))
            {
                _popup.PopupClient(warReason, ent.Owner, args.Actor);
                return;
            }

            if (_xformQuery.GetComponent(root).MapUid is not { } mapUid ||
                !_planetSystem.TryGetPlanetForMap(mapUid, out var planet) ||
                !TryStartAscent((root, _gridQuery.GetComponent(root)), planet))
            {
                _popup.PopupClient(Loc.GetString("ce-descent-request-denied"), ent.Owner, args.Actor);
            }

            return;
        }

        // Resolve the best target: the closest landable planet on this map within its zone.
        // The z-stack is created lazily here (or on approach) — dormant planets cost no maps.
        string? denyReason = null;
        if (TryGetClosestPlanet(root, out var descentPlanet, out _) &&
            _planetSystem.EnsurePlanetStack(descentPlanet.Owner) &&
            TryBeginDescent(root, descentPlanet, out denyReason))
        {
            // The ship is committed to the charge: lock the pilots for its duration.
            var spinup = EnsureComp<CEDescentSpinupComponent>(root);
            if (!HasComp<PreventPilotComponent>(root))
            {
                AddComp<PreventPilotComponent>(root);
                spinup.PilotLocked.Add(root);
            }

            Dirty(root, spinup);

            RefreshConsoles(root);
            return;
        }

        if (denyReason != null)
        {
            _descentDenies[root] = (denyReason, Timing.CurTime + DenyFeedbackTime);
            _popup.PopupClient(Loc.GetString(denyReason), ent.Owner, args.Actor);
            RefreshConsoles(root);
        }
    }

    /// <summary>
    /// Collects the whole docked chain of <paramref name="root"/> (peers only — the root
    /// itself is not added). Equivalent of upstream's shuttle helper, which this fork lacks.
    /// </summary>
    private void GetAllDockedShuttles(EntityUid root, HashSet<EntityUid> output)
    {
        // One pass builds a grid → docked-with-docks map; the BFS reuses it instead of
        // rescanning the whole world for every grid.
        var dockedByGrid = new Dictionary<EntityUid, List<EntityUid>>();
        var query = AllEntityQuery<DockingComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var dock, out var xform))
        {
            if (!dock.Docked || dock.DockedWith == null || xform.GridUid is not { } grid)
                continue;

            if (!dockedByGrid.TryGetValue(grid, out var docks))
                dockedByGrid[grid] = docks = new List<EntityUid>();

            docks.Add(dock.DockedWith.Value);
        }

        var pending = new Queue<EntityUid>();
        pending.Enqueue(root);

        while (pending.TryDequeue(out var grid))
        {
            if (!dockedByGrid.TryGetValue(grid, out var docks))
                continue;

            foreach (var otherDockUid in docks)
            {
                if (!_xformQuery.TryGetComponent(otherDockUid, out var otherXform) ||
                    otherXform.GridUid is not { } otherGrid)
                    continue;

                if (otherGrid == root || !output.Add(otherGrid))
                    continue;

                pending.Enqueue(otherGrid);
            }
        }

        // Only flyable ships ride along: a ship docked to a planet's outpost (or a station)
        // must leave the structure behind, not drag it into space. Roof grids (thrusterless
        // helpers spawned for z-stack shuttles) still count as part of the ship.
        output.RemoveWhere(uid => !IsPartOfShipSet(uid));
    }

    /// <summary>True for grids the descent/ascent should relocate: flyable ships and their roof grids.</summary>
    private bool IsPartOfShipSet(EntityUid uid)
    {
        if (HasComp<CEZShuttleRoofComponent>(uid))
            return true;

        return TryComp<ShuttleComponent>(uid, out var shuttle) &&
               (shuttle.AngularThrusters.Count > 0 ||
                shuttle.LinearThrusters.Any(list => list.Count > 0));
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

        // Fast path: no charge is running anywhere, so there's nothing to abort.
        var anySpinup = EntityQueryEnumerator<CEDescentSpinupComponent>();
        if (!anySpinup.MoveNext(out _, out _))
            return;

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
            // Only the grids whose PreventPilot this charge created are ours to remove later.
            stunned.PilotLocked = spinup.PilotLocked.Contains(uid);
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

        // Touchdown (surface or space, descent or ascent) gets the classic arrival sting and a
        // rumble through the hull for everyone aboard.
        if (stage == CEDescentStage.Arriving)
        {
            _audio.PlayPvs(_descentArriveSound, ent.Owner);

            var filter = Filter.Empty();
            foreach (var grid in ent.Comp.GridSet)
                filter.AddInGrid(grid);

            RaiseNetworkEvent(new RadialShakeEvent
            {
                Duration = 1.5f,
                Amplitude = 1.1f,
            }, filter);
        }

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

        MoveGridSet(ent.Comp.GridSet, ent.Owner, pseudoMap);

        // The fall begins audibly for everyone aboard.
        _audio.PlayPvs(_descentStartSound, ent.Owner);
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

        // Arrive on the top sky level of the planet's z-stack. The pilot then flies the
        // shuttle down level by level with the console's fly-down controls and picks the
        // landing spot themselves.
        if (!TryGetTopMap(networkUid, out var skyMap))
        {
            Finish(ent);
            return false;
        }

        // LandingRadius scatters the arrival point around the surface origin (0,0) — on the
        // lavaland planet that's around the outpost; 0 means everyone arrives at the origin.
        var landingOffset = Vector2.Zero;
        if (TryComp<CEPlanetComponent>(planetUid.Value, out var planetComp) && planetComp.LandingRadius > 0f)
            landingOffset = _random.NextAngle().ToVec() * _random.NextFloat(0f, planetComp.LandingRadius);

        MoveGridSet(ent.Comp.GridSet, ent.Owner, skyMap, landingOffset);
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
        var orbitDist = MathF.Max(planet.WorldRadius, MinAscentOrbitDistance) +
                        _random.NextFloat(MinAscentOrbitDistance, MaxAscentOrbitDistance);
        var worldPos = planetPos + angle.ToVec() * orbitDist;

        MoveGridSet(ent.Comp.GridSet, ent.Owner, spaceMap, worldPos);
        ReenableShuttles(ent.Comp.GridSet);
        _roof.EnsureRoof(ent.Owner);
        return true;
    }

    private void ReenableShuttles(HashSet<EntityUid> gridSet)
    {
        foreach (var member in gridSet)
        {
            // The set may hold grids deleted mid-sequence; skip them without failing the rest.
            if (!Exists(member) || !_gridQuery.HasComponent(member))
                continue;

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

        // Deferred: this can run while Update enumerates CEDescentComponents; removing the
        // component eagerly would invalidate the enumerator mid-iteration.
        RemCompDeferred<CEDescentComponent>(ent);
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

        if (HasComp<CEDescentComponent>(grid) ||
            HasComp<CEDescentSpinupComponent>(grid) ||
            HasComp<CEDescentStunnedComponent>(grid))
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
    /// Relocates a set of grids to <paramref name="targetMap"/>, preserving their world rotation,
    /// undocking first and zeroing velocities. With <paramref name="worldPos"/> the root grid
    /// lands exactly there and every other grid keeps its current offset from the root, so the
    /// docked formation stays intact; without it each grid keeps its own position.
    /// </summary>
    private void MoveGridSet(HashSet<EntityUid> gridSet, EntityUid root, EntityUid targetMap, Vector2? worldPos = null)
    {
        var rootPos = _transform.GetWorldPosition(root);
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
                if (worldPos != null)
                    pos += oldPos - rootPos;

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
    /// Creates descendable z-stacks for planets a shuttle has entered the approach radius of
    /// (or parked on the surface of), if they don't have one yet
    /// (see <see cref="CEPlanetSystem.EnsurePlanetStack"/>). Also keeps landing pads clear:
    /// the biome generates rocks around viewers (the ship), so parked and moving ships on a
    /// planet surface continuously re-clear their own footprint.
    /// </summary>
    private void EnsureNearbyPlanetStacks()
    {
        var query = EntityQueryEnumerator<ShuttleComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.MapUid is not { } mapUid)
                continue;

            // Clear the ship's footprint on planet surfaces so freshly generated rocks never
            // end up inside the hull.
            if (HasComp<CEZGroundLayerComponent>(mapUid))
                _planetSystem.ExcavateLandingPad(mapUid, uid);

            var shuttlePos = _transform.GetWorldPosition(xform);

            var planets = EntityQueryEnumerator<CEPlanetComponent, TransformComponent>();
            while (planets.MoveNext(out var planetUid, out var planet, out var planetXform))
            {
                if (planet.Network != null)
                    continue;

                // A ship parked on a not-yet-wrapped planet surface (lavaland/nukie ground —
                // e.g. a shuttle spawned at the outpost) needs its stack too: it never
                // "approached" the planet entity, it's already on the surface.
                if (planet.GroundMap == mapUid)
                {
                    _planetSystem.EnsurePlanetStack(planetUid);
                    continue;
                }

                if (planetXform.MapUid != mapUid)
                    continue;

                if ((shuttlePos - _transform.GetWorldPosition(planetXform)).LengthSquared() > planet.ApproachRadius * planet.ApproachRadius)
                    continue;

                _planetSystem.EnsurePlanetStack(planetUid);
            }
        }
    }

    /// <summary>
    /// FTL beacon targets that are stellar bodies arrive in orbit around them instead of at their
    /// centre (the bodies are far bigger than a ship). The star is found by matching the beacon's
    /// position against the map's star system. Any other beacon passes through. Returns
    /// <see cref="EntityCoordinates.Invalid"/> when the beacon's map can't be resolved.
    /// </summary>
    public EntityCoordinates ResolvePlanetBeaconTarget(EntityUid beaconEnt, TransformComponent targetXform)
    {
        if (TryComp<CEPlanetComponent>(beaconEnt, out var comp) && comp.WorldRadius > 0f)
        {
            if (targetXform.MapUid is not { } planetMapUid)
                return EntityCoordinates.Invalid;

            var pos = _transform.GetWorldPosition(targetXform);
            var dir = _random.NextAngle().ToVec();
            var orbitDist = comp.WorldRadius + 40f;
            return new EntityCoordinates(planetMapUid, pos + dir * orbitDist);
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

        if (targetXform.MapUid is not { } fallbackMapUid)
            return EntityCoordinates.Invalid;

        return new EntityCoordinates(fallbackMapUid, _transform.GetWorldPosition(targetXform));
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
            state.CEDescentPlanet = GetNetEntity(spinup.Planet);
        }
        else if (TryComp<CEDescentComponent>(root, out var descent))
        {
            state.CEDescentState = CEDescentConsoleState.Descending;
            state.CEDescentTime = StartEndTime.FromStartDuration(descent.StageStart, StageDuration(descent.Stage, descent.Ascent));
            if (descent.Planet is { } planet)
                state.CEDescentPlanet = GetNetEntity(planet);
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

            // A recent refusal is shown on the console UI for a few seconds.
            if (_descentDenies.TryGetValue(root, out var deny) && Timing.CurTime < deny.Until)
            {
                state.CEDescentDenyReason = deny.Reason;
                state.CEDescentDenyUntil = deny.Until;
            }
            else
            {
                _descentDenies.Remove(root);
            }
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
        if (HasComp<CEDescentComponent>(root) ||
            HasComp<CEDescentSpinupComponent>(root) ||
            HasComp<CEDescentStunnedComponent>(root))
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
        // Lazy z-stacks: a planet that still has no descendable maps grows them the moment a
        // ship enters its approach radius, so dormant worlds stay map-free all round.
        EnsureNearbyPlanetStacks();

        var seen = new HashSet<EntityUid>();
        var query = EntityQueryEnumerator<ShuttleComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            seen.Add(uid);

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

        // Drop only entries for shuttles that are gone — never the whole cache, so active
        // shuttles keep their cached states and don't get needless console refreshes.
        if (_consoleFlagsCache.Count > seen.Count)
        {
            var staleUids = new List<EntityUid>(_consoleFlagsCache.Count);
            foreach (var cachedUid in _consoleFlagsCache.Keys)
            {
                if (!seen.Contains(cachedUid))
                    staleUids.Add(cachedUid);
            }

            foreach (var staleUid in staleUids)
                _consoleFlagsCache.Remove(staleUid);
        }
    }
}
