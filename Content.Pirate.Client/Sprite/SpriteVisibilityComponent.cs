// SPDX-License-Identifier: AGPL-3.0-or-later
// Pirate - ported from Trauma Station

namespace Content.Pirate.Client.Sprite;

/// <summary>
/// Controls sprite visibility, used to avoid conflicts for different systems/overlays modifying alpha
/// </summary>
[RegisterComponent]
public sealed partial class SpriteVisibilityComponent : Component
{
    /// <summary>
    /// Source key -> alpha value [0, 1)
    /// Final alpha is calculated by multiplying the values
    /// If final alpha is 0, sprite.Visible is set to false
    /// </summary>
    [DataField]
    public Dictionary<string, float> VisibilityModifiers = new();

    /// <summary>
    /// Last aggregate alpha applied by this system, used to detect visibility changes made outside it.
    /// </summary>
    public float AppliedAlpha = 1f;
}
