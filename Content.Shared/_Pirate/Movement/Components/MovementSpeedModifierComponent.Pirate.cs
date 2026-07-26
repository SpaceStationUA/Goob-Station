// SPDX-License-Identifier: AGPL-3.0-or-later
// Pirate - ported from Trauma Station

namespace Content.Shared.Movement.Components;

public sealed partial class MovementSpeedModifierComponent
{
    public static readonly Angle DefaultBackwardsAngle = Angle.FromDegrees(105);
    public const float DefaultBackwardsSpeed = 0.75f;

    /// <summary>
    /// Moving at or beyond this angle from the facing direction counts as backpedalling.
    /// </summary>
    [DataField]
    public Angle BackwardsAngle = DefaultBackwardsAngle;

    /// <summary>
    /// Speed multiplier applied while backpedalling.
    /// </summary>
    [DataField]
    public float BackwardsSpeed = DefaultBackwardsSpeed;
}
