// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Objectives.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Pirate.Ghost.Roles.Components;

/// <summary>
/// Grants objectives after a player successfully takes a ghost role.
/// </summary>
[RegisterComponent]
public sealed partial class GrantObjectivesOnGhostTakeoverComponent : Component
{
    [DataField(required: true)]
    public List<EntProtoId<ObjectiveComponent>> Objectives = new();
}
