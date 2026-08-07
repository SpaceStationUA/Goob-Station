// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Pirate.Common.Bloodstream;

/// <summary>
/// Pirate: Marks an entity as having a blood deficiency.
/// Blood regeneration is set to a negative amount (blood drain).
/// Once blood drops below the BloodlossThreshold (80%), the existing bloodloss damage system kicks in.
/// </summary>
[RegisterComponent]
public sealed partial class BloodDeficiencyComponent : Component
{
    /// <summary>
    /// How much blood is drained per regeneration tick (negative = drain).
    /// The event amount is set to this value, overriding normal regeneration.
    /// </summary>
    [DataField]
    public float DrainPerTick = -0.05f;
}
