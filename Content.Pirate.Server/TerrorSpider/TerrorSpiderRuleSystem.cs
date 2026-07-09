using Content.Pirate.Shared.TerrorSpider;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Roles;
using Content.Server.RoundEnd;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Systems;
using Content.Shared.GameTicking.Components;
using Content.Shared.Humanoid;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Pirate.Server.TerrorSpider;

public sealed partial class TerrorSpiderRuleSystem : GameRuleSystem<TerrorSpiderRuleComponent>
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly EmergencyShuttleSystem _emergencyShuttle = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly RoundEndSystem _roundEnd = default!;
    [Dependency] private readonly StationSystem _station = default!;

    private readonly TimeSpan _rulesCheckCacheTime = TimeSpan.FromSeconds(30);
    private TimeSpan _lastRuleCheck = TimeSpan.Zero;
    private bool _cachedState;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HumanoidAppearanceComponent, MobStateChangedEvent>(OnCrewMobStateChanged);
        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);
        SubscribeLocalEvent<TerrorPrincessComponent, MobStateChangedEvent>(OnPrincessStateChanged);
        SubscribeLocalEvent<TerrorPrincessComponent, GetBriefingEvent>(OnGetBriefing);
    }

    protected override void Started(EntityUid uid, TerrorSpiderRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        AtLeastOneRuleExists(false);
    }

    private void OnGetBriefing(Entity<TerrorPrincessComponent> ent, ref GetBriefingEvent args)
    {
        args.Append(Loc.GetString(ent.Comp.Briefing));
    }

    private void OnCrewMobStateChanged(Entity<HumanoidAppearanceComponent> ent, ref MobStateChangedEvent args)
    {
        if (AtLeastOneRuleExists() && args.NewMobState is MobState.Dead or MobState.Invalid)
            ProcessLose();
    }

    private void OnPrincessStateChanged(Entity<TerrorPrincessComponent> ent, ref MobStateChangedEvent args)
    {
        if (AtLeastOneRuleExists(false) && args.NewMobState is MobState.Dead or MobState.Invalid)
            ProcessLose();
    }

    protected override void AppendRoundEndText(EntityUid uid, TerrorSpiderRuleComponent component, GameRuleComponent gameRule, ref RoundEndTextAppendEvent args)
    {
        var message = component.Status switch
        {
            TerrorSpidersWinStatus.MinorLose => Loc.GetString("terrorspiders-minorlose"),
            TerrorSpidersWinStatus.Lose => Loc.GetString("terrorspiders-lose"),
            TerrorSpidersWinStatus.MinorWin => Loc.GetString("terrorspiders-minorwin"),
            TerrorSpidersWinStatus.Win => Loc.GetString("terrorspiders-win"),
            _ => string.Empty
        };

        args.AddLine(message);

        var query = EntityQueryEnumerator<MetaDataComponent, TerrorSpiderComponent>();
        var startAdded = false;
        while (query.MoveNext(out var spider, out var meta, out _))
        {
            if (!_player.TryGetSessionByEntity(spider, out var session))
                continue;

            if (!startAdded)
            {
                args.AddLine(Loc.GetString("terrorspiders-list-start"));
                startAdded = true;
            }

            args.AddLine(Loc.GetString("terrorspiders-list-name-user",
                ("name", meta.EntityName),
                ("user", session.Name)));
        }

        args.AddLine(string.Empty);
    }

    private void OnRunLevelChanged(GameRunLevelChangedEvent ev)
    {
        if (ev.New is not GameRunLevel.PostRound)
            return;

        var query = QueryActiveRules();
        while (query.MoveNext(out var uid, out _, out var rule, out _))
        {
            OnRoundEnd((uid, rule));
        }
    }

    private void OnRoundEnd(Entity<TerrorSpiderRuleComponent> ent)
    {
        if (ent.Comp.LoseProcessed)
            return;

        var score = SpidersScore(ent.Comp, out _, false);
        ent.Comp.Status = score > ent.Comp.TargetMinorScore
            ? TerrorSpidersWinStatus.MinorWin
            : TerrorSpidersWinStatus.MinorLose;
    }

    private void ProcessLose()
    {
        var lose = AreAllPrincessesDead();
        var count = 0;
        var query = QueryActiveRules();
        while (query.MoveNext(out _, out _, out var ruleComp, out _))
        {
            if (ruleComp.LoseProcessed)
                continue;

            var score = SpidersScore(ruleComp, out _);
            var win = score > ruleComp.TargetWinScore && !lose;

            if (!win && !lose)
                continue;

            count++;
            ruleComp.Status = win ? TerrorSpidersWinStatus.Win : TerrorSpidersWinStatus.Lose;
            ruleComp.LoseProcessed = true;
            ruleComp.AnnouncementTime = _timing.CurTime + ruleComp.AnnouncementDelay;
            ruleComp.EndRoundTime = _timing.CurTime + ruleComp.RoundEndDelay;
        }

        if (count == 0)
            return;

        if (_roundEnd.IsRoundEndRequested())
            _roundEnd.CancelRoundEndCountdown(null, false);
    }

    protected override void ActiveTick(EntityUid uid, TerrorSpiderRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        if (!component.LoseProcessed)
            return;

        if (component.AlreadyAnnounced && component.RoundAlreadyEnded)
            return;

        if (GameTicker.RunLevel != GameRunLevel.InRound)
            return;

        if (!component.AlreadyAnnounced && component.AnnouncementTime < _timing.CurTime)
        {
            try
            {
                if (component.Status == TerrorSpidersWinStatus.Lose)
                {
                    _chat.DispatchGlobalAnnouncement(
                        Loc.GetString("central-command-terror-spiders-announcement-lose"),
                        Loc.GetString("central-command-sender"),
                        true,
                        new SoundPathSpecifier("/Audio/_Pirate/Announcements/announce_broken.ogg"),
                        Color.Red);
                }
                else if (component.Status == TerrorSpidersWinStatus.Win)
                {
                    _chat.DispatchGlobalAnnouncement(
                        Loc.GetString("central-command-terror-spiders-announcement-win"),
                        Loc.GetString("central-command-sender"),
                        true,
                        new SoundPathSpecifier("/Audio/_Pirate/Announcements/announce_broken.ogg"),
                        Color.Green);
                }

                component.AlreadyAnnounced = true;
            }
            catch (Exception ex)
            {
                Log.Error($"Error during terror spiders announcement: {ex}");
                component.AlreadyAnnounced = true;
            }
        }

        if (component.RoundAlreadyEnded || component.EndRoundTime >= _timing.CurTime)
            return;

        _roundEnd.EndRound();
        component.RoundAlreadyEnded = true;
        GameTicker.EndGameRule(uid, gameRule);
    }

    private float SpidersScore(TerrorSpiderRuleComponent component, out float percentage, bool shouldUseMinGate = true)
    {
        percentage = 0;
        var crewList = GetCrew();

        if (crewList.Count == 0)
            return 0;

        var spidersList = new List<EntityUid>();
        var spiders = EntityQueryEnumerator<TerrorSpiderComponent>();
        while (spiders.MoveNext(out var uid, out _))
            spidersList.Add(uid);

        if (shouldUseMinGate && spidersList.Count < component.MinSpidersCountForWin)
            return 0;

        if (spidersList.Count == 0)
            return 0;

        var crewDeadAmount = CountGoneEntities(crewList);
        var spidersDeadAmount = CountGoneEntities(spidersList);

        var deadCrewPercent = (float) crewDeadAmount / crewList.Count * 100f;
        percentage = deadCrewPercent;

        var aliveSpiderPercent = (float) (spidersList.Count - spidersDeadAmount) / spidersList.Count * 100f;
        return deadCrewPercent * component.DeadCrewWeight + aliveSpiderPercent * component.SpiderAmountWeight;
    }

    private List<EntityUid> GetCrew()
    {
        var crew = new List<EntityUid>();
        var query = EntityQueryEnumerator<HumanoidAppearanceComponent, ActorComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out _, out _, out _))
        {
            if (HasComp<TerrorSpiderComponent>(uid))
                continue;

            crew.Add(uid);
        }

        return crew;
    }

    private int CountGoneEntities(IEnumerable<EntityUid> entities, bool checkOffStation = true)
    {
        var gone = 0;
        foreach (var ent in entities)
        {
            if (TryComp<MobStateComponent>(ent, out var mobState) && mobState.CurrentState is MobState.Dead or MobState.Invalid)
                gone++;
            else if (checkOffStation && _station.GetOwningStation(ent) == null && !_emergencyShuttle.EmergencyShuttleArrived)
                gone++;
        }

        return gone;
    }

    private bool AreAllPrincessesDead()
    {
        var query = EntityQueryEnumerator<TerrorPrincessComponent, MobStateComponent>();
        var count = 0;

        while (query.MoveNext(out _, out _, out var state))
        {
            count++;
            if (state.CurrentState == MobState.Alive)
                return false;
        }

        return count != 0;
    }

    private bool AtLeastOneRuleExists(bool canUseCache = true)
    {
        if (canUseCache && _timing.CurTime < _lastRuleCheck + _rulesCheckCacheTime)
            return _cachedState;

        _lastRuleCheck = _timing.CurTime;

        var count = 0;
        var query = EntityQueryEnumerator<TerrorSpiderRuleComponent>();
        while (query.MoveNext(out _, out var ruleComp))
        {
            if (ruleComp.LoseProcessed)
                continue;

            count++;
        }

        _cachedState = count > 0;
        return _cachedState;
    }
}
