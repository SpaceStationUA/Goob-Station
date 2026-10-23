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
/// Mutation component that makes it remove some mutations when this is mutated.
/// Does nothing when this gets removed, or if the mutations weren't present.
/// </summary>
[RegisterComponent, Access(typeof(RemovesMutationSystem))]
public sealed partial class RemovesMutationComponent : Component
{
    /// <summary>
    /// The mutations to remove.
    /// </summary>
    [DataField(required: true)]
    public List<EntProtoId<MutationComponent>> Removes = new();
}
