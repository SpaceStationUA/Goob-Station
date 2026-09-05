// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameObjects;

namespace Content.Pirate.Shared.Backrooms;

/// <summary>
/// When this edible is fully eaten, teleports the eater to a random tile on the main station.
/// </summary>
[RegisterComponent]
public sealed partial class TeleportToStationOnFullyEatenComponent : Component;
