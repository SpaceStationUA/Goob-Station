// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Pirate.Common.Bloodstream;

/// <summary>
/// Raised on an entity before blood regeneration is applied.
/// Allows other systems to modify or cancel the regeneration amount.
/// </summary>
[ByRefEvent]
public struct BloodRegenerationModifierEvent
{
    /// <summary>
    /// The amount of blood to regenerate. Systems can modify this value.
    /// Set to zero to cancel regeneration entirely.
    /// Negative values will drain blood.
    /// </summary>
    public float Amount;

    public BloodRegenerationModifierEvent(float amount)
    {
        Amount = amount;
    }
}
