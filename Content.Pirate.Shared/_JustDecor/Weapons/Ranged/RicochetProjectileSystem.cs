using System.Linq;
using System.Numerics;
using Content.Shared.Damage;
using Content.Shared.Physics;
using Content.Shared.Projectiles;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Spawners;
using Robust.Shared.Utility;

namespace Content.Pirate.Shared._JustDecor.Weapons.Ranged;

/// <summary>
/// System that handles ricochet projectiles with improved tracking and collision handling.
/// </summary>
public sealed class RicochetProjectileSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;

    private const float MaxSearchRadius = 50f;
    private const float MinWallDistance = 0.45f;
    private const float AngleSearchStep = 10f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RicochetProjectileComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<RicochetProjectileComponent, ProjectileHitEvent>(OnProjectileHit);
        SubscribeLocalEvent<RicochetProjectileComponent, PreventCollideEvent>(OnPreventCollide);
    }

    private void OnStartup(EntityUid uid, RicochetProjectileComponent component, ComponentStartup args)
    {
        if (component.Target == null || !component.Target.Value.IsValid() || !TryComp(component.Target.Value, out TransformComponent? _))
        {
            component.FollowPlannedPath = false;
            return;
        }

        component.HomingAccumulator = component.HomingDelay;
        CalculateRicochetPath(uid, component);
    }

    private void OnPreventCollide(EntityUid uid, RicochetProjectileComponent component, ref PreventCollideEvent args)
    {
        if (component.Target == null || !component.Target.Value.IsValid())
            return;

        var maxBounces = component.MaxBounces <= 0 ? component.TargetBounces : Math.Min(component.TargetBounces, component.MaxBounces);

        // Don't collide with the target UNTIL we have finished bounces or have LoS shortcut
        if (args.OtherEntity == component.Target)
        {
            // If we have LoS and at least one bounce, we SHOULD collide
            if (component.CurrentBounces >= 1)
            {
                if (!TryComp(component.Target.Value, out TransformComponent? targetXform))
                    return;

                var currentPos = _transform.GetWorldPosition(uid);
                var targetPos = _transform.GetWorldPosition(targetXform);
                if (HasDirectLineOfSight(currentPos, targetPos, Transform(uid).MapID, component.Target.Value))
                {
                    return; // Allow collision
                }
            }

            if (component.CurrentBounces < maxBounces && component.FollowPlannedPath)
            {
                args.Cancelled = true;
            }
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<RicochetProjectileComponent, PhysicsComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var component, out var physics, out var xform))
        {
            // Reduce homing delay
            if (component.HomingAccumulator > 0)
                component.HomingAccumulator -= frameTime;

            // Sync rotation
            var currentVelocity = physics.LinearVelocity;
            var currentSpeed = currentVelocity.Length();

            if (currentSpeed > 0.1f)
            {
                _transform.SetWorldRotation(uid, currentVelocity.ToWorldAngle());
            }

            if (component.Target == null || !component.Target.Value.IsValid() || Deleted(component.Target.Value))
                continue;

            if (!TryComp(component.Target.Value, out TransformComponent? targetXform))
                continue;

            var currentPos = _transform.GetWorldPosition(xform);
            var targetPos = _transform.GetWorldPosition(targetXform);
            var dist = (targetPos - currentPos).Length();

            // Manual hit fallback to prevent orbiting/flying through hitboxes
            if (dist < 0.6f)
            {
                HandleTargetHit(uid, component);
                continue;
            }

            // Homing logic
            var hasLoS = component.CurrentBounces >= 1 && HasDirectLineOfSight(currentPos, targetPos, xform.MapID, component.Target.Value);

            if (component.HomingAccumulator <= 0 && (component.CurrentBounces >= component.TargetBounces || hasLoS))
            {
                var towardsTarget = (targetPos - currentPos).Normalized();
                var currentDir = currentVelocity.Normalized();

                if (currentSpeed < 1f) continue;

                // Very aggressive steering
                var factor = component.SteeringStrength * frameTime * 50f;
                var newDir = Vector2.Normalize(Vector2.Lerp(currentDir, towardsTarget, Math.Min(factor, 1.0f)));
                _physics.SetLinearVelocity(uid, newDir * currentSpeed, body: physics);

                // Ensure it can hit
                if (TryComp<ProjectileComponent>(uid, out var proj) && !proj.DeleteOnCollide)
                {
                    proj.DeleteOnCollide = true;
                    Dirty(uid, proj);
                }
            }
        }
    }

    private void HandleTargetHit(EntityUid uid, RicochetProjectileComponent component)
    {
        if (Deleted(uid) || component.Target == null) return;

        if (TryComp<ProjectileComponent>(uid, out var proj))
        {
            // Apply damage from the projectile component
            _damageable.TryChangeDamage(component.Target.Value, proj.Damage, proj.IgnoreResistances, origin: proj.Shooter);

            proj.DeleteOnCollide = true;
            proj.ProjectileSpent = true;
            _physics.SetLinearVelocity(uid, Vector2.Zero);
            PredictedQueueDel(uid);
        }
    }

    private void OnProjectileHit(EntityUid uid, RicochetProjectileComponent component, ref ProjectileHitEvent args)
    {
        if (args.Target == component.Target)
        {
            PredictedQueueDel(uid);
            return;
        }

        var maxBounces = component.MaxBounces <= 0 ? component.TargetBounces : Math.Min(component.TargetBounces, component.MaxBounces);
        if (component.CurrentBounces >= maxBounces)
        {
            if (TryComp<ProjectileComponent>(uid, out var proj))
            {
                proj.DeleteOnCollide = true;
                Dirty(uid, proj);
            }
            return;
        }

        if (!IsBouncable(args.Target, component.Target))
            return;

        if (!TryComp<PhysicsComponent>(uid, out var physics))
            return;

        // Apply speed and lifetime bonus
        var speed = physics.LinearVelocity.Length();
        speed *= component.SpeedRetentionOnBounce;
        speed += component.SpeedBonusPerBounce;
        if (speed < component.MinimumRicochetSpeed)
        {
            PredictedQueueDel(uid);
            return;
        }

        if (TryComp<TimedDespawnComponent>(uid, out var timed))
            timed.Lifetime += 3f;

        component.CurrentBounces++;
        component.HomingAccumulator = MathF.Min(component.HomingDelay, 0.05f);

        var currentPos = _transform.GetWorldPosition(uid);
        var mapId = Transform(uid).MapID;

        // Find normal to push out of wall
        var normal = (currentPos - _transform.GetWorldPosition(args.Target)).Normalized();
        _transform.SetWorldPosition(uid, currentPos + normal * 0.15f);

        // Check for LoS shortcut
        if (component.Target != null && TryComp(component.Target.Value, out TransformComponent? tXform))
        {
            var targetPos = _transform.GetWorldPosition(tXform);
            if (HasDirectLineOfSight(currentPos, targetPos, mapId, component.Target.Value))
            {
                var direction = (targetPos - currentPos).Normalized();
                _physics.SetLinearVelocity(uid, direction * speed, body: physics);
                component.FollowPlannedPath = false;
                ResetProjectileState(uid);
                return;
            }
        }

        // Follow path or dynamic bounce
        if (component.FollowPlannedPath && component.PlannedPath.Count > component.CurrentBounces)
        {
            var nextWaypoint = component.PlannedPath[component.CurrentBounces];
            var direction = (nextWaypoint - currentPos).Normalized();
            _physics.SetLinearVelocity(uid, direction * speed, body: physics);
            ResetProjectileState(uid);
        }
        else
        {
            var velocity = CalculateDynamicBounce(uid, physics, component.Target ?? EntityUid.Invalid, speed, normal);
            _physics.SetLinearVelocity(uid, velocity, body: physics);
            ResetProjectileState(uid);
        }
    }

    private void ResetProjectileState(EntityUid uid)
    {
        if (TryComp<ProjectileComponent>(uid, out var proj))
        {
            proj.DeleteOnCollide = false;
            proj.ProjectileSpent = false;
            Dirty(uid, proj);
        }
    }

    private bool IsBouncable(EntityUid entity, EntityUid? target)
    {
        if (entity == target) return false;
        if (HasComp<ProjectileComponent>(entity)) return false;

        if (!TryComp<FixturesComponent>(entity, out var fixtures))
            return false;

        return fixtures.Fixtures.Values.Any(f =>
            (f.CollisionMask & (int) (CollisionGroup.Impassable | CollisionGroup.MidImpassable | CollisionGroup.BulletImpassable | CollisionGroup.GlassAirlockLayer)) != 0);
    }

    private void CalculateRicochetPath(EntityUid projectile, RicochetProjectileComponent component)
    {
        if (component.Target == null || !component.Target.Value.IsValid())
        {
            component.FollowPlannedPath = false;
            return;
        }

        if (!TryComp(projectile, out TransformComponent? xform) || !TryComp(component.Target.Value, out TransformComponent? targetXform))
        {
            component.FollowPlannedPath = false;
            return;
        }

        var startPos = _transform.GetWorldPosition(xform);
        var targetPos = _transform.GetWorldPosition(targetXform);
        var mapId = xform.MapID;

        component.PlannedPath.Clear();

        var visited = new HashSet<EntityUid>();
        var depth = component.MaxBounces <= 0 ? component.TargetBounces : Math.Min(component.TargetBounces, component.MaxBounces);
        var path = TryRecursiveSolve(startPos, targetPos, mapId, depth, component.Target.Value, visited, 0);

        if (path != null)
        {
            component.PlannedPath.AddRange(path);
            component.FollowPlannedPath = true;
            if (path.Count > 0 && TryComp<PhysicsComponent>(projectile, out var physics))
            {
                var dir = (path[0] - startPos).Normalized();
                _physics.SetLinearVelocity(projectile, dir * physics.LinearVelocity.Length(), body: physics);
            }
        }
        else
        {
            component.FollowPlannedPath = false;
        }
    }

    private List<Vector2>? TryRecursiveSolve(Vector2 current, Vector2 target, MapId mapId, int depth, EntityUid targetEnt, HashSet<EntityUid> visited, int currentDepth)
    {
        if (depth == 0)
            return HasDirectLineOfSight(current, target, mapId, targetEnt) ? new List<Vector2>() : null;

        if (currentDepth > 8) return null;

        for (float angle = 0; angle < 360; angle += AngleSearchStep)
        {
            var dir = Angle.FromDegrees(angle).ToWorldVec();
            var ray = new CollisionRay(current, dir, (int) (CollisionGroup.Impassable | CollisionGroup.MidImpassable | CollisionGroup.BulletImpassable));
            var hits = _physics.IntersectRay(mapId, ray, MaxSearchRadius, returnOnFirstHit: true).ToList();

            if (hits.Count == 0) continue;
            var hit = hits[0];

            if (visited.Contains(hit.HitEntity)) continue;

            var normal = (current - hit.HitPos).Normalized();
            var nextPos = hit.HitPos + normal * MinWallDistance;

            visited.Add(hit.HitEntity);
            var subPath = TryRecursiveSolve(nextPos, target, mapId, depth - 1, targetEnt, visited, currentDepth + 1);
            visited.Remove(hit.HitEntity);

            if (subPath != null)
            {
                subPath.Insert(0, hit.HitPos);
                return subPath;
            }
        }
        return null;
    }

    private bool HasDirectLineOfSight(Vector2 from, Vector2 to, MapId mapId, EntityUid targetEntity)
    {
        var direction = (to - from).Normalized();
        var distance = (to - from).Length();
        if (distance < 0.2f) return true;

        var ray = new CollisionRay(from, direction, (int) (CollisionGroup.Impassable | CollisionGroup.MidImpassable | CollisionGroup.BulletImpassable));
        var hits = _physics.IntersectRay(mapId, ray, distance, returnOnFirstHit: true).ToList();

        return hits.Count == 0 || hits.Any(h => h.HitEntity == targetEntity);
    }

    private Vector2 CalculateDynamicBounce(EntityUid projectile, PhysicsComponent physics, EntityUid target, float speed, Vector2 normal)
    {
        var reflect = Vector2.Reflect(physics.LinearVelocity.Normalized(), normal);
        if (target == EntityUid.Invalid || !Exists(target)) return reflect * speed;

        var toTarget = (_transform.GetWorldPosition(target) - _transform.GetWorldPosition(projectile)).Normalized();
        return (reflect * 0.3f + toTarget * 0.7f).Normalized() * speed;
    }
}
