// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Defibrillator;

/// <summary>
/// Appearance data keys for <see cref="DefibrillatorChargeVisualsComponent"/>.
/// </summary>
[Serializable, NetSerializable]
public enum DefibrillatorChargeVisuals : byte
{
    /// <summary>
    /// Current charge level of the installed power cell, 1..4 (25/50/75/100%). 0 means the bar is hidden.
    /// </summary>
    ChargeLevel,

    /// <summary>
    /// True when the defibrillator has no power cell installed.
    /// </summary>
    NoCell,

    /// <summary>
    /// True when the defibrillator's safety protocols have been disabled (emagged).
    /// </summary>
    Emagged,
}
