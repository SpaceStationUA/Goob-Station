using System.Linq;
using Content.Goobstation.Common.Projectiles;
using Content.Goobstation.Common.Weapons.Penetration;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Camera;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Destructible;
using Content.Shared.Effects;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;

namespace Content.Shared._Pirate.Projectiles;

public sealed class PredictedProjectileHitSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedCameraRecoilSystem _recoil = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedProjectileSystem _projectile = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private EntityQuery<ProjectileComponent> _projectileQuery;
    private EntityQuery<PhysicsComponent> _physicsQuery;
    private EntityQuery<FixturesComponent> _fixturesQuery;

    public override void Initialize()
    {
        base.Initialize();

        _projectileQuery = GetEntityQuery<ProjectileComponent>();
        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        _fixturesQuery = GetEntityQuery<FixturesComponent>();

        if (_net.IsClient)
            SubscribeLocalEvent<ProjectileComponent, StartCollideEvent>(OnStartCollide);
    }

    private void OnStartCollide(EntityUid uid, ProjectileComponent component, ref StartCollideEvent args)
    {
        if (args.OurFixtureId != SharedProjectileSystem.ProjectileFixture || !args.OtherFixture.Hard)
            return;

        DoHit((uid, component, args.OurBody), args.OtherEntity, args.OtherFixture);
    }

    public void DoHit(EntityUid uid, EntityUid target)
    {
        if (!_projectileQuery.TryComp(uid, out var component) ||
            !_physicsQuery.TryComp(uid, out var physics) ||
            FindHardFixture(target) is not { } otherFixture)
        {
            return;
        }

        DoHit((uid, component, physics), target, otherFixture);
    }

    private Fixture? FindHardFixture(EntityUid uid)
    {
        if (!_fixturesQuery.TryComp(uid, out var fixtures))
            return null;

        foreach (var fixture in fixtures.Fixtures.Values)
        {
            if (fixture.Hard)
                return fixture;
        }

        return null;
    }

    public void DoHit(Entity<ProjectileComponent, PhysicsComponent> ent, EntityUid target, Fixture otherFixture)
    {
        var (uid, component, ourBody) = ent;
        if (component is { Weapon: null, OnlyCollideWhenShot: true })
            return;

        if (component.ProjectileSpent && _timing.IsFirstTimePredicted)
            return;

        var reflectEv = new ProjectileReflectAttemptEvent(uid, component, false, target);
        RaiseLocalEvent(target, ref reflectEv);
        if (reflectEv.Cancelled)
        {
            _projectile.SetShooter(uid, component, target);
            _gun.SetTarget(uid, null, out _);
            component.IgnoredEntities.Clear();
            return;
        }

        var hitEv = new ProjectileHitEvent(
            component.Damage * _damageable.UniversalProjectileDamageModifier,
            target,
            component.Shooter);
        RaiseLocalEvent(uid, ref hitEv);

        var damageRequired = FixedPoint2.Zero;
        if (TryComp<DamageableComponent>(target, out var damageable))
        {
            damageRequired = FixedPoint2.Max(damageRequired - damageable.TotalDamage, FixedPoint2.Zero);
        }

        var modifiedDamage = damageable != null ? hitEv.Damage : null;
        var deleted = Deleted(target);

        if (modifiedDamage is not null)
        {
            component.ProjectileSpent = !TryPenetrate((uid, component), target, modifiedDamage, damageRequired);
        }
        else
        {
            component.ProjectileSpent = true;
        }

        if (component.Penetrate)
        {
            component.IgnoredEntities.Add(target);
            component.ProjectileSpent = false;
        }

        if (!deleted)
        {
            PlayImpactSound(target, modifiedDamage, component.SoundHit, component.ForceSound, component.Shooter);

            if (!ourBody.LinearVelocity.IsLengthZero() && _timing.IsFirstTimePredicted)
                _recoil.KickCamera(target, ourBody.LinearVelocity.Normalized());
        }

        if ((component.DeleteOnCollide && component.ProjectileSpent) ||
            (component.NoPenetrateMask & otherFixture.CollisionLayer) != 0)
        {
            var deleteEv = new DeletingProjectileEvent(uid);
            RaiseLocalEvent(ref deleteEv);
            PredictedQueueDel(uid);
        }

        if (component.ImpactEffect != null && TryComp(uid, out TransformComponent? xform) && _timing.IsFirstTimePredicted)
            RaiseLocalEvent(new ImpactEffectEvent(component.ImpactEffect, GetNetCoordinates(xform.Coordinates)));
    }

    private bool TryPenetrate(Entity<ProjectileComponent> projectile, EntityUid target, DamageSpecifier damage, FixedPoint2 damageRequired)
    {
        var component = projectile.Comp;
        if (TryComp<PenetratableComponent>(target, out var penetratable))
        {
            if (component.PenetrationThreshold < penetratable.PenetrateDamage)
                return false;

            component.PenetrationThreshold -= FixedPoint2.New(penetratable.PenetrateDamage);
            component.Damage *= (1 - penetratable.DamagePenaltyModifier);
            return true;
        }

        if (component.PenetrationThreshold == 0)
            return false;

        if (component.PenetrationDamageTypeRequirement != null)
        {
            foreach (var requiredDamageType in component.PenetrationDamageTypeRequirement)
            {
                if (!damage.DamageDict.Keys.Contains(requiredDamageType))
                    return false;
            }
        }

        if (damage.GetTotal() < damageRequired)
            return false;

        if (!component.ProjectileSpent)
        {
            component.PenetrationAmount += damageRequired;
            if (component.PenetrationAmount >= component.PenetrationThreshold)
                return false;
        }

        return true;
    }

    private void PlayImpactSound(EntityUid target, DamageSpecifier? damage, SoundSpecifier? weaponSound, bool forceWeaponSound, EntityUid? user)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        if (weaponSound != null)
            _audio.PlayPredicted(weaponSound, target, user);
    }
}
