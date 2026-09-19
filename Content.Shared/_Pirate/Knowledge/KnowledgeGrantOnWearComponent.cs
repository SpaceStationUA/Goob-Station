// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.EntityConditions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Knowledge;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class KnowledgeGrantOnWearComponent : Component
{
    [DataField]
    public EntityCondition[]? Conditions;

    [DataField, AutoNetworkedField, AlwaysPushInheritance]
    public Dictionary<EntProtoId, int> Skills = new();

    [DataField]
    public bool Examinable;
}
