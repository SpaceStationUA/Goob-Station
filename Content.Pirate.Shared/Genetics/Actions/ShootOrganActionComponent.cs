// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Serialization;

using Content.Shared.Actions;
using Content.Shared.Polymorph;

namespace Content.Pirate.Shared.Genetics.Actions;

/// <summary>
/// Action component for polymorphing an organ of the performer into a projectile and shooting it at the target.
/// The projectile will have <see cref="ActionProjectileComponent"/> set to the action's container.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ShootOrganActionComponent : Component
{
    [DataField(required: true)]
    public string Organ = default!;

    [DataField(required: true)]
    public ProtoId<PolymorphPrototype> Polymorph;
}

public sealed partial class ShootOrganActionEvent : WorldTargetActionEvent;
