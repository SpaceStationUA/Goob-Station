// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.Defibrillator;

/// <summary>
/// Marks a defibrillator that should be completely hidden in the holder's in-hand sprite.
/// Belt defibrillators are carried as equipment, not as a visible held item.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DefibrillatorHideInHandComponent : Component
{
}
