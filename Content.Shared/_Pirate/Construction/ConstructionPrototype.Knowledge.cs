// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.Knowledge.Quality;
using Robust.Shared.Prototypes;

namespace Content.Shared.Construction.Prototypes;

public sealed partial class ConstructionPrototype
{
    /// <summary>
    /// Masteries required to understand and start this construction recipe.
    /// </summary>
    [DataField]
    public Dictionary<EntProtoId, int> Theory = new();

    /// <summary>
    /// Optional hands-on masteries used only by the resulting quality roll.
    /// </summary>
    [DataField]
    public Dictionary<EntProtoId, int>? Practical;

    /// <summary>
    /// Experience granted to the listed skills when this construction is completed.
    /// </summary>
    [DataField]
    public Dictionary<EntProtoId, int> Experience = new();

    [DataField]
    public ProtoId<QualityPrototype>? QualityPrototype;

    /// <summary>
    /// Whether the completed construction receives the Trauma quality roll.
    /// </summary>
    [DataField]
    public bool UseQuality = true;
}
