using System.Numerics;
using Content.Goobstation.Common.Projectiles;
using Content.Shared._Goobstation.Weapons.SmartGun;
using Content.Shared.Body.Components;
using Content.Shared.Damage;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Map;
using Robust.Shared.Physics;

namespace Content.Shared.Weapons.Ranged.Systems;

public abstract partial class SharedGunSystem
{
    [Dependency] private readonly EntityLookupSystem _lookupPirate = default!;

    private readonly HashSet<Entity<BodyComponent>> _predictedBodies = new();
    private readonly HashSet<Entity<DamageableComponent>> _smartTargetCandidates = new();

    private const float SmartTargetAimRadius = 0.5f;

    public TargetBodyPart? GetTargetPart(EntityUid? shooter, EntityUid target)
        => shooter is { } targeting
            ? GetTargetPart(targeting, TransformSystem.GetMapCoordinates(targeting), TransformSystem.GetMapCoordinates(target))
            : null;

    public TargetBodyPart? GetTargetPart(Entity<TargetingComponent?>? targeting, MapCoordinates shootCoords, MapCoordinates targetCoords)
    {
        if (shootCoords.MapId != targetCoords.MapId || targeting is not { } ent)
            return null;

        if (!Resolve(ent, ref ent.Comp, false))
            return null;

        var dist = (shootCoords.Position - targetCoords.Position).Length();
        var missChance = MathHelper.Lerp(0f, 1f, Math.Clamp(dist / 2f, 0f, 1f));
        return PredictedRandom(ent.Owner).NextDouble() < missChance
            ? TargetBodyPart.Chest
            : ent.Comp.Target;
    }

    public void SetProjectilePerfectHitEntities(EntityUid projectile, Entity<TargetingComponent?>? shooter, MapCoordinates coords)
    {
        if (shooter is not { } ent)
            return;

        if (!Resolve(ent, ref ent.Comp, false))
            return;

        var part = GetTargetPart(shooter, coords, TransformSystem.GetMapCoordinates(ent));
        if (part is null or TargetBodyPart.Chest)
            return;

        var comp = EnsureComp<ProjectileMissTargetPartChanceComponent>(projectile);
        _predictedBodies.Clear();
        _lookupPirate.GetEntitiesInRange(coords, 2f, _predictedBodies, LookupFlags.Dynamic);
        foreach (var (uid, _) in _predictedBodies)
        {
            comp.PerfectHitEntities.Add(uid);
        }

        Dirty(projectile, comp);
    }

    protected TargetBodyPart? GetPredictedTargetPart(Entity<TargetingComponent?>? targeting, MapCoordinates shootCoords, MapCoordinates targetCoords)
        => GetTargetPart(targeting, shootCoords, targetCoords);

    protected void SetProjectilePerfectHitEntitiesPredicted(EntityUid projectile, Entity<TargetingComponent?>? shooter, MapCoordinates coords)
        => SetProjectilePerfectHitEntities(projectile, shooter, coords);

    private EntityUid? GetPredictedSmartTarget(
        EntityUid gunUid,
        EntityUid projectile,
        EntityUid? shooter,
        Vector2? targetCoordinates,
        out string source,
        out float distance)
    {
        source = "none";
        distance = float.NaN;

        if (!TryComp<SmartGunComponent>(gunUid, out var smartGun))
            return null;

        if (TryComp<GunComponent>(gunUid, out var gun) &&
            gun.Target is { } requestedTarget &&
            !TerminatingOrDeleted(requestedTarget))
        {
            source = "request";
            distance = 0f;
            return requestedTarget;
        }

        var wielded = TryComp<WieldableComponent>(gunUid, out var wieldable) && wieldable.Wielded;
        if (smartGun.RequiresWield && !wielded)
        {
            source = "unwielded";
            return null;
        }

        if (targetCoordinates is not { } targetPosition)
            return null;

        var projectileCoordinates = TransformSystem.GetMapCoordinates(projectile);
        if (projectileCoordinates.MapId == MapId.Nullspace)
            return null;

        var targetMapCoordinates = new MapCoordinates(targetPosition, projectileCoordinates.MapId);
        _smartTargetCandidates.Clear();
        _lookupPirate.GetEntitiesInRange(
            targetMapCoordinates,
            SmartTargetAimRadius,
            _smartTargetCandidates,
            LookupFlags.Dynamic | LookupFlags.Static | LookupFlags.Sundries);

        EntityUid? closest = null;
        var closestDistanceSquared = SmartTargetAimRadius * SmartTargetAimRadius;
        foreach (var candidate in _smartTargetCandidates)
        {
            if (candidate.Owner == projectile ||
                candidate.Owner == gunUid ||
                candidate.Owner == shooter ||
                TerminatingOrDeleted(candidate.Owner))
            {
                continue;
            }

            var candidateCoordinates = TransformSystem.GetMapCoordinates(candidate.Owner);
            if (candidateCoordinates.MapId != targetMapCoordinates.MapId)
                continue;

            var candidateDistanceSquared =
                Vector2.DistanceSquared(candidateCoordinates.Position, targetMapCoordinates.Position);
            if (candidateDistanceSquared > closestDistanceSquared)
                continue;

            closest = candidate.Owner;
            closestDistanceSquared = candidateDistanceSquared;
        }

        if (closest is not { } target)
            return null;

        source = "aim-fallback";
        distance = MathF.Sqrt(closestDistanceSquared);
        return target;
    }
}
