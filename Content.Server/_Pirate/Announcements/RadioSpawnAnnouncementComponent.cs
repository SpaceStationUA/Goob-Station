// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Radio;
using Robust.Shared.Prototypes;

namespace Content.Server._Pirate.Announcements;

/// <summary>Announces ghost-role spawns over radio.</summary>
[RegisterComponent, Access(typeof(RadioSpawnAnnouncementSystem))]
public sealed partial class RadioSpawnAnnouncementComponent : Component
{
    [DataField(required: true)]
    public ProtoId<RadioChannelPrototype> Channel;

    [DataField(required: true)]
    public LocId Message;

    [DataField]
    public LocId? SenderName;

    [DataField]
    public LocId? Job;
}
