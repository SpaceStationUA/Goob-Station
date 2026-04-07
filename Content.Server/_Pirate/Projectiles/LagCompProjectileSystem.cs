using Content.Server.Movement.Components;
using Content.Server.Movement.Systems;
using Content.Server.Projectiles;
using Content.Shared.CCVar;
using Content.Shared._Pirate.Projectiles;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Physics.Events;
using Robust.Shared.Player;

namespace Content.Server._Pirate.Projectiles;

public sealed class LagCompProjectileSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly LagCompensationSystem _lag = default!;
    [Dependency] private readonly ProjectileSystem _projectile = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private EntityQuery<ActorComponent> _actorQuery;
    private EntityQuery<LagCompensationComponent> _lagQuery;

    public float Range = 0.6f;

    public override void Initialize()
    {
        base.Initialize();

        _actorQuery = GetEntityQuery<ActorComponent>();
        _lagQuery = GetEntityQuery<LagCompensationComponent>();

        SubscribeLocalEvent<PlayerShotProjectileEvent>(OnShotProjectile);
        SubscribeLocalEvent<LagCompProjectileComponent, StartCollideEvent>(OnStartCollide);
        SubscribeLocalEvent<LagCompProjectileComponent, EndCollideEvent>(OnEndCollide);

        Subs.CVar(_cfg, CCVars.GunLagCompRange, value => Range = value, true);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<LagCompProjectileComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Targets.Count == 0)
                continue;

            var pos = _transform.GetMapCoordinates(uid);
            foreach (var target in comp.Targets)
            {
                var lagPos = _transform.ToMapCoordinates(_lag.GetCoordinates(target, comp.ShooterSession));
                if (!pos.InRange(lagPos, Range))
                    continue;

                _projectile.DoHit(uid, target);
                RemCompDeferred<LagCompProjectileComponent>(uid);
                break;
            }
        }
    }

    private void OnShotProjectile(ref PlayerShotProjectileEvent args)
    {
        if (!_actorQuery.TryComp(args.User, out var actor))
            return;

        var session = actor.PlayerSession;
        var comp = EnsureComp<LagCompProjectileComponent>(args.Projectile);
        comp.ShooterSession = session;
        comp.Shooter = args.User;

        var ev = new ShotPredictedProjectileEvent
        {
            Projectile = GetNetEntity(args.Projectile),
        };

        RaiseNetworkEvent(ev, session);
    }

    private void OnStartCollide(Entity<LagCompProjectileComponent> ent, ref StartCollideEvent args)
    {
        if (args.OurEntity != ent.Owner || args.OurFixtureId != SharedFlyBySoundSystem.FlyByFixture)
            return;

        if (_lagQuery.HasComp(args.OtherEntity))
            ent.Comp.Targets.Add(args.OtherEntity);
    }

    private void OnEndCollide(Entity<LagCompProjectileComponent> ent, ref EndCollideEvent args)
    {
        if (args.OurEntity != ent.Owner || args.OurFixtureId != SharedFlyBySoundSystem.FlyByFixture)
            return;

        ent.Comp.Targets.Remove(args.OtherEntity);
    }
}
