// SPDX-License-Identifier: AGPL-3.0-or-later

// Pirate: explicit imports supplied by Trauma global usings.
using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Serialization;

using Content.Pirate.Shared.Genetics.Mutations;

namespace Content.Pirate.Shared.Genetics.Console;

/// <summary>
/// A disk storing genetics data.
/// It can store either a mutation or unique enzymes, but not both.
/// This is for the geneticist what an id card is for HoP.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(GeneticsDiskSystem))]
[AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class GeneticsDiskComponent : Component
{
    /// <summary>
    /// The mutation stored on this disk.
    /// It can be set by a genetics console while a mutated mob is in the scanner.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntProtoId<MutationComponent>? Mutation;

    /// <summary>
    /// The unique enzymes stored on this disk.
    /// It can be set by a genetics console while a scanned mutatable mob is in the scanner.
    /// </summary>
    [DataField, AutoNetworkedField]
    public UniqueEnzymes? Enzymes;
}
