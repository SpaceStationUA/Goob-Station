/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Numerics;
using Content.Shared._FarHorizons.Planets;
using Content.Shared._Pirate.ZLevels.Core.Components;
using Content.Shared._Pirate.ZLevels.Core.EntitySystems;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Server._FarHorizons.Planets;

/// <summary>
/// Far Horizons: planet sky layers are no-landing zones for buildings. Anything floating there —
/// a player who stepped off a shuttle, a thrown item — drifts sideways toward the nearest open
/// spot when it's over a building's grid, and is transferred down level by level the moment it
/// isn't (landing damage comes from the touchdown drop on the ground layer). Players with an
/// active jetpack are exempt: they fly under their own power and use the level actions. Thrown
/// items keep their momentum for a grace period, then drop — nothing is lost in the sky.
/// The fall is driven here directly instead of relying on the shared gravity machinery, whose
/// resting logic would otherwise keep weightless floaters pinned to the level plane.
/// </summary>
public sealed class CEZPlanetSkyDriftSystem : EntitySystem
{
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(0.25);
    private static readonly TimeSpan ItemGracePeriod = TimeSpan.FromSeconds(10);

    /// <summary>Target horizontal speed while drifting toward a legal landing spot.</summary>
    private const float DriftSpeed = 3f;

    /// <summary>Per-update cap on the drift velocity correction (keeps the nudge smooth).</summary>
    private const float MaxDriftVelocityDelta = 0.6f;

    /// <summary>Spiral search radius (tiles) for the nearest open landing spot.</summary>
    private const int MaxDriftSearchTiles = 24;

    /// <summary>Descent speed given to a falling entity and the touchdown drop height on the ground.</summary>
    private const float FallSpeed = 6f;
    private const float TouchdownDropHeight = 4f;

    [Dependency] private readonly CESharedZLevelsSystem _zLevels = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    /// <summary>Cached drift destinations so the spiral search doesn't re-run every update.</summary>
    private readonly Dictionary<EntityUid, Vector2> _driftTargets = new();

    /// <summary>When a thrown item entered the sky — it keeps its momentum until the grace elapses.</summary>
    private readonly Dictionary<EntityUid, TimeSpan> _itemGrace = new();

