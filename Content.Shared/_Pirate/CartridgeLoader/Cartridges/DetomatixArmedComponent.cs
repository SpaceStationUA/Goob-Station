// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.CartridgeLoader.Cartridges;

/// <summary>Client marker for an armed device; the countdown remains server-side.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DetomatixArmedComponent : Component;
