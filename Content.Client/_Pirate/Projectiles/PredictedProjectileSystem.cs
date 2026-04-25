using System.Numerics;
using Content.Shared.Projectiles;
using Content.Shared._Pirate.Projectiles;
using Robust.Client.GameObjects;
using Robust.Shared.Collections;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Client._Pirate.Projectiles;

public sealed class PredictedProjectileSystem : EntitySystem
{
    private static readonly TimeSpan PendingPairTtl = TimeSpan.FromSeconds(0.5);

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
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

    private readonly record struct PendingPromoted(EntityUid Projectile, TimeSpan ExpiresAt);
    private readonly record struct PendingAuthoritativeLink(EntityUid Promoted, TimeSpan ExpiresAt);

    private readonly Dictionary<EntityUid, PromotedData> _promoted = new();
    private readonly Queue<PendingPromoted> _pendingPromoted = new();
    private readonly HashSet<NetEntity> _pendingHide = new();
    private readonly Dictionary<NetEntity, PendingAuthoritativeLink> _pendingAuthoritativeLinks = new();
    private readonly Dictionary<EntityUid, EntityUid> _authoritativeToPromoted = new();
    private readonly Dictionary<EntityUid, EntityUid> _promotedToAuthoritative = new();
    private readonly HashSet<EntityUid> _hiddenAuthoritative = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ProjectileComponent, Robust.Client.Physics.UpdateIsPredictedEvent>(OnUpdateIsPredicted);
        SubscribeLocalEvent<ProjectileComponent, EntityTerminatingEvent>(OnProjectileTerminating);
        SubscribeLocalEvent<PlayerShotProjectileEvent>(OnLocalPlayerShotProjectile);
        SubscribeNetworkEvent<ShotPredictedProjectileEvent>(OnShotPredictedProjectile);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        PrunePendingPromoted();
        PrunePendingAuthoritativeLinks();

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

        if (_hiddenAuthoritative.Count > 0)
        {
            var hiddenToRemove = new ValueList<EntityUid>();
            foreach (var uid in _hiddenAuthoritative)
            {
                if (!Exists(uid))
                {
                    hiddenToRemove.Add(uid);
                    continue;
                }

                HideProjectile(uid);
            }

            foreach (var uid in hiddenToRemove)
                _hiddenAuthoritative.Remove(uid);
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

            var promoted = ConsumePendingAuthoritativeLink(netEnt);
            HideAndLinkAuthoritative(uid, promoted);

            resolved.Add(netEnt);
        }

        foreach (var r in resolved)
            _pendingHide.Remove(r);
    }

    private void OnUpdateIsPredicted(Entity<ProjectileComponent> ent, ref Robust.Client.Physics.UpdateIsPredictedEvent args)
    {
        // Promoted entities are manually integrated - exclude from physics prediction
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
            _pendingPromoted.Enqueue(new PendingPromoted(args.Projectile, GetPendingPairExpiry()));
        }
        else
        {
            // Re-sim: hide transient duplicate (deleted next frame).
            HideProjectile(args.Projectile);
        }
    }

    private void OnShotPredictedProjectile(ShotPredictedProjectileEvent args)
    {
        var promoted = DequeuePendingPromoted();

        var uid = GetEntity(args.Projectile);
        if (uid.IsValid())
        {
            HideAndLinkAuthoritative(uid, promoted);
        }
        else
        {
            _pendingHide.Add(args.Projectile);
            if (promoted is { } promotedUid)
                _pendingAuthoritativeLinks[args.Projectile] = new PendingAuthoritativeLink(promotedUid, GetPendingPairExpiry());
        }
    }

    private void OnProjectileTerminating(Entity<ProjectileComponent> ent, ref EntityTerminatingEvent args)
    {
        var uid = ent.Owner;
        _promoted.Remove(uid);
        _hiddenAuthoritative.Remove(uid);

        if (_authoritativeToPromoted.Remove(uid, out var promoted))
        {
            _promotedToAuthoritative.Remove(promoted);

            if (!TerminatingOrDeleted(promoted))
                PredictedQueueDel(promoted);

            return;
        }

        if (_promotedToAuthoritative.Remove(uid, out var authoritative))
            _authoritativeToPromoted.Remove(authoritative);
    }

    private void LinkAuthoritativeProjectile(EntityUid authoritative, EntityUid promoted)
    {
        if (TerminatingOrDeleted(authoritative) ||
            TerminatingOrDeleted(promoted) ||
            !_promoted.ContainsKey(promoted))
            return;

        if (_authoritativeToPromoted.TryGetValue(authoritative, out var existingPromoted))
            _promotedToAuthoritative.Remove(existingPromoted);

        if (_promotedToAuthoritative.TryGetValue(promoted, out var existingAuthoritative))
            _authoritativeToPromoted.Remove(existingAuthoritative);

        _authoritativeToPromoted[authoritative] = promoted;
        _promotedToAuthoritative[promoted] = authoritative;
    }

    private void HideAndLinkAuthoritative(EntityUid uid, EntityUid? promoted = null)
    {
        _hiddenAuthoritative.Add(uid);
        HideProjectile(uid);

        if (promoted is { } promotedUid)
            LinkAuthoritativeProjectile(uid, promotedUid);
    }

    private TimeSpan GetPendingPairExpiry()
    {
        return _timing.CurTime + PendingPairTtl;
    }

    private EntityUid? DequeuePendingPromoted()
    {
        PrunePendingPromoted();

        if (_pendingPromoted.Count == 0)
            return null;

        return _pendingPromoted.Dequeue().Projectile;
    }

    private EntityUid? ConsumePendingAuthoritativeLink(NetEntity netEnt)
    {
        PrunePendingAuthoritativeLinks();

        if (!_pendingAuthoritativeLinks.Remove(netEnt, out var pending) ||
            IsStalePendingPromoted(pending.Promoted, pending.ExpiresAt))
        {
            return null;
        }

        return pending.Promoted;
    }

    private void PrunePendingPromoted()
    {
        while (_pendingPromoted.Count > 0)
        {
            var pending = _pendingPromoted.Peek();
            if (!IsStalePendingPromoted(pending.Projectile, pending.ExpiresAt))
                break;

            _pendingPromoted.Dequeue();
        }
    }

    private void PrunePendingAuthoritativeLinks()
    {
        if (_pendingAuthoritativeLinks.Count == 0)
            return;

        var stale = new ValueList<NetEntity>();
        foreach (var (netEnt, pending) in _pendingAuthoritativeLinks)
        {
            if (IsStalePendingPromoted(pending.Promoted, pending.ExpiresAt))
                stale.Add(netEnt);
        }

        foreach (var netEnt in stale)
            _pendingAuthoritativeLinks.Remove(netEnt);
    }

    private bool IsStalePendingPromoted(EntityUid promoted, TimeSpan expiresAt)
    {
        return _timing.CurTime >= expiresAt ||
               TerminatingOrDeleted(promoted) ||
               !_promoted.ContainsKey(promoted) ||
               _promotedToAuthoritative.ContainsKey(promoted);
    }

    private void HideProjectile(EntityUid uid)
    {
        if (!HasComp<ProjectileComponent>(uid))
            return;

        if (TryComp<SpriteComponent>(uid, out var sprite))
            _sprite.SetVisible((uid, sprite), false);

        if (TryComp<PointLightComponent>(uid, out var light))
            _lights.SetEnabled(uid, false, light);

        if (TryComp<PhysicsComponent>(uid, out var physics))
            _physics.SetCanCollide(uid, false, body: physics);
    }
}
