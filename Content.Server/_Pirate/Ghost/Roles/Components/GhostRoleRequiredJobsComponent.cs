// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server._Pirate.Ghost.Roles.Components;

/// <summary>Applies job eligibility checks without assigning that job to the ghost role's taker.</summary>
[RegisterComponent]
public sealed partial class GhostRoleRequiredJobsComponent : Component
{
    [DataField(required: true)]
    public List<ProtoId<JobPrototype>> Jobs = new();
}
