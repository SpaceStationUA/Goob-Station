// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.Mobs;

/// <summary>Marker maintained from MobState events for dead-mob whitelists.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DeadMobComponent : Component;
