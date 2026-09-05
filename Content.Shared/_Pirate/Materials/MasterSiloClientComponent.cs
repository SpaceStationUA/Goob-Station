// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.Materials;

/// <summary>
/// Marks a material storage as a possible receiver for a master material distributor.
/// The telesphere keeps this marker for compatibility with the Trauma crafting port.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class MasterSiloClientComponent : Component;
