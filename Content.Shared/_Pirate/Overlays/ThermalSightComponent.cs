// SPDX-License-Identifier: MIT
using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.Overlays;

/// <summary>
/// Makes the entity see air temperature.
/// When added to a clothing item it will also grant the wearer the same overlay.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ThermalSightComponent : Component;
