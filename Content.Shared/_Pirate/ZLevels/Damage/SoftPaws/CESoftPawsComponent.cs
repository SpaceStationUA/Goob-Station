/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.ZLevels.Damage.SoftPaws;

/// <summary>
/// Reduces fall damage and removes stun if the fall speed does not exceed a certain limit.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CESoftPawsComponent : Component
{
    /// <summary>
    /// The fall speed must be less than this for damage reduction and stun to start working.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MaxSpeedLimit = 1f;

    /// <summary>
    /// Scales the base fall damage applied when the entity lands at or below <see cref="MaxSpeedLimit"/>.
    /// 0 means no damage; 1 leaves the damage unchanged.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float DamageMultiplier = 0f;

    /// <summary>
    /// Scales the base fall stun applied when the entity lands at or below <see cref="MaxSpeedLimit"/>.
    /// 0 disables stun; 1 leaves the stun unchanged.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float StunMultiplier = 0f;

    /// <summary>
    /// Scales the base fall damage applied when the entity lands above <see cref="MaxSpeedLimit"/>
    /// (a "hard" fall, where soft paws don't fully absorb the impact).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float DamageHardFallMultiplier = 0.5f;

    /// <summary>
    /// Scales the base fall stun applied when the entity lands above <see cref="MaxSpeedLimit"/>.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float StunHardFallMultiplier = 0.5f;
}
