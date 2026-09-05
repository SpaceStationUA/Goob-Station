// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Pirate.Shared.Backrooms;

/// <summary>
/// Allows opening a prey list of players on the same map and tracking them with a pinpointer.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BackroomsPreySenseComponent : Component;
