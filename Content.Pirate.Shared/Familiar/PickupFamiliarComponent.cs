// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Pirate.Shared.Familiar;

/// <summary>
/// Makes the first mob that picks up this entity its master.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class PickupFamiliarComponent : Component;
