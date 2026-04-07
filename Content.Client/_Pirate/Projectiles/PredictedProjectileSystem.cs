using System.Numerics;
using Content.Shared.Projectiles;
using Content.Shared._Pirate.Projectiles;
using Robust.Client.GameObjects;
using Robust.Shared.Collections;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Client._Pirate.Projectiles;

public sealed class PredictedProjectileSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly SharedPointLightSystem _lights = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    /// <summary>
    /// Tracks promoted projectile state for manual position integration.
    /// These entities have PredictedSpawnComponent removed (so ResetPredictedEntities
    /// doesn't delete them) and IsPredicted=false (so physics rollback doesn't teleport
    /// them to garbage coordinates). We manually advance their position each tick.
    /// </summary>
    private sealed class PromotedData
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public Angle Rotation;
    }

    private readonly Dictionary<EntityUid, PromotedData> _promoted = new();
    private readonly HashSet<NetEntity> _pendingHide = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ProjectileComponent, Robust.Client.Physics.UpdateIsPredictedEvent>(OnUpdateIsPredicted);
        SubscribeLocalEvent<PlayerShotProjectileEvent>(OnLocalPlayerShotProjectile);
        SubscribeNetworkEvent<ShotPredictedProjectileEvent>(OnShotPredictedProjectile);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Advance promoted entities only during the main tick, not during re-simulation.
        if (_timing.IsFirstTimePredicted)
        {
            var toRemove = new ValueList<EntityUid>();
            foreach (var (uid, data) in _promoted)
            {
                if (!Exists(uid))
                {
                    toRemove.Add(uid);
                    continue;
                }

                data.Position += data.Velocity * frameTime;
                _transform.SetLocalPosition(uid, data.Position);
                _transform.SetWorldRotationNoLerp((uid, Transform(uid)), data.Rotation);
            }

            foreach (var uid in toRemove)
                _promoted.Remove(uid);
        }

        // Resolve pending server entity hides.
        if (_pendingHide.Count == 0)
            return;

        var resolved = new ValueList<NetEntity>();
        foreach (var netEnt in _pendingHide)
        {
            var uid = GetEntity(netEnt);
            if (!uid.IsValid())
                continue;

            HideAuthoritativeVisuals(uid);
            resolved.Add(netEnt);
        }

        foreach (var r in resolved)
            _pendingHide.Remove(r);
    }

    private void OnUpdateIsPredicted(Entity<ProjectileComponent> ent, ref Robust.Client.Physics.UpdateIsPredictedEvent args)
    {
        // Promoted entities are manually integrated — exclude from physics prediction
        // to prevent rollback to garbage coordinates (they have no server state).
        if (!_promoted.ContainsKey(ent))
            args.IsPredicted = true;
    }

    private void OnLocalPlayerShotProjectile(ref PlayerShotProjectileEvent args)
    {
        if (!HasComp<PredictedSpawnComponent>(args.Projectile))
            return;

        if (_timing.IsFirstTimePredicted)
        {
            // Promote: remove from prediction cycle so entity persists.
            RemComp<PredictedSpawnComponent>(args.Projectile);

            var xform = Transform(args.Projectile);
            var worldVel = TryComp<PhysicsComponent>(args.Projectile, out var physics)
                ? physics.LinearVelocity
                : Vector2.Zero;

            // LinearVelocity is relative to the broadphase (grid) but uses
            // world-axis orientation. Rotate into grid-local axis to match LocalPosition.
            var parentRot = _transform.GetWorldRotation(xform.ParentUid);
            var localVel = (-parentRot).RotateVec(worldVel);

            _promoted[args.Projectile] = new PromotedData
            {
                Position = xform.LocalPosition,
                Velocity = localVel,
                Rotation = _transform.GetWorldRotation(xform),
            };
        }
        else
        {
            // Re-sim: hide transient duplicate (deleted next frame).
            if (TryComp<SpriteComponent>(args.Projectile, out var sprite))
                _sprite.SetVisible((args.Projectile, sprite), false);
            if (TryComp<PointLightComponent>(args.Projectile, out var light))
                _lights.SetEnabled(args.Projectile, false, light);
        }
    }

    private void OnShotPredictedProjectile(ShotPredictedProjectileEvent args)
    {
        var uid = GetEntity(args.Projectile);
        if (uid.IsValid())
            HideAuthoritativeVisuals(uid);
        else
            _pendingHide.Add(args.Projectile);
    }

    private void HideAuthoritativeVisuals(EntityUid uid)
    {
        if (!HasComp<ProjectileComponent>(uid))
            return;

        if (TryComp<SpriteComponent>(uid, out var sprite))
            _sprite.SetVisible((uid, sprite), false);

        if (TryComp<PointLightComponent>(uid, out var light))
            _lights.SetEnabled(uid, false, light);
    }
}
