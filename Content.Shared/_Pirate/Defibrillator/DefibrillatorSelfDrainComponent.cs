// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Pirate.Defibrillator;

/// <summary>
/// Marks a belt defibrillator whose installed power cell slowly loses charge on its own.
/// The drain happens continuously while a cell is installed, regardless of use.
/// </summary>
[RegisterComponent]
public sealed partial class DefibrillatorSelfDrainComponent : Component
{
    /// <summary>
    /// How much charge (in watts/joules) is drained from the installed power cell per second.
    /// </summary>
    [DataField]
    public float DrainPerSecond = 0.05f;
}
