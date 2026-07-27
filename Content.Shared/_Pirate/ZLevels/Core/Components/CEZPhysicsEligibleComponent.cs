/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

namespace Content.Shared._Pirate.ZLevels.Core.Components;

/// <summary>
/// Marks a prototype as eligible for Z-physics without keeping the full networked runtime state
/// on entities outside a traversal context.
/// </summary>
[RegisterComponent]
public sealed partial class CEZPhysicsEligibleComponent : Component
{
    [DataField]
    public float Bounciness = 0.3f;

    [DataField]
    public float GravityMultiplier = 1f;

    [DataField]
    public bool AutoStep = true;
}
