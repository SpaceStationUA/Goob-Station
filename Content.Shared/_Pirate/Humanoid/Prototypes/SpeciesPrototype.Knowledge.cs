// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.Knowledge;
using Robust.Shared.Prototypes;

namespace Content.Shared.Humanoid.Prototypes;

public sealed partial class SpeciesPrototype
{
    /// <summary>
    /// Racial skill baseline and character customization budget.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<KnowledgeProfilePrototype> Knowledge;
}
