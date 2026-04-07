using System;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Shared._JustDecor.Missions.Components;

[RegisterComponent]
public sealed partial class MissionSelfDestructComponent : Component
{
    [DataField]
    public float Countdown = 300f;

    [DataField]
    public float ExplosionTotalIntensity = 400f;

    [DataField]
    public float ExplosionSlope = 30f;

    [DataField]
    public float ExplosionMaxTileIntensity = 125f;

    [DataField]
    public string ExplosionPrototype = "Default";

    [DataField]
    public List<int> AnnounceAtSeconds = new()
    {
        300,
        180,
        120,
        60,
        30,
        10,
        5,
        4,
        3,
        2,
        1
    };

    [DataField]
    public string AnnouncementSender = "Система безпеки";

    [DataField]
    public bool Activated = false;

    [DataField]
    public TimeSpan? EndTime;

    [DataField]
    public int NextAnnouncementIndex = 0;
}
