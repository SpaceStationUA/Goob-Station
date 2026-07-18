// SPDX-FileCopyrightText: 2024 Just-a-Unity-Dev <67359748+Just-a-Unity-Dev@users.noreply.github.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Polymorph;
using Robust.Shared.Prototypes;

namespace Content.Server._Pirate.Geras;

/// <summary>
/// Grants slimepeople the action used to enter their smaller geras form.
/// </summary>
[RegisterComponent]
public sealed partial class GerasComponent : Component
{
    [DataField]
    public ProtoId<PolymorphPrototype> GerasPolymorphId = "SlimeMorphGeras";

    [DataField]
    public EntProtoId GerasAction = "ActionMorphGeras";

    [DataField]
    public EntityUid? GerasActionEntity;
}
