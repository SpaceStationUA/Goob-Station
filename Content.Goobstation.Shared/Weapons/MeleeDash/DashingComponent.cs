// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Goobstation.Shared.Weapons.MeleeDash;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DashingComponent : Component
{
    [DataField, AutoNetworkedField]
    // Pirate: a hit may outlive its target during replication.
    public HashSet<NetEntity> HitEntities = new();

    [DataField, AutoNetworkedField]
    public EntityUid? Weapon;
}