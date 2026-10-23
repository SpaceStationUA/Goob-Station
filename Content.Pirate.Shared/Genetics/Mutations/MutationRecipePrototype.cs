// SPDX-License-Identifier: AGPL-3.0-or-later

// Pirate: explicit imports supplied by Trauma global usings.
using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Serialization;


namespace Content.Pirate.Shared.Genetics.Mutations;

/// <summary>
/// A mutation recipe for combining mutations into new ones.
/// </summary>
[Prototype]
public sealed partial class MutationRecipePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = string.Empty;

    /// <summary>
    /// The mutation produced.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId<MutationComponent> Result;

    /// <summary>
    /// Each mutation needed.
    /// This is currently hardcoded to only work with 2 mutations.
    /// </summary>
    [DataField(required: true)]
    public List<EntProtoId<MutationComponent>> Required = new();
}
