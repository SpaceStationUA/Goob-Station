// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.CartridgeLoader;

/// <summary>Marks a cartridge that cannot be installed.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class NonInstallableProgramComponent : Component;
