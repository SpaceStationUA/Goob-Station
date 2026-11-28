// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using Content.Server.GameTicking;
using Content.Server._Pirate.Arenas;
using Content.Server.RoundEnd;
using Content.Shared._Pirate.CaptureTheFlag;
using Content.Shared.ActionBlocker;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server._Pirate.CaptureTheFlag;

public sealed partial class CaptureTheFlagRuleSystem : ArenaRuleSystem<CaptureTheFlagRuleComponent>
{
    internal const string PresetId = "CaptureTheFlag";
    internal static readonly TimeSpan ClockInterval = TimeSpan.FromMilliseconds(200);

    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly RoundEndSystem _roundEnd = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedPopupSystem _popups = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnScoreboardPlayerAttached);
        SubscribeLocalEvent<CaptureTheFlagRuleComponent, EntityTerminatingEvent>(OnRuleTerminating);
        InitializePlayers();
        InitializeObjectives();
        InitializeRelay();
    }

    protected override void Started(EntityUid uid, CaptureTheFlagRuleComponent rule, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        if (GameTicker.CurrentPreset?.ID != PresetId || GameTicker.DefaultMap == MapId.Nullspace)
            return;

        var homes = AllEntityQuery<CaptureTheFlagBaseComponent, TransformComponent>();
        while (homes.MoveNext(out var homeId, out var home, out var xform))
        {
            if (xform.MapID != GameTicker.DefaultMap || xform.GridUid is not { } grid)
                continue;

            if (home.Rule.IsValid() || (rule.Grid.IsValid() && rule.Grid != grid) || !rule.Homes.TryAdd(home.Team, homeId))
                return;

            rule.Grid = grid;
        }

        if (!rule.Homes.ContainsKey(CaptureTheFlagTeam.Red) || !rule.Homes.ContainsKey(CaptureTheFlagTeam.Blue))
            return;

        var spawns = AllEntityQuery<CaptureTheFlagSpawnComponent, TransformComponent>();
        while (spawns.MoveNext(out var spawnId, out var spawn, out var xform))
        {
            if (xform.GridUid == rule.Grid && rule.Homes.TryGetValue(spawn.Team, out var homeId))
                Comp<CaptureTheFlagBaseComponent>(homeId).Spawns.Add(spawnId);
        }

        var supplyStations = new Dictionary<CaptureTheFlagTeam, Entity<CaptureTheFlagSupplyComponent>>();
        var supplies = AllEntityQuery<CaptureTheFlagSupplyComponent, TransformComponent>();
        while (supplies.MoveNext(out var supplyId, out var supply, out var xform))
        {
            if (xform.GridUid == rule.Grid && !supplyStations.TryAdd(supply.Team, (supplyId, supply)))
                return;
        }

        foreach (var (team, homeId) in rule.Homes)
        {
            if (Comp<CaptureTheFlagBaseComponent>(homeId).Spawns.Count == 0 || !supplyStations.ContainsKey(team))
                return;
        }

        if (!BindRelay(uid, rule))
            return;

        foreach (var (team, homeId) in rule.Homes)
        {
            var home = Comp<CaptureTheFlagBaseComponent>(homeId);
            home.Rule = uid;
            home.ReserveCharges = 0;
            var supply = supplyStations[team];
            supply.Comp.Rule = uid;
            home.Supply = supply;
            ReturnFlag((homeId, home));
        }
    }

    private void OnRunLevelChanged(GameRunLevelChangedEvent args)
    {
        if (args.New != GameRunLevel.InRound)
        {
            // A removed stand can end the round before the arena passes its usual validation.
            if (ActiveRule is { } active && active.Comp.Started)
            {
                StopClock();
                StopMatch(active.Comp);
            }

            return;
        }

        if (!TryGetActiveArena(out var uid, out var rule))
            return;

        rule.Started = true;
        rule.EndAt = Timing.CurTime + rule.MatchDuration;
        var (red, blue) = CountPlaying(rule);
        rule.HadOpponents = red > 0 && blue > 0;
        StartClock(ClockInterval, () => TickMatch(uid, rule));
        SendScoreboard(rule);
        foreach (var session in _players.Sessions)
            SendTeam(rule, session);
        ChatManager.DispatchServerAnnouncement(Loc.GetString("ctf-round-started",
            ("target", rule.ScoreToWin), ("minutes", (int) rule.MatchDuration.TotalMinutes)));
    }

    private void TickMatch(EntityUid uid, CaptureTheFlagRuleComponent rule)
    {
        if (GameTicker.RunLevel != GameRunLevel.InRound || rule.Finished || rule.EndAt is not { } ends)
            return;

        var (red, blue) = RefreshBalance(rule);
        if (CheckForfeit(rule, red, blue))
            return;

        if (Timing.CurTime >= ends)
        {
            if (rule.ForfeitAt == null)
                Finish(rule);
            return;
        }

        if (!rule.AnnouncedLastMinute && ends - Timing.CurTime <= TimeSpan.FromMinutes(1))
        {
            rule.AnnouncedLastMinute = true;
            ChatManager.DispatchServerAnnouncement(Loc.GetString("ctf-remaining-minute", ("score", GetScore(rule))));
        }

        foreach (var homeId in rule.Homes.Values)
        {
            if (TryComp<CaptureTheFlagBaseComponent>(homeId, out var home) && !TerminatingOrDeleted(homeId))
                UpdateFlag(uid, rule, (homeId, home));
            else
            {
                Finish(rule);
                return;
            }
        }

        UpdateRelay(uid, rule, red, blue);
        UpdatePlayers(uid, rule);
    }

    private bool CheckForfeit(CaptureTheFlagRuleComponent rule, int red, int blue)
    {
        if (red > 0 && blue > 0)
            rule.HadOpponents = true;

        if (!rule.HadOpponents || red + blue >= 2)
        {
            rule.ForfeitAt = null;
            return false;
        }

        if (rule.ForfeitAt is not { } deadline)
        {
            rule.ForfeitAt = Timing.CurTime + rule.ReconnectGrace;
            ChatManager.DispatchServerAnnouncement(Loc.GetString("ctf-reconnect-grace",
                ("seconds", (int) rule.ReconnectGrace.TotalSeconds)));
            return false;
        }

        if (Timing.CurTime < deadline)
            return false;

        if (red > 0)
            rule.ForfeitWinner = CaptureTheFlagTeam.Red;
        else if (blue > 0)
            rule.ForfeitWinner = CaptureTheFlagTeam.Blue;
        else
            rule.Abandoned = true;

        Finish(rule);
        return true;
    }

    protected override void Ended(EntityUid uid, CaptureTheFlagRuleComponent rule, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        StopMatch(rule);
        ReleaseRelay(uid, rule);
        foreach (var homeId in rule.Homes.Values)
        {
            if (!TryComp<CaptureTheFlagBaseComponent>(homeId, out var home) || home.Rule != uid)
                continue;

            QueueDel(home.Flag);
            home.Rule = EntityUid.Invalid;
            home.ReserveCharges = 0;
            home.Spawns.Clear();
            if (TryComp<CaptureTheFlagSupplyComponent>(home.Supply, out var supply) && supply.Rule == uid)
                supply.Rule = EntityUid.Invalid;
        }

        rule.Grid = EntityUid.Invalid;
        rule.Homes.Clear();
    }

    protected override void AppendRoundEndText(EntityUid uid, CaptureTheFlagRuleComponent rule, GameRuleComponent gameRule,
        ref RoundEndTextAppendEvent args)
    {
        if (!rule.Started)
            return;

        var red = GetTeamScore(rule, CaptureTheFlagTeam.Red);
        var blue = GetTeamScore(rule, CaptureTheFlagTeam.Blue);
        args.AddLine(GetScore(rule));
        if (rule.Abandoned)
            args.AddLine(Loc.GetString("ctf-round-abandoned"));
        else if (rule.ForfeitWinner is { } winner)
            args.AddLine(Loc.GetString("ctf-round-forfeit", ("team", GetTeamName(winner))));
        else if (red == blue)
            args.AddLine(Loc.GetString("ctf-round-draw"));
        else
            args.AddLine(Loc.GetString("ctf-round-winner",
                ("team", GetTeamName(red > blue ? CaptureTheFlagTeam.Red : CaptureTheFlagTeam.Blue))));
    }

    private void Finish(CaptureTheFlagRuleComponent rule)
    {
        rule.Finished = true;
        _roundEnd.EndRound();
    }

    private void OnRuleTerminating(EntityUid uid, CaptureTheFlagRuleComponent rule, ref EntityTerminatingEvent args)
    {
        StopMatch(rule);
    }

    private void StopMatch(CaptureTheFlagRuleComponent rule)
    {
        if (rule.EndAt != null)
            RaiseNetworkEvent(new CaptureTheFlagScoreClosedEvent());

        rule.Finished = true;
        rule.EndAt = null;
        rule.ForfeitAt = null;
        rule.LargerTeam = null;
        rule.LargerSince = default;
        rule.EmptyTeamSince = null;
        rule.Players.Clear();
    }

    internal bool TryGetActiveArena(out EntityUid uid, [NotNullWhen(true)] out CaptureTheFlagRuleComponent? rule)
    {
        if (GameTicker.CurrentPreset?.ID == PresetId && ActiveRule is { } arena &&
            arena.Comp.Homes.Count == 2 && GameTicker.IsGameRuleActive(arena) &&
            arena.Comp.Homes.TryGetValue(CaptureTheFlagTeam.Red, out var red) &&
            TryComp<CaptureTheFlagBaseComponent>(red, out var home) && home.Rule == arena.Owner)
        {
            uid = arena;
            rule = arena.Comp;
            return true;
        }

        uid = EntityUid.Invalid;
        rule = null;
        return false;
    }

    private bool TryGetMatch(EntityUid uid, [NotNullWhen(true)] out CaptureTheFlagRuleComponent? rule)
    {
        return TryComp(uid, out rule) && !rule.Finished && rule.EndAt is { } ends && Timing.CurTime < ends &&
            GameTicker.RunLevel == GameRunLevel.InRound && GameTicker.IsGameRuleActive(uid);
    }

    private bool CanUse(EntityUid user, EntityUid target)
    {
        return _actionBlocker.CanInteract(user, target) && _interaction.InRangeUnobstructed(user, target);
    }

    private void OnScoreboardPlayerAttached(PlayerAttachedEvent args)
    {
        if (TryGetActiveArena(out var uid, out _) && TryGetMatch(uid, out var rule))
            SendScoreboard(rule, args.Player);
    }

    private void SendScoreboard(CaptureTheFlagRuleComponent rule, ICommonSession? recipient = null)
    {
        TryGetHome(rule, CaptureTheFlagTeam.Red, out var red);
        TryGetHome(rule, CaptureTheFlagTeam.Blue, out var blue);
        var score = new CaptureTheFlagScoreEvent(red?.Score ?? 0, blue?.Score ?? 0, rule.Tier, rule.RelayState,
            red?.ReserveCharges ?? 0, blue?.ReserveCharges ?? 0);
        if (recipient == null)
            RaiseNetworkEvent(score);
        else
        {
            RaiseNetworkEvent(score, recipient);
            SendTeam(rule, recipient);
        }
    }

    private void SendTeam(CaptureTheFlagRuleComponent rule, ICommonSession viewer)
    {
        bool? red = rule.Players.TryGetValue(viewer.UserId, out var player) ? player.Team == CaptureTheFlagTeam.Red : null;
        RaiseNetworkEvent(new CaptureTheFlagAssignmentEvent(red), viewer);
    }

    private int GetTeamScore(CaptureTheFlagRuleComponent rule, CaptureTheFlagTeam team)
    {
        return rule.Homes.TryGetValue(team, out var home) && TryComp<CaptureTheFlagBaseComponent>(home, out var objective)
            ? objective.Score
            : 0;
    }

    private string GetScore(CaptureTheFlagRuleComponent rule)
    {
        return Loc.GetString("ctf-score",
            ("red", GetTeamScore(rule, CaptureTheFlagTeam.Red)), ("blue", GetTeamScore(rule, CaptureTheFlagTeam.Blue)));
    }

    private string GetTeamName(CaptureTheFlagTeam team)
    {
        return Loc.GetString(team == CaptureTheFlagTeam.Red ? "ctf-team-red" : "ctf-team-blue");
    }

    private static CaptureTheFlagTeam Opponent(CaptureTheFlagTeam team)
    {
        return team == CaptureTheFlagTeam.Red ? CaptureTheFlagTeam.Blue : CaptureTheFlagTeam.Red;
    }
}
