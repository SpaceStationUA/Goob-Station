using System.Numerics;
using Content.Goobstation.Common.Weapons.Ranged;
using Content.Shared.Audio;
using Content.Shared.Projectiles;
using Content.Shared.Random.Helpers;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Shared.Weapons.Ranged.Systems;

public abstract partial class SharedGunSystem
{
    protected System.Random PredictedRandom(EntityUid uid)
    {
        return new System.Random(GetPredictedRandomSeed(uid));
    }

    private int GetPredictedRandomSeed(EntityUid uid)
    {
        var netEntity = GetNetEntity(uid);
        unchecked
        {
            // System.HashCode is salted per process, so it cannot be used for client/server prediction.
            // Mix the shared tick and network entity ID explicitly to produce the same seed on both sides.
            var hash = 0x50495241u;
            hash = MixPredictedRandomSeed(hash, (uint) Timing.CurTick.Value);
            hash = MixPredictedRandomSeed(hash, (uint) netEntity.Id);
            hash ^= hash >> 16;
            hash *= 0x7FEB352Du;
            hash ^= hash >> 15;
            hash *= 0x846CA68Bu;
            hash ^= hash >> 16;
            return (int) hash;
        }
    }

    private static uint MixPredictedRandomSeed(uint hash, uint value)
    {
        unchecked
        {
            return hash ^ (value + 0x9E3779B9u + (hash << 6) + (hash >> 2));
        }
    }

    protected Angle GetPredictedRecoilAngle(TimeSpan curTime, Entity<GunComponent> ent, Angle direction, EntityUid? user = null)
    {
        var (uid, component) = ent;
        var timeSinceLastFire = (curTime - component.LastFire).TotalSeconds;
        var minTheta = Math.Min(component.MinAngleModified.Theta, component.MaxAngleModified.Theta);
        var maxTheta = Math.Max(component.MinAngleModified.Theta, component.MaxAngleModified.Theta);
        var newTheta = MathHelper.Clamp(
            component.CurrentAngle.Theta + component.AngleIncreaseModified.Theta - component.AngleDecayModified.Theta * timeSinceLastFire,
            minTheta,
            maxTheta);

        component.CurrentAngle = new Angle(newTheta);
        component.LastFire = component.NextFire;

        var seed = GetPredictedRandomSeed(uid);
        var random = new System.Random(seed).NextFloat(-0.5f, 0.5f);

        var angleEv = new GetRecoilModifiersEvent
        {
            Gun = uid,
            User = user ?? uid,
        };

        if (user != null)
            RaiseLocalEvent(user.Value, angleEv);

        RaiseLocalEvent(uid, angleEv);
        random *= angleEv.Modifier;

        var spread = component.CurrentAngle.Theta * random;
        var angle = new Angle(direction.Theta + spread);

        DebugTools.Assert(Math.Abs(spread) <= maxTheta);
        return angle;
    }

    protected Angle[] LinearSpreadPredicted(Angle start, Angle end, int intervals)
    {
        var angles = new Angle[intervals];
        DebugTools.Assert(intervals > 1);

        for (var i = 0; i <= intervals - 1; i++)
        {
            angles[i] = new Angle(start + (end - start) * i / (intervals - 1));
        }

        return angles;
    }

    protected void ShootOrThrowPredicted(EntityUid uid, Vector2 mapDirection, Vector2 gunVelocity, GunComponent gun, EntityUid gunUid, EntityUid? user, Vector2? targetCoordinates = null)
    {
        EntityUid? target = gun.Target is { } requestedTarget && !TerminatingOrDeleted(requestedTarget)
            ? requestedTarget
            : null;

        if (HasComp<Content.Shared._Goobstation.Weapons.SmartGun.SmartGunComponent>(gunUid))
        {
            target = GetPredictedSmartTarget(
                gunUid,
                uid,
                user,
                targetCoordinates,
                out _,
                out _);

            if (target is { } smartTarget)
                gun.Target = smartTarget;
        }

        if (target is { } validTarget)
            SetTarget(uid, validTarget, out _);

        if (!HasComp<ProjectileComponent>(uid))
        {
            RemoveShootable(uid);
            ThrowingSystem.TryThrow(uid, mapDirection, gun.ProjectileSpeedModified, user);
            return;
        }

        ShootProjectile(uid, mapDirection, gunVelocity, gunUid, user, gun.ProjectileSpeedModified, targetCoordinates);
    }

    protected void CycleBallisticPredicted(EntityUid uid, BallisticAmmoProviderComponent component, MapCoordinates coordinates, EntityUid? user = null)
    {
        if (component.Entities.Count > 0)
        {
            var existing = component.Entities[^1];
            component.Entities.RemoveAt(component.Entities.Count - 1);
            DirtyField(uid, component, nameof(BallisticAmmoProviderComponent.Entities));

            Containers.Remove(existing, component.Container);
            EnsureShootable(existing);
        }
        else if (component.UnspawnedCount > 0)
        {
            component.UnspawnedCount--;
            DirtyField(uid, component, nameof(BallisticAmmoProviderComponent.UnspawnedCount));
            var ent = EntityManager.PredictedSpawn(component.Proto, coordinates);
            EnsureShootable(ent);
            EjectCartridgePredicted(PredictedRandom(uid), user, ent);
        }

        var cycledEvent = new GunCycledEvent();
        RaiseLocalEvent(uid, ref cycledEvent);
    }

    protected void EjectCartridgePredicted(System.Random rand, EntityUid? user, EntityUid entity, Angle? angle = null, bool playSound = true)
    {
        var offsetPos = rand.NextAngle().RotateVec(new Vector2(rand.NextFloat(0, EjectOffset), 0));
        var xform = Transform(entity);

        var coordinates = xform.Coordinates.Offset(offsetPos);
        TransformSystem.SetLocalRotation(entity, rand.NextAngle(), xform);
        TransformSystem.SetCoordinates(entity, xform, coordinates);

        if (angle != null)
        {
            var ejectAngle = angle.Value + 3.7f;
            ThrowingSystem.TryThrow(entity, ejectAngle.ToVec().Normalized() / 100, 5f);
        }

        if (playSound && TryComp<CartridgeAmmoComponent>(entity, out var cartridge))
        {
            Audio.PlayPredicted(
                cartridge.EjectSound,
                entity,
                user,
                AudioParams.Default.WithVariation(SharedContentAudioSystem.DefaultVariation).WithVolume(-1f));
        }
    }
}
