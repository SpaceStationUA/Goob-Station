using System.Numerics;
using Content.Goobstation.Common.Weapons.Ranged;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Map;

namespace Content.Shared.Weapons.Ranged.Systems;

public abstract partial class SharedGunSystem
{
    [Dependency] private readonly SharedMapSystem _mapPirate = default!;

    protected virtual void Recoil(EntityUid? user, Vector2 recoil, float recoilScalar)
    {
    }

    protected virtual void HandleHitscanShot(
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
        if (Timing.IsFirstTimePredicted)
            Audio.PlayPredicted(gun.SoundGunshotModified, gunUid, user);
    }

    protected void SharedShoot(
        EntityUid gunUid,
        GunComponent gun,
        List<(EntityUid? Entity, IShootable Shootable)> ammo,
        EntityCoordinates fromCoordinates,
        EntityCoordinates toCoordinates,
        out bool userImpulse,
        EntityUid? user = null,
        bool throwItems = false)
    {
        userImpulse = false;

        if (user != null)
        {
            var selfEvent = new SelfBeforeGunShotEvent(user.Value, (gunUid, gun), ammo);
            RaiseLocalEvent(user.Value, selfEvent);
            if (selfEvent.Cancelled)
                return;
        }

        var fromMap = TransformSystem.ToMapCoordinates(fromCoordinates);
        var toMap = TransformSystem.ToMapCoordinates(toCoordinates).Position;
        var mapDirection = toMap - fromMap.Position;
        if (mapDirection == Vector2.Zero)
            return;

        var mapAngle = mapDirection.ToAngle();
        var angle = GetPredictedRecoilAngle(Timing.CurTime, (gunUid, gun), mapAngle, user);
        userImpulse = true;

        var fromEnt = MapManager.TryFindGridAt(fromMap, out var gridUid, out _)
            ? TransformSystem.WithEntityId(fromCoordinates, gridUid)
            : new EntityCoordinates(_mapPirate.GetMapOrInvalid(fromMap.MapId), fromMap.Position);

        var toMapBeforeRecoil = toMap;
        toMap = fromMap.Position + angle.ToVec() * mapDirection.Length();
        mapDirection = toMap - fromMap.Position;
        var gunVelocity = Physics.GetMapLinearVelocity(fromEnt);
        var shotProjectiles = new List<EntityUid>(ammo.Count);

        foreach (var (ent, shootable) in ammo)
        {
            if (throwItems && ent != null)
            {
                ShootOrThrowPredicted(ent.Value, mapDirection, gunVelocity, gun, gunUid, user, toMapBeforeRecoil);
                shotProjectiles.Add(ent.Value);

                if (userImpulse)
                    Recoil(user, mapDirection, gun.CameraRecoilScalarModified);

                continue;
            }

            switch (shootable)
            {
                case CartridgeAmmoComponent cartridge:
                    if (!cartridge.Spent)
                    {
                        SetCartridgeSpent(ent!.Value, cartridge, true);

                        var projectile = PredictedSpawnAtPosition(cartridge.Prototype, fromEnt);
                        CreateAndFireProjectiles(projectile, cartridge);

                        RaiseLocalEvent(ent.Value, new AmmoShotEvent
                        {
                            FiredProjectiles = shotProjectiles,
                        });

                        if (cartridge.DeleteOnSpawn)
                        {
                            PredictedDel(ent.Value);
                        }
                        else if (!Containers.IsEntityInContainer(ent.Value))
                        {
                            EjectCartridgePredicted(PredictedRandom(gunUid), user, ent.Value, angle);
                        }
                    }
                    else
                    {
                        userImpulse = false;
                        Audio.PlayPredicted(gun.SoundEmpty, gunUid, user);
                    }

                    Dirty(ent!.Value, cartridge);
                    break;
                case AmmoComponent newAmmo:
                    if (ent == null)
                        break;

                    CreateAndFireProjectiles(ent.Value, newAmmo);
                    break;
                case HitscanPrototype hitscan:
                    HandleHitscanShot(
                        gunUid,
                        gun,
                        hitscan,
                        fromCoordinates,
                        fromCoordinates,
                        fromMap,
                        toMapBeforeRecoil,
                        mapDirection,
                        user,
                        ref userImpulse,
                        shotProjectiles);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (userImpulse)
                Recoil(user, mapDirection, gun.CameraRecoilScalarModified);
        }

        RaiseLocalEvent(gunUid, new AmmoShotEvent
        {
            FiredProjectiles = shotProjectiles,
        });

        if (user is { } userUid)
        {
            var userEv = new AmmoShotUserEvent
            {
                Gun = gunUid,
                FiredProjectiles = shotProjectiles,
            };
            RaiseLocalEvent(userUid, userEv);
        }

        void CreateAndFireProjectiles(EntityUid ammoEnt, AmmoComponent ammoComp)
        {
            if (TryComp<ProjectileSpreadComponent>(ammoEnt, out var ammoSpreadComp))
            {
                var spreadEvent = new GunGetAmmoSpreadEvent(ammoSpreadComp.Spread);
                RaiseLocalEvent(gunUid, ref spreadEvent);

                var angles = LinearSpreadPredicted(
                    mapAngle - spreadEvent.Spread / 2,
                    mapAngle + spreadEvent.Spread / 2,
                    ammoSpreadComp.Count);

                ShootOrThrowPredicted(ammoEnt, angles[0].ToVec(), gunVelocity, gun, gunUid, user, toMapBeforeRecoil);
                shotProjectiles.Add(ammoEnt);

                for (var i = 1; i < ammoSpreadComp.Count; i++)
                {
                    var pellet = PredictedSpawnAtPosition(ammoSpreadComp.Proto, fromEnt);
                    SetProjectilePerfectHitEntities(pellet, user, new MapCoordinates(toMap, fromMap.MapId));
                    ShootOrThrowPredicted(pellet, angles[i].ToVec(), gunVelocity, gun, gunUid, user, toMapBeforeRecoil);
                    shotProjectiles.Add(pellet);
                }
            }
            else
            {
                ShootOrThrowPredicted(ammoEnt, mapDirection, gunVelocity, gun, gunUid, user, toMapBeforeRecoil);
                shotProjectiles.Add(ammoEnt);
            }

            MuzzleFlash(gunUid, ammoComp, mapDirection.ToAngle(), user);
            Audio.PlayPredicted(gun.SoundGunshotModified, gunUid, user);
        }
    }
}
