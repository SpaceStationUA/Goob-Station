// SPDX-License-Identifier: AGPL-3.0-or-later

// Pirate: explicit imports supplied by Trauma global usings.
using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Serialization;

using Content.Shared.Tag;

namespace Content.Pirate.Shared.Genetics.Mutations;

/// <summary>
/// Adds or removes tags to the target mob.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TagMutationComponent : Component
{
    [DataField]
    public List<ProtoId<TagPrototype>> Added = new();

    [DataField]
    public List<ProtoId<TagPrototype>> Removed = new();

    [DataField, AutoNetworkedField]
    public List<ProtoId<TagPrototype>> AddedTags = new();

    [DataField, AutoNetworkedField]
    public List<ProtoId<TagPrototype>> RemovedTags = new();
}
