// SPDX-License-Identifier: AGPL-3.0-or-later

// Pirate: explicit imports supplied by Trauma global usings.
using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Serialization;

using Content.Shared.Radio;

namespace Content.Pirate.Shared.Genetics.Abilities;

/// <summary>
/// Gives the mutated mob intrinsic radio channels.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(RadioMutationSystem))]
[AutoGenerateComponentState]
public sealed partial class RadioMutationComponent : Component
{
    [DataField(required: true)]
    public List<ProtoId<RadioChannelPrototype>> Channels = new();

    /// <summary>
    /// Active radio channels that were added and not already present.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<ProtoId<RadioChannelPrototype>> AddedActive = new();

    /// <summary>
    /// Transmitter channels that were added and not already present.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<ProtoId<RadioChannelPrototype>> AddedTransmitters = new();

    [DataField, AutoNetworkedField]
    public bool CreatedReceiver;

    [DataField, AutoNetworkedField]
    public bool CreatedActive;

    [DataField, AutoNetworkedField]
    public bool CreatedTransmitter;
}
