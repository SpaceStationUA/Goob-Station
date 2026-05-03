using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Projectiles;

[Serializable, NetSerializable]
public sealed class ShotPredictedProjectileEvent : EntityEventArgs
{
    public NetEntity Projectile;
}
