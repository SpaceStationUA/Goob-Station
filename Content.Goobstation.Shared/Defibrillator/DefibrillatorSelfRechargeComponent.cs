// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Goobstation.Shared.Defibrillator;

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
}
