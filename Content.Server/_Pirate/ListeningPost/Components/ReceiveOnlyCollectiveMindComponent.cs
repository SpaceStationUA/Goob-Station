// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Starlight.CollectiveMind;
using Robust.Shared.Prototypes;

namespace Content.Server._Pirate.ListeningPost.Components;

/// <summary>
/// Grants a collective-mind channel for interception while preventing transmission to it.
/// </summary>
[RegisterComponent]
public sealed partial class ReceiveOnlyCollectiveMindComponent : Component
{
    [DataField]
    public ProtoId<CollectiveMindPrototype> Channel = "Binary";
}
