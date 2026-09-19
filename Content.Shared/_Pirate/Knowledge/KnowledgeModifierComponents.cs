// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Knowledge;

[RegisterComponent]
public sealed partial class KnowledgeTemporaryModifierSourcesComponent : Component
{
    [ViewVariables]
    public Dictionary<EntityUid, int> EntitySources = new();

    [ViewVariables]
    public Dictionary<string, int> NamedSources = new();
}

[RegisterComponent]
public sealed partial class KnowledgeCompetencyComponent : Component
{
    [DataField]
    public Dictionary<EntProtoId, int> Minimums = new();
}
