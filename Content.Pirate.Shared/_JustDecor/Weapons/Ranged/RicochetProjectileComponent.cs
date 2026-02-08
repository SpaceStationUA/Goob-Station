using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Pirate.Shared._JustDecor.Weapons.Ranged;

/// <summary>
/// Component for projectiles that can ricochet off walls to hit a target.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RicochetProjectileComponent : Component
{
    /// <summary>
    /// The target entity that the projectile should try to hit via ricochets.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Target;

    /// <summary>
    /// Maximum number of bounces allowed before the projectile stops ricocheting.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int MaxBounces = 4;

    /// <summary>
    /// Current number of bounces that have occurred.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int CurrentBounces = 0;

    /// <summary>
    /// Planned waypoints for the ricochet path (wall hit positions).
    /// </summary>
    [DataField]
    public List<Vector2> PlannedPath = new();

    /// <summary>
    /// Whether the projectile should follow the planned path.
    /// If false, the projectile will calculate ricochets dynamically on each bounce.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool FollowPlannedPath = true;

    /// <summary>
    /// Minimum speed required for ricochet to occur. Below this speed, projectile will stop.
    /// </summary>
    [DataField]
    public float MinimumRicochetSpeed = 5f;

    /// <summary>
    /// Speed multiplier applied after each bounce (energy loss).
    /// </summary>
    [DataField]
    public float SpeedRetentionOnBounce = 0.95f;

    /// <summary>
    /// How strongly the projectile steers towards its target every frame.
    /// 0.0 means no steering, 1.0 means instant snap.
    /// </summary>
    [DataField]
    public float SteeringStrength = 1.0f; // High default for 100% hits

    /// <summary>
    /// Delay before homing starts (seconds).
    /// </summary>
    [DataField]
    public float HomingDelay = 0.2f;

    /// <summary>
    /// Current accumulator for homing delay.
    /// </summary>
    [ViewVariables]
    public float HomingAccumulator = 0f;

    /// <summary>
    /// How much speed to add per bounce.
    /// </summary>
    [DataField]
    public float SpeedBonusPerBounce = 5f;

    /// <summary>
    /// Lifetime bonus applied after each bounce.
    /// </summary>
    [DataField]
    public float BounceLifetimeBonus = 3f;

    /// <summary>
    /// Target number of bounces before hitting the final target.
    /// </summary>
    [DataField]
    public int TargetBounces = 0;
}
