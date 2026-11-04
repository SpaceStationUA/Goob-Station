// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Preferences.Loadouts;
using Robust.Shared.Prototypes;

namespace Content.Server._Pirate.Ghost.Roles;

[RegisterComponent]
public sealed partial class GhostRoleLoadoutComponent : Component
{
    [DataField(required: true)]
    public ProtoId<RoleLoadoutPrototype> Role;

    [ViewVariables]
    public bool Applied;
}
