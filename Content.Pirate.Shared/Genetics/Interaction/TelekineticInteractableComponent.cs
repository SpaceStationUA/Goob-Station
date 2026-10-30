using Robust.Shared.GameStates;
// SPDX-License-Identifier: AGPL-3.0-or-later


namespace Content.Pirate.Shared.Genetics.Interaction;

/// <summary>
/// Allows interaction at any range with this entity by players with <see cref="TelekinesisComponent"/>.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class TelekineticInteractableComponent : Component;
