// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Paper;
using Content.Shared.Radio;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.ListeningPost.DropConsole;

[RegisterComponent]
public sealed partial class SyndicateDropDispatcherComponent : Component
{

    [DataField]
    public TimeSpan MinInterval = TimeSpan.FromMinutes(5);

    [DataField]
    public TimeSpan MaxInterval = TimeSpan.FromMinutes(30);

    [DataField]
    public EntProtoId DropPrototype = "SyndieDeadDropSpawner";

    [DataField]
    public LocId StationAnnouncement = "station-syndicate-dead-drop-announcement";

    [DataField]
    public Color AnnouncementColor = Color.Gold;

    [DataField]
    public SoundSpecifier AnnouncementSound = new SoundPathSpecifier("/Audio/Announcements/attention.ogg");

    [DataField]
    public EntProtoId? PodArrivalEffect = "EffectFlashRedspace";

    [DataField]
    public EntProtoId AnnouncerPrototype = "SyndicateDropAnnouncer";

    [DataField]
    public ProtoId<RadioChannelPrototype> RadioChannel = "Syndicate";

    [DataField]
    public LocId RadioMessage = "syndicate-drop-console-radio-announcement";

    [DataField]
    public int MaxCharges = 6;

    [DataField]
    public int TargetSearchRadius = 5;

    [DataField]
    public int MaxDropHistory = 10;

    [DataField]
    public TimeSpan MinPodCooldown = TimeSpan.FromMinutes(5);

    [DataField]
    public TimeSpan MaxPodCooldown = TimeSpan.FromMinutes(20);

    [DataField]
    public LocId InterceptAnnouncement = "syndicate-drop-console-intercept-announcement";

    [DataField]
    public Color InterceptAnnouncementColor = Color.Gold;

    [DataField]
    public SoundSpecifier InterceptAnnouncementSound = new SoundPathSpecifier("/Audio/Announcements/attention.ogg");

    [DataField]
    public LocId InterceptFaxTitle = "syndicate-drop-console-intercept-fax-title";

    [DataField]
    public LocId InterceptFaxBody = "syndicate-drop-console-intercept-fax-body";

    [DataField]
    public EntProtoId<StampComponent> InterceptFaxStamp = "RubberStampCentcom";

    [DataField]
    public List<string> InterceptFaxKeywords = new()
    {
        "Капітан",
        "Captain",
        "Гл. Безпеки",
        "Голова Безпеки",
        "HoS",
        "Head of security",
        "Наглядач",
        "Warden",
        "Конф",
        "Conference",
        "Command Meeting Room",
    };



    [DataField]
    public TimeSpan NextDrop;

    [DataField]
    public bool Manual;

    [DataField]
    public EntityUid? TargetGrid;

    [DataField]
    public EntityUid? TargetStation;

    [DataField]
    public EntityUid? SelectedGrid;

    [DataField]
    public Vector2i? SelectedTile;

    [DataField]
    public int Charges;

    [DataField]
    public List<SyndicateDropRecord> DropHistory = new();

    [DataField]
    public TimeSpan PodCooldownEnd;

}
