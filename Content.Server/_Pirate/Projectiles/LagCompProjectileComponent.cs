using Robust.Shared.Player;

namespace Content.Server._Pirate.Projectiles;

[RegisterComponent]
public sealed partial class LagCompProjectileComponent : Component
{
    [ViewVariables]
    public ICommonSession? ShooterSession;

    [DataField]
    public EntityUid Shooter;

    [ViewVariables]
    public HashSet<EntityUid> Targets = new();
}
