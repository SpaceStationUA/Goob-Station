using Content.Goobstation.Common.Projectiles;
using Content.Shared.Body.Components;
using Content.Shared._Shitmed.Targeting;
using Robust.Shared.Map;

namespace Content.Shared.Weapons.Ranged.Systems;

public abstract partial class SharedGunSystem
{
    [Dependency] private readonly EntityLookupSystem _lookupPirate = default!;

    private readonly HashSet<Entity<BodyComponent>> _predictedBodies = new();

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
}
