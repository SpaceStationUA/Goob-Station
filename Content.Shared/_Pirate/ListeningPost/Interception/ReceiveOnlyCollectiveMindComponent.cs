// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Starlight.CollectiveMind;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.ListeningPost.Interception;

/// <summary>
/// Grants a collective-mind channel for interception while preventing transmission to it.
/// Lives on the listener, not on the item: the syndicate interception headset hands it out
/// with ClothingGrantComponent so it lasts exactly as long as the headset is worn.
/// </summary>
[RegisterComponent]
public sealed partial class ReceiveOnlyCollectiveMindComponent : Component
{
    [DataField]
    public ProtoId<CollectiveMindPrototype> Channel = "Binary";

    /// <summary>
    /// Whether this component is what added <see cref="Channel"/>, so giving up the headset does not
    /// strip a channel the listener already had from somewhere else.
    /// </summary>
    [ViewVariables]
    public bool GrantedChannel;
}
