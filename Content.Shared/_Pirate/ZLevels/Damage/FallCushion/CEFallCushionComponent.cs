using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.ZLevels.Damage.FallCushion;

/// <summary>
/// Scales fall damage and stun for entities that land on this surface.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CEFallCushionComponent : Component
{
    /// <summary>
    /// Fall damage multiplier. 0 negates damage; 1 leaves it unchanged.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float DamageMultiplier = 0f;

    /// <summary>
    /// Fall stun multiplier. 0 negates stun; 1 leaves it unchanged.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float StunMultiplier = 1f;
}
