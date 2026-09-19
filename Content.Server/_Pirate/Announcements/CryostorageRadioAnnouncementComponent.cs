// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Radio;
using Robust.Shared.Prototypes;

namespace Content.Server._Pirate.Announcements;

/// <summary>Announces cryostorage departures over radio.</summary>
[RegisterComponent, Access(typeof(CryostorageRadioAnnouncementSystem))]
public sealed partial class CryostorageRadioAnnouncementComponent : Component
{
    [DataField(required: true)]
    public ProtoId<RadioChannelPrototype> Channel;

    [DataField(required: true)]
    public LocId Message;

    [DataField]
    public LocId? SenderName;

    [DataField]
    public LocId UnknownJob = "earlyleave-cryo-job-unknown";
}

/// <summary>Raised before a cryostorage departure announcement.</summary>
[ByRefEvent]
public record struct CryostorageAnnounceAttemptEvent(EntityUid Stored, string StoredName, bool Handled = false);
