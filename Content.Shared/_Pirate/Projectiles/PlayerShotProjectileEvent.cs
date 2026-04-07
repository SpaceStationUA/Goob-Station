namespace Content.Shared._Pirate.Projectiles;

[ByRefEvent]
public readonly record struct PlayerShotProjectileEvent(EntityUid Projectile, EntityUid User);
