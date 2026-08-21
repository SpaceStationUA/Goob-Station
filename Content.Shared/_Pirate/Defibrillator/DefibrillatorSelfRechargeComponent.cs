// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Pirate.Defibrillator;

/// <summary>
/// Marks a belt defibrillator that slowly recharges its installed power cell on its own,
/// like an experimental self-recharging battery. Works regardless of which cell is installed.
/// </summary>
[RegisterComponent]
public sealed partial class DefibrillatorSelfRechargeComponent : Component
{
    /// <summary>
    /// How much charge (in watts/joules) is added to the installed power cell per second.
    /// </summary>
    [DataField]
    public float RechargePerSecond = 0.1f;

    /// <summary>
    /// Per-entity accumulated time since the last recharge tick. Replaced the shared
    /// system-level accumulator so that each defibrillator tracks its own recharge
    /// independently (e.g. after a battery swap resets this to zero).
    /// </summary>
    [DataField]
    public float AccumulatedTime;
}
