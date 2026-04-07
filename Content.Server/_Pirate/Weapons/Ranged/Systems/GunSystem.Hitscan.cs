using System.Linq;
using System.Numerics;
using Content.Server.Atmos.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Database;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Reflect;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Player;

namespace Content.Server.Weapons.Ranged.Systems;

public sealed partial class GunSystem
{
    protected override void HandleHitscanShot(
        EntityUid gunUid,
        GunComponent gun,
        HitscanPrototype hitscan,
        EntityCoordinates fromCoordinates,
        EntityCoordinates fromEffect,
        MapCoordinates fromMap,
        Vector2 toMapBeforeRecoil,
        Vector2 mapDirection,
        EntityUid? user,
        ref bool userImpulse,
        List<EntityUid> shotProjectiles)
    {
        EntityUid? lastHit = null;

        var from = fromMap;
        var effectCoordinates = fromEffect;
        var dir = mapDirection.Normalized();
        var lastUser = user ?? gunUid;

        if (hitscan.Reflective != ReflectType.None)
        {
            for (var reflectAttempt = 0; reflectAttempt < 3; reflectAttempt++)
            {
                var ray = new CollisionRay(from.Position, dir, hitscan.CollisionMask);
                var rayCastResults = Physics.IntersectRay(from.MapId, ray, hitscan.MaxLength, lastUser, false).ToList();
                if (!rayCastResults.Any())
                    break;

                var result = rayCastResults[0];

                if (!_container.IsEntityOrParentInContainer(lastUser))
                {
                    foreach (var collide in rayCastResults)
                    {
                        if (collide.HitEntity != gun.Target &&
                            CompOrNull<RequireProjectileTargetComponent>(collide.HitEntity)?.Active == true &&
                            (_transform.GetMapCoordinates(collide.HitEntity).Position - toMapBeforeRecoil).Length() > _crawlHitzoneSize)
                        {
                            continue;
                        }

                        result = collide;
                        break;
                    }
                }

                var hit = result.HitEntity;
                lastHit = hit;

                FireEffects(effectCoordinates, result.Distance, dir.Normalized().ToAngle(), hitscan, hit, user);

                var ev = new HitScanReflectAttemptEvent(user, gunUid, hitscan.Reflective, dir, false, hitscan.Damage, hit);
                RaiseLocalEvent(hit, ref ev);

                if (!ev.Reflected)
                    break;

                effectCoordinates = Transform(hit).Coordinates;
                from = TransformSystem.ToMapCoordinates(effectCoordinates);
                dir = ev.Direction;
                lastUser = hit;
            }
        }

        if (lastHit != null)
        {
            var hitEntity = lastHit.Value;
            if (hitscan.StaminaDamage > 0f)
                _stamina.TakeStaminaDamage(hitEntity, hitscan.StaminaDamage, source: user, applyResistances: true);

            if (hitscan.FireStacks > 0f && TryComp<FlammableComponent>(hitEntity, out var flammable) && flammable != null)
                _flammable.AdjustFireStacks(hitEntity, hitscan.FireStacks, flammable, true);

            var dmg = hitscan.Damage;
            var hitName = ToPrettyString(hitEntity);
            if (dmg != null)
            {
                dmg = Damageable.TryChangeDamage(hitEntity,
                    dmg * Damageable.UniversalHitscanDamageModifier,
                    origin: user,
                    targetPart: GetTargetPart(lastUser,
                        new MapCoordinates(toMapBeforeRecoil, fromMap.MapId),
                        _transform.GetMapCoordinates(hitEntity)),
                    canBeCancelled: true);
            }

            if (dmg != null)
            {
                if (!Deleted(hitEntity))
                {
                    if (dmg.AnyPositive())
                        _color.RaiseEffect(Color.Red, new List<EntityUid> { hitEntity }, Filter.Pvs(hitEntity, entityManager: EntityManager));

                    PlayImpactSound(hitEntity, dmg, hitscan.Sound, hitscan.ForceSound);
                }

                if (user != null)
                {
                    Logs.Add(LogType.HitScanHit,
                        $"{ToPrettyString(user.Value):user} hit {hitName:target} using hitscan and dealt {dmg.GetTotal():damage} damage");
                }
                else
                {
                    Logs.Add(LogType.HitScanHit,
                        $"{hitName:target} hit by hitscan dealing {dmg.GetTotal():damage} damage");
                }
            }
        }
        else
        {
            FireEffects(effectCoordinates, hitscan.MaxLength, dir.ToAngle(), hitscan, user: user);
        }

        Audio.PlayPredicted(gun.SoundGunshotModified, gunUid, user);
    }
}
