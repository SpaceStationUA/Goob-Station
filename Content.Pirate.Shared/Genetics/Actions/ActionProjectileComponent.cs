// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Serialization;


namespace Content.Pirate.Shared.Genetics.Actions;

/// <summary>
/// Component that stores the action container for projectiles shot by certain actions.
/// Currently used by <see cref="ShootOrganActionComponent"/>.
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class ActionProjectileComponent : Component
{
    /// <summary>
    /// The action container used to shoot this projectile.
    /// Not guaranteed to always exist, e.g. if a mutation is removed.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Container;
}
