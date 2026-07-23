using Robust.Shared.Player;
using Robust.Shared.Map;

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

    [ViewVariables]
    public MapCoordinates? PreviousPosition;
}
