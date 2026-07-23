using System.Numerics;
using Content.Goobstation.Common.CCVar;
using Content.Server.Movement.Components;
using Content.Server.Movement.Systems;
using Content.Server.Projectiles;
using Content.Shared.CCVar;
using Content.Shared.Damage.Components;
using Content.Shared.Projectiles;
using Content.Shared._Pirate.Projectiles;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Player;

namespace Content.Server._Pirate.Projectiles;

public sealed class LagCompProjectileSystem : EntitySystem
{
    private const float CandidateSearchRadius = 1.5f;

    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly LagCompensationSystem _lag = default!;
    [Dependency] private readonly ProjectileSystem _projectile = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private EntityQuery<ActorComponent> _actorQuery;
    private EntityQuery<LagCompensationComponent> _lagQuery;
    private readonly HashSet<Entity<LagCompensationComponent>> _nearbyTargets = new();
    private readonly HashSet<EntityUid> _candidateTargets = new();

    public float Range = 0.6f;
    private float _crawlHitzoneSize;

    public override void Initialize()
    {
        base.Initialize();

        _actorQuery = GetEntityQuery<ActorComponent>();
        _lagQuery = GetEntityQuery<LagCompensationComponent>();

        SubscribeLocalEvent<PlayerShotProjectileEvent>(OnShotProjectile);
        SubscribeLocalEvent<LagCompProjectileComponent, StartCollideEvent>(OnStartCollide);
        SubscribeLocalEvent<LagCompProjectileComponent, EndCollideEvent>(OnEndCollide);

        Subs.CVar(_cfg, CCVars.GunLagCompRange, value => Range = value, true);
        Subs.CVar(_cfg, GoobCVars.CrawlHitzoneSize, value => _crawlHitzoneSize = value, true);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<LagCompProjectileComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            var pos = _transform.GetMapCoordinates(uid);
            var previousPos = comp.PreviousPosition ?? pos;
            comp.PreviousPosition = pos;

            _candidateTargets.Clear();
            foreach (var target in comp.Targets)
                _candidateTargets.Add(target);

            var travelDistance = previousPos.MapId == pos.MapId
                ? (pos.Position - previousPos.Position).Length()
                : 0f;
            _nearbyTargets.Clear();
            _lookup.GetEntitiesInRange(
                pos,
                CandidateSearchRadius + travelDistance,
                _nearbyTargets,
                LookupFlags.Dynamic);

            foreach (var target in _nearbyTargets)
            {
                if (target.Owner != comp.Shooter)
                    _candidateTargets.Add(target.Owner);
            }

            EntityUid? explicitTarget = null;
            if (TryComp<TargetedProjectileComponent>(uid, out var targeted) &&
                targeted.Target is { } targetedUid &&
                !TerminatingOrDeleted(targetedUid))
            {
                explicitTarget = targetedUid;
                _candidateTargets.Add(targetedUid);
            }

            if (_candidateTargets.Count == 0)
                continue;

            EntityUid? bestTarget = null;
            var bestFraction = float.PositiveInfinity;

            foreach (var target in _candidateTargets)
            {
                if (TerminatingOrDeleted(target))
                    continue;

                var lagPos = _transform.ToMapCoordinates(_lag.GetCoordinates(target, comp.ShooterSession));
                var sameMap = pos.MapId == lagPos.MapId;
                var rewoundSweptDistance = sameMap && previousPos.MapId == lagPos.MapId
                    ? DistanceToSegment(lagPos.Position, previousPos.Position, pos.Position)
                    : float.PositiveInfinity;
                var rewoundSweptFraction = sameMap && previousPos.MapId == lagPos.MapId
                    ? FractionAlongSegment(lagPos.Position, previousPos.Position, pos.Position)
                    : float.PositiveInfinity;
                var rewoundSweptInRange = rewoundSweptDistance <= Range;
                var contactCandidate = comp.Targets.Contains(target);
                var proneAllowed = AllowsRewoundProneHit(
                    uid,
                    target,
                    lagPos,
                    contactCandidate,
                    explicitTarget,
                    out var proneActive);

                var explicitAimSweptDistance = float.PositiveInfinity;
                var explicitAimSweptFraction = float.PositiveInfinity;
                if (proneActive &&
                    explicitTarget == target &&
                    TryComp<ProjectileComponent>(uid, out var projectile) &&
                    previousPos.MapId == pos.MapId)
                {
                    explicitAimSweptDistance =
                        DistanceToSegment(projectile.TargetCoordinates, previousPos.Position, pos.Position);
                    explicitAimSweptFraction =
                        FractionAlongSegment(projectile.TargetCoordinates, previousPos.Position, pos.Position);
                }

                // For an explicitly clicked prone target, the cursor position is the only
                // authoritative representation of where that target was rendered to the shooter.
                // Use it as a narrow fallback when target history and client interpolation disagree.
                var explicitAimInRange = explicitAimSweptDistance <= _crawlHitzoneSize;
                var acceptedByRewind = rewoundSweptInRange;
                var accepted = (acceptedByRewind || explicitAimInRange) && proneAllowed;
                var effectiveSweptFraction = acceptedByRewind
                    ? rewoundSweptFraction
                    : explicitAimSweptFraction;

                if (!accepted || effectiveSweptFraction >= bestFraction)
                    continue;

                bestTarget = target;
                bestFraction = effectiveSweptFraction;
            }

            if (bestTarget is not { } hitTarget)
                continue;

            _projectile.DoHit(uid, hitTarget);
            RemCompDeferred<LagCompProjectileComponent>(uid);
        }
    }

    private bool AllowsRewoundProneHit(
        EntityUid projectileUid,
        EntityUid target,
        MapCoordinates rewoundPosition,
        bool contactCandidate,
        EntityUid? explicitTarget,
        out bool proneActive)
    {
        proneActive = TryComp<RequireProjectileTargetComponent>(target, out var requireTarget) &&
                      requireTarget.Active;

        if (!proneActive || contactCandidate || explicitTarget == target)
            return true;

        if (!TryComp<ProjectileComponent>(projectileUid, out var projectile) ||
            rewoundPosition.MapId != _transform.GetMapCoordinates(projectileUid).MapId)
        {
            return false;
        }

        return (rewoundPosition.Position - projectile.TargetCoordinates).Length() <= _crawlHitzoneSize;
    }

    private void OnShotProjectile(ref PlayerShotProjectileEvent args)
    {
        if (!_actorQuery.TryComp(args.User, out var actor))
            return;

        var session = actor.PlayerSession;
        var comp = EnsureComp<LagCompProjectileComponent>(args.Projectile);
        comp.ShooterSession = session;
        comp.Shooter = args.User;
        comp.PreviousPosition = _transform.GetMapCoordinates(args.Projectile);

        var ev = new ShotPredictedProjectileEvent
        {
            Projectile = GetNetEntity(args.Projectile),
        };

        RaiseNetworkEvent(ev, session);
    }

    private void OnStartCollide(Entity<LagCompProjectileComponent> ent, ref StartCollideEvent args)
    {
        if (args.OurEntity != ent.Owner ||
            args.OurFixtureId != SharedFlyBySoundSystem.FlyByFixture ||
            !args.OtherFixture.Hard)
        {
            return;
        }

        if (_lagQuery.HasComp(args.OtherEntity))
            ent.Comp.Targets.Add(args.OtherEntity);
    }

    private void OnEndCollide(Entity<LagCompProjectileComponent> ent, ref EndCollideEvent args)
    {
        if (args.OurEntity != ent.Owner ||
            args.OurFixtureId != SharedFlyBySoundSystem.FlyByFixture ||
            !args.OtherFixture.Hard)
        {
            return;
        }

        ent.Comp.Targets.Remove(args.OtherEntity);
    }

    private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        var amount = FractionAlongSegment(point, start, end);
        return (point - (start + (end - start) * amount)).Length();
    }

    private static float FractionAlongSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        var segment = end - start;
        var lengthSquared = segment.LengthSquared();
        if (lengthSquared <= 0f)
            return 0f;

        return Math.Clamp(Vector2.Dot(point - start, segment) / lengthSquared, 0f, 1f);
    }

}
