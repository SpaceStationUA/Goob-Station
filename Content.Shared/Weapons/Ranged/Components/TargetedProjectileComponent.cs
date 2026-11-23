// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.Weapons;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Weapons.Ranged.Components;

[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedGunSystem), typeof(TargetedProjectileStateSystem))]
public sealed partial class TargetedProjectileComponent : Component
{
    [DataField]
    public EntityUid? Target; // Goob edit - if null it hits everything
}

// Pirate: a projectile can outlive its target between firing and PVS serialization.
[Serializable, NetSerializable]
public sealed class TargetedProjectileState(NetEntity? target) : ComponentState
{
    public NetEntity? Target { get; } = target;
}
