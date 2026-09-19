// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Radio;
using Robust.Shared.Prototypes;

namespace Content.Server._Pirate.Traitor;

/// <summary>
/// Makes an extraction beacon broadcast a radio message whenever a fultoned entity is delivered to it.
/// Goes next to <see cref="Content.Shared._Pirate.Traitor.ExtractionBeaconComponent"/>.
/// </summary>
[RegisterComponent, Access(typeof(ExtractionAnnouncementSystem))]
public sealed partial class ExtractionAnnouncementComponent : Component
{
    /// <summary>
    /// Channel the delivery gets announced on.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<RadioChannelPrototype> Channel;

    /// <summary>
    /// Message to broadcast, gets the delivered entity as $name.
    /// </summary>
    [DataField(required: true)]
    public LocId Message;

    /// <summary>
    /// Name the broadcast is signed with, instead of the beacon's own entity name.
    /// </summary>
    [DataField]
    public LocId? SenderName;
}
