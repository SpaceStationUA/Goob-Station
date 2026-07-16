// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.Nuclear.Holosign;

/// <summary>
/// Marker used by charge holoprojectors to reclaim radiation barriers.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HoloRadiationblockerComponent : Component;
