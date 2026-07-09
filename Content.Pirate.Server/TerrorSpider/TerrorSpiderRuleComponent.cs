namespace Content.Pirate.Server.TerrorSpider;

/// <summary>
/// Stores Terror Spider round-end scoring and delayed announcement state.
/// </summary>
[RegisterComponent, Access(typeof(TerrorSpiderRuleSystem))]
public sealed partial class TerrorSpiderRuleComponent : Component
{
    [DataField]
    public TimeSpan TimerWait = TimeSpan.FromSeconds(20);

    [DataField]
    public float MinAliveCrewPercentage = 60;

    public TerrorSpidersWinStatus Status = TerrorSpidersWinStatus.Lose;

    [DataField]
    public TimeSpan AnnouncementDelay = TimeSpan.FromSeconds(10);

    [DataField]
    public TimeSpan RoundEndDelay = TimeSpan.FromSeconds(25);

    [DataField]
    public float DeadCrewWeight = 0.7f;

    [DataField]
    public float SpiderAmountWeight = 0.3f;

    [DataField]
    public float TargetWinScore = 70f;

    [DataField]
    public float TargetMinorScore = 40f;

    [DataField]
    public int MinSpidersCountForWin = 15;

    [DataField]
    public bool LoseProcessed;

    public TimeSpan AnnouncementTime = TimeSpan.Zero;
    public bool AlreadyAnnounced;

    public TimeSpan EndRoundTime = TimeSpan.Zero;
    public bool RoundAlreadyEnded;
}

public enum TerrorSpidersWinStatus
{
    Lose,
    MinorLose,
    MinorWin,
    Win
}
