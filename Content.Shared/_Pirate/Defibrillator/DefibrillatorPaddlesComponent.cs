// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.Defibrillator;

/// <summary>
/// Marks an item as defibrillator shock paddles that belong to a belt defibrillator.
/// Using the paddles on a target triggers the parent belt's defibrillator zap logic
/// (power cell, cooldown, sounds), exactly like SS13. The paddles snap back into the
/// belt when dropped or when the holder moves out of range.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DefibrillatorPaddlesComponent : Component
{
    /// <summary>
    /// The belt defibrillator these paddles belong to, set when they are spawned inside its paddles slot.
    /// </summary>
    [DataField]
    public EntityUid? Belt;

    /// <summary>
    /// How far (in meters) the holder may move away from the belt before the paddles snap back.
    /// </summary>
    [DataField]
    public float SnapBackRange = 1.5f;
}
