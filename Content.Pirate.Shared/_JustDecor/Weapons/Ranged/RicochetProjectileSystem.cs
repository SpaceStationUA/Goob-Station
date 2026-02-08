using System.Numerics;
using Content.Shared.Physics;
using Content.Shared.Projectiles;
using Content.Shared.Damage;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Spawners;

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
    private const int MaxRaycasts = 256;
    private const float SteeringResponsiveness = 50f;
    private const float MinHomingDelay = 0.05f;

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
                if (!component.FollowPlannedPath || component.CurrentBounces >= maxBounces)
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
                var responsiveness = component.SteeringStrength * SteeringResponsiveness;
                var alpha = 1f - MathF.Exp(-responsiveness * frameTime);
                var newDir = Vector2.Normalize(Vector2.Lerp(currentDir, towardsTarget, MathF.Min(alpha, 1.0f)));
                _physics.SetLinearVelocity(uid, newDir * currentSpeed, body: physics);

            // Keep projectile alive for ricochet handling.
            }
        }
    }

    private void HandleTargetHit(EntityUid uid, RicochetProjectileComponent component)
    {
        if (Deleted(uid) || component.Target == null)
            return;

        if (Deleted(component.Target.Value))
            return;

        if (TryComp<ProjectileComponent>(uid, out var proj))
        {
            var hitEvent = new ProjectileHitEvent(proj.Damage, component.Target.Value, proj.Shooter);
            RaiseLocalEvent(uid, ref hitEvent);
            _damageable.TryChangeDamage(component.Target.Value, hitEvent.Damage, proj.IgnoreResistances, origin: proj.Shooter);

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

        if (HasComp<ProjectileComponent>(args.Target))
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
            timed.Lifetime += component.BounceLifetimeBonus;

        component.CurrentBounces++;
        component.HomingAccumulator = MathF.Max(component.HomingDelay, MinHomingDelay);

        var currentPos = _transform.GetWorldPosition(uid);
        var mapId = Transform(uid).MapID;

        // Find normal to push out of wall
        var normal = TryGetContactNormal(uid, args.Target, out var contactNormal)
            ? contactNormal
            : (currentPos - _transform.GetWorldPosition(args.Target)).Normalized();
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
        var raycastBudget = MaxRaycasts;
        var path = TryRecursiveSolve(startPos, targetPos, mapId, depth, component.Target.Value, visited, 0, ref raycastBudget);

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

    private List<Vector2>? TryRecursiveSolve(Vector2 current, Vector2 target, MapId mapId, int depth, EntityUid targetEnt, HashSet<EntityUid> visited, int currentDepth, ref int raycastBudget)
    {
        if (depth == 0)
            return HasDirectLineOfSight(current, target, mapId, targetEnt, ref raycastBudget) ? new List<Vector2>() : null;

        if (currentDepth > 8 || raycastBudget <= 0) return null;

        for (float angle = 0; angle < 360; angle += AngleSearchStep)
        {
            var dir = Angle.FromDegrees(angle).ToWorldVec();
            var ray = new CollisionRay(current, dir, (int) (CollisionGroup.Impassable | CollisionGroup.MidImpassable | CollisionGroup.BulletImpassable));
            if (!TryGetFirstRayHit(mapId, ray, MaxSearchRadius, out var hit, ref raycastBudget))
                continue;

            if (visited.Contains(hit.HitEntity)) continue;

            var normal = (current - hit.HitPos).Normalized();
            var nextPos = hit.HitPos + normal * MinWallDistance;

            visited.Add(hit.HitEntity);
            var subPath = TryRecursiveSolve(nextPos, target, mapId, depth - 1, targetEnt, visited, currentDepth + 1, ref raycastBudget);
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
        if (!TryGetFirstRayHit(mapId, ray, distance, out var hit))
            return true;

        return hit.HitEntity == targetEntity;
    }

    private bool HasDirectLineOfSight(Vector2 from, Vector2 to, MapId mapId, EntityUid targetEntity, ref int raycastBudget)
    {
        var direction = (to - from).Normalized();
        var distance = (to - from).Length();
        if (distance < 0.2f) return true;

        if (raycastBudget <= 0)
            return false;

        var ray = new CollisionRay(from, direction, (int) (CollisionGroup.Impassable | CollisionGroup.MidImpassable | CollisionGroup.BulletImpassable));
        if (!TryGetFirstRayHit(mapId, ray, distance, out var hit, ref raycastBudget))
            return true;

        return hit.HitEntity == targetEntity;
    }

    private bool TryGetFirstRayHit(MapId mapId, CollisionRay ray, float distance, out RayCastResults hit)
    {
        foreach (var result in _physics.IntersectRay(mapId, ray, distance, returnOnFirstHit: true))
        {
            hit = result;
            return true;
        }

        hit = default;
        return false;
    }

    private bool TryGetFirstRayHit(MapId mapId, CollisionRay ray, float distance, out RayCastResults hit, ref int raycastBudget)
    {
        if (raycastBudget <= 0)
        {
            hit = default;
            return false;
        }

        raycastBudget--;

        foreach (var result in _physics.IntersectRay(mapId, ray, distance, returnOnFirstHit: true))
        {
            hit = result;
            return true;
        }

        hit = default;
        return false;
    }

    private bool TryGetContactNormal(EntityUid uid, EntityUid target, out Vector2 normal)
    {
        normal = Vector2.Zero;

        if (!TryComp(uid, out FixturesComponent? fixtures))
            return false;

        var contacts = _physics.GetContacts((uid, fixtures));
        while (contacts.MoveNext(out var contact))
        {
            if (contact == null)
                continue;

            var bodyA = contact.BodyA;
            var bodyB = contact.BodyB;
            if (bodyA == null || bodyB == null)
                continue;

            if (bodyA.Owner != uid && bodyB.Owner != uid)
                continue;

            var other = bodyA.Owner == uid ? bodyB.Owner : bodyA.Owner;
            if (other != target)
                continue;

            var (posA, rotA) = _transform.GetWorldPositionRotation(bodyA.Owner);
            var (posB, rotB) = _transform.GetWorldPositionRotation(bodyB.Owner);
            var transformA = new Robust.Shared.Physics.Transform(posA, rotA);
            var transformB = new Robust.Shared.Physics.Transform(posB, rotB);
            contact.GetWorldManifold(transformA, transformB, out var contactNormal);

            if (bodyA.Owner == uid)
                contactNormal = -contactNormal;

            normal = contactNormal.Normalized();
            return true;
        }

        return false;
    }

    private Vector2 CalculateDynamicBounce(EntityUid projectile, PhysicsComponent physics, EntityUid target, float speed, Vector2 normal)
    {
        var reflect = Vector2.Reflect(physics.LinearVelocity.Normalized(), normal);
        if (target == EntityUid.Invalid || !Exists(target)) return reflect * speed;

        var toTarget = (_transform.GetWorldPosition(target) - _transform.GetWorldPosition(projectile)).Normalized();
        return (reflect * 0.3f + toTarget * 0.7f).Normalized() * speed;
    }
}