    private TimeSpan _nextUpdate;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextUpdate)
            return;

        _nextUpdate = _timing.CurTime + UpdateInterval;

        var query = EntityQueryEnumerator<CEZPhysicsComponent, PhysicsComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var body, out var xform))
        {
            // Aboard a grid (the shuttle) — nothing to do here. Map-parented floaters only.
            if (xform.GridUid != null || xform.MapUid is not { } mapUid)
                continue;

            // Planet sky layer? The marker is authoritative on the server; the direct neighbour
            // check covers stacks built before the marker existed.
            var hasSkyMarker = HasComp<CEZPlanetSkyLayerComponent>(mapUid);
            var directBelowGround = _zLevels.TryGetPlanetGroundLayerBelow(mapUid, out var directGroundMapUid);

            if (!hasSkyMarker && !directBelowGround)
                continue;

            var isMob = HasComp<MobStateComponent>(uid);

            // Jetpack active = the player flies under their own power; no drift, no forced fall.
            if (isMob && HasComp<JetpackUserComponent>(uid))
                continue;

            // Thrown items keep their momentum for a while before the sky takes them.
            if (!isMob)
            {
                if (!_itemGrace.TryGetValue(uid, out var firstSeen))
                {
                    _itemGrace[uid] = _timing.CurTime;
                    Log.Info($"CEZPlanetSkyDrift: item {ToPrettyString(uid)} grace started on {ToPrettyString(mapUid)} at {_transform.GetWorldPosition(xform)}");
                    continue;
                }

                if (_timing.CurTime - firstSeen < ItemGracePeriod)
                    continue;

                if (_timing.CurTime - firstSeen < ItemGracePeriod + UpdateInterval)
                    Log.Info($"CEZPlanetSkyDrift: item {ToPrettyString(uid)} grace elapsed on {ToPrettyString(mapUid)} at {_transform.GetWorldPosition(xform)}");
            }

            var worldPos = _transform.GetWorldPosition(xform);

            // Over a building (a grid with actual tiles under the position, one level above the
            // ground layer)? Drift sideways until clear — buildings are no-landing zones.
            EntityUid? overGridUid = null;
            if (directGroundMapUid is { } groundUid &&
                _zLevels.TryResolveTraversalGridForOffsetAtWorldPosition(uid, -1, worldPos, out var belowGrid, out var belowGridComp, xform) &&
                belowGrid != groundUid &&
                _map.TryGetTileRef(belowGrid, belowGridComp, worldPos, out var tileRef) &&
                !tileRef.Tile.IsEmpty)
            {
                overGridUid = belowGrid;
            }

            if (overGridUid is { } buildingUid && directGroundMapUid is { } driftGroundUid)
            {
                DriftTowardsOpenSpot(uid, body, xform, worldPos, driftGroundUid);
                continue;
            }

            // Open ground or void — fall. Transfer straight down a level; the ground layer's
            // gravity handles the touchdown (and the damage) from the drop height set on arrival.
            _driftTargets.Remove(uid);

            if (_zLevels.TryMoveDown(uid, bypassPassability: true))
            {
                var newMapUid = Transform(uid).MapUid;
                if (newMapUid is { } arrivedMapUid && HasComp<CEZGroundLayerComponent>(arrivedMapUid))
                {
                    // Touchdown: drop from a height so the z-physics lands them with impact.
                    _zLevels.SetFallTouchdown(uid, TouchdownDropHeight, FallSpeed);

                    if (!isMob)
                        _physics.SetLinearVelocity(uid, Vector2.Zero, body: body);

                    _itemGrace.Remove(uid);
                }
            }
            else
            {
                Log.Warning($"CEZPlanetSkyDrift: TryMoveDown failed for {ToPrettyString(uid)} on map {ToPrettyString(mapUid)}");
            }
        }
    }

    /// <summary>Nudges the entity toward its (cached) nearest legal landing spot using impulses.</summary>
    private void DriftTowardsOpenSpot(EntityUid uid, PhysicsComponent body, TransformComponent xform, Vector2 worldPos, EntityUid groundMapUid)
    {
        if (!_driftTargets.TryGetValue(uid, out var target))
        {
            if (!TryFindDriftTarget(uid, xform, worldPos, groundMapUid, out target))
                return;

            _driftTargets[uid] = target;
        }

        var delta = target - worldPos;
        if (delta.Length() < 0.25f)
        {
            _driftTargets.Remove(uid);
            _physics.SetLinearVelocity(uid, Vector2.Zero, body: body);
            return;
        }

        // Accelerate toward the drift velocity, capped per update so the push stays smooth and
        // the entity's own movement still counts against it.
        var desired = Vector2.Normalize(delta) * DriftSpeed;
        var correction = desired - body.LinearVelocity;
        if (correction.Length() > MaxDriftVelocityDelta)
            correction = Vector2.Normalize(correction) * MaxDriftVelocityDelta;

        _physics.ApplyLinearImpulse(uid, correction * body.Mass, body: body);
    }

    /// <summary>
    /// Spiral-searches the ground layer for the nearest open terrain tile (the map's own grid,
    /// non-empty) around <paramref name="worldPos"/>. Falls back to drifting away from the
    /// building below when no open terrain exists on the map (e.g. a space-borne outpost map).
    /// </summary>
    private bool TryFindDriftTarget(EntityUid uid, TransformComponent xform, Vector2 worldPos, EntityUid groundMapUid, out Vector2 target)
    {
        target = default;

        for (var ring = 1; ring <= MaxDriftSearchTiles; ring++)
        {
            for (var dx = -ring; dx <= ring; dx++)
            {
                for (var dy = -ring; dy <= ring; dy++)
                {
                    if (MathF.Max(MathF.Abs(dx), MathF.Abs(dy)) != ring)
                        continue;

                    var candidate = worldPos + new Vector2(dx, dy);
                    if (!_zLevels.TryResolveTraversalGridForOffsetAtWorldPosition(uid, -1, candidate, out var gridUid, out var gridComp, xform) ||
                        gridUid != groundMapUid ||
                        !_map.TryGetTileRef(gridUid, gridComp, candidate, out var tileRef) ||
                        tileRef.Tile.IsEmpty)
                        continue;

                    target = candidate;
                    return true;
                }
            }
        }

        // No open terrain on this map — drift away from the building below instead.
        if (_zLevels.TryResolveTraversalGridForOffsetAtWorldPosition(uid, -1, worldPos, out var buildingUid, out _, xform))
        {
            var away = worldPos - _transform.GetWorldPosition(buildingUid);
            if (away.LengthSquared() < 0.01f)
                away = Vector2.UnitX;

            target = worldPos + Vector2.Normalize(away) * (MaxDriftSearchTiles * 2f);
            return true;
        }

        return false;
    }
}
