// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using Content.Goobstation.Shared.MisandryBox.Mind;
using Content.Server.Clothing.Systems;
using Content.Server.GameTicking;
using Content.Server.Hands.Systems;
using Content.Server.Mind;
using Content.Server.Station.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Preferences;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server._Pirate.CaptureTheFlag;

public sealed partial class CaptureTheFlagRuleSystem
{
    [Dependency] private readonly HandsSystem _hands = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly OutfitSystem _outfits = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly StationSpawningSystem _spawning = default!;

    private void InitializePlayers()
    {
        SubscribeLocalEvent<RulePlayerSpawningEvent>(OnRoundStartSpawning);
        SubscribeLocalEvent<PlayerBeforeSpawnEvent>(OnBeforeSpawn);
        SubscribeLocalEvent<CaptureTheFlagBodyComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<CaptureTheFlagBodyComponent, EntityTerminatingEvent>(OnBodyTerminating);
        SubscribeLocalEvent<CaptureTheFlagBodyComponent, PlayerDetachedEvent>(OnPlayerDetached);
    }

    private void OnRoundStartSpawning(RulePlayerSpawningEvent args)
    {
        if (!TryGetActiveArena(out var uid, out var rule))
            return;

        var players = args.PlayerPool.ToArray();
        RobustRandom.Shuffle(players);

        foreach (var session in players)
        {
            if (!args.Profiles.TryGetValue(session.UserId, out var profile))
                continue;

            JoinMatch(uid, rule, session, profile);
            GameTicker.PlayerJoinGame(session);
            args.PlayerPool.Remove(session);
        }
    }

    private void OnBeforeSpawn(PlayerBeforeSpawnEvent args)
    {
        if (args.Handled || ActiveRule is not { } active || GameTicker.CurrentPreset?.ID != PresetId)
            return;

        if (active.Comp.Finished || GameTicker.RunLevel == GameRunLevel.PostRound)
        {
            args.Handled = true;
            GameTicker.SpawnObserver(args.Player);
            return;
        }

        if (TryGetActiveArena(out var uid, out var rule))
        {
            args.Handled = true;
            JoinMatch(uid, rule, args.Player, args.Profile);
        }
    }

    private void JoinMatch(EntityUid uid, CaptureTheFlagRuleComponent rule, ICommonSession session, HumanoidCharacterProfile profile)
    {
        if (!rule.Players.TryGetValue(session.UserId, out var player))
        {
            var (red, blue) = CountPlaying(rule);
            var team = red < blue ? CaptureTheFlagTeam.Red : CaptureTheFlagTeam.Blue;
            if (red == blue && RobustRandom.Prob(0.5f))
                team = CaptureTheFlagTeam.Red;

            player = new CaptureTheFlagPlayer(team, profile);
            rule.Players.Add(session.UserId, player);
        }
        else if (player.RespawnAt is { } readyAt)
        {
            if (Timing.CurTime >= readyAt)
                RespawnPlayer(uid, rule, session, player);
            return;
        }

        if (player.Body.IsValid() && !TerminatingOrDeleted(player.Body) && _mobState.IsAlive(player.Body))
        {
            ReattachPlayer(session, player);
            return;
        }

        SpawnPlayer(uid, rule, session, player);
        ChatManager.DispatchServerMessage(session, Loc.GetString("ctf-joined",
            ("team", GetTeamName(player.Team)), ("target", rule.ScoreToWin), ("tier", rule.Tier)));
        ChatManager.DispatchServerMessage(session, Loc.GetString("ctf-player-instructions"));
    }

    private void ReattachPlayer(ICommonSession session, CaptureTheFlagPlayer player)
    {
        if (session.AttachedEntity == player.Body || HasComp<ActorComponent>(player.Body))
            return;

        // Keep the current life and claim history when a participant rejoins with their body still alive.
        var mind = _mind.TryGetMind(session.UserId, out var existing)
            ? existing.Value
            : _mind.CreateMind(session.UserId, player.Profile.Name);
        if (mind.Comp.OwnedEntity == player.Body)
        {
            _mind.UnVisit(mind, mind: mind.Comp);
            _players.SetAttachedEntity(session, player.Body);
        }
        else
        {
            _mind.TransferTo(mind, player.Body, ghostCheckOverride: true, mind: mind.Comp);
        }
    }

    private void SpawnPlayer(EntityUid uid, CaptureTheFlagRuleComponent rule, ICommonSession session, CaptureTheFlagPlayer player)
    {
        var previous = player.Body;
        if (previous.IsValid() && !TerminatingOrDeleted(previous))
            DropFlag(rule, previous);

        var homeId = rule.Homes[player.Team];
        var home = Comp<CaptureTheFlagBaseComponent>(homeId);
        var spawn = RobustRandom.Pick(home.Spawns);
        var position = !TerminatingOrDeleted(spawn) ? Transform(spawn).Coordinates : Transform(homeId).Coordinates;
        var mob = _spawning.SpawnPlayerMob(position, null, player.Profile, null, ckey: session.Name);
        _outfits.SetOutfit(mob, player.Team == CaptureTheFlagTeam.Red ? "CaptureTheFlagRedGear" : "CaptureTheFlagBlueGear");
        foreach (var item in _hands.EnumerateHeld(mob))
            AllowArenaGunUse(item);
        EquipArenaBreathingGear(mob);

        var body = AddComp<CaptureTheFlagBodyComponent>(mob);
        body.Rule = uid;
        body.Player = session.UserId;
        player.Body = mob;
        player.RespawnAt = null;
        player.ClaimedTier = 1;

        // Preserve the mind through automatic respawns, including a recoverable ghost visit.
        var mind = _mind.TryGetMind(session.UserId, out var existing)
            ? existing.Value
            : _mind.CreateMind(session.UserId, player.Profile.Name);
        _mind.TransferTo(mind, mob, ghostCheckOverride: true, mind: mind.Comp);

        if (previous.IsValid() && !TerminatingOrDeleted(previous))
            QueueDel(previous);
    }

    private void OnMobStateChanged(EntityUid uid, CaptureTheFlagBodyComponent body, MobStateChangedEvent args)
    {
        if (!TryGetMatch(body.Rule, out var rule) || !rule.Players.TryGetValue(body.Player, out var player) || player.Body != uid)
            return;

        if (args.NewMobState == MobState.Alive)
        {
            player.RespawnAt = null;
            return;
        }

        DropFlag(rule, uid);
        QueueRespawn(rule, body.Player, player);
    }

    private void OnBodyTerminating(EntityUid uid, CaptureTheFlagBodyComponent body, ref EntityTerminatingEvent args)
    {
        if (!TryGetMatch(body.Rule, out var rule) || !rule.Players.TryGetValue(body.Player, out var player) || player.Body != uid)
            return;

        QueueRespawn(rule, body.Player, player);
    }

    private void OnPlayerDetached(EntityUid uid, CaptureTheFlagBodyComponent body, PlayerDetachedEvent args)
    {
        if (args.Player.UserId == body.Player && TryGetMatch(body.Rule, out var rule))
            DropFlag(rule, uid);
    }

    private void QueueRespawn(CaptureTheFlagRuleComponent rule, NetUserId user, CaptureTheFlagPlayer player)
    {
        if (player.RespawnAt != null)
            return;

        player.RespawnAt = Timing.CurTime + rule.RespawnDelay;
        if (_players.TryGetSessionById(user, out var session) && session.Status == SessionStatus.InGame)
        {
            ChatManager.DispatchServerMessage(session, Loc.GetString("ctf-respawn-pending",
                ("seconds", (int) rule.RespawnDelay.TotalSeconds)));
        }
    }

    private bool TryGetParticipant(EntityUid uid, EntityUid ruleId, CaptureTheFlagRuleComponent rule,
        [NotNullWhen(true)] out CaptureTheFlagPlayer? player)
    {
        player = null;
        return TryComp<CaptureTheFlagBodyComponent>(uid, out var body) && body.Rule == ruleId &&
            rule.Players.TryGetValue(body.Player, out player) && player.Body == uid &&
            !TerminatingOrDeleted(uid) && Transform(uid).GridUid == rule.Grid && _mobState.IsAlive(uid) &&
            _players.TryGetSessionById(body.Player, out var session) &&
            session.Status == SessionStatus.InGame && session.AttachedEntity == uid;
    }

    private bool TryGetPlayingSession(NetUserId user, CaptureTheFlagPlayer player, [NotNullWhen(true)] out ICommonSession? session)
    {
        return _players.TryGetSessionById(user, out session) && session.Status == SessionStatus.InGame &&
            (session.AttachedEntity == player.Body || player.RespawnAt != null);
    }

    private (int Red, int Blue) CountPlaying(CaptureTheFlagRuleComponent rule)
    {
        var red = 0;
        var blue = 0;
        foreach (var (user, player) in rule.Players)
        {
            if (!TryGetPlayingSession(user, player, out _))
                continue;

            if (player.Team == CaptureTheFlagTeam.Red)
                red++;
            else
                blue++;
        }

        return (red, blue);
    }

    private (int Red, int Blue) RefreshBalance(CaptureTheFlagRuleComponent rule)
    {
        var (red, blue) = CountPlaying(rule);
        CaptureTheFlagTeam? larger = null;
        if (Math.Abs(red - blue) >= 2)
            larger = red > blue ? CaptureTheFlagTeam.Red : CaptureTheFlagTeam.Blue;

        if (larger != rule.LargerTeam)
        {
            rule.LargerTeam = larger;
            rule.LargerSince = Timing.CurTime;
            rule.EmptyTeamSince = null;
        }

        if (larger != null && (red == 0 || blue == 0))
            rule.EmptyTeamSince ??= Timing.CurTime;
        else
            rule.EmptyTeamSince = null;

        return (red, blue);
    }

    private void UpdatePlayers(EntityUid uid, CaptureTheFlagRuleComponent rule)
    {
        foreach (var (user, player) in rule.Players)
        {
            if (player.RespawnAt is not { } readyAt || Timing.CurTime < readyAt ||
                !TryGetPlayingSession(user, player, out var session) ||
                HasComp<TemporaryMindComponent>(session.AttachedEntity))
                continue;

            RespawnPlayer(uid, rule, session, player);
        }

        // An empty team cannot fight anyone to create a respawn. Leave flag carriers in play.
        if (rule.EmptyTeamSince is not { } emptySince || Timing.CurTime - emptySince < rule.EmptyTeamRebalanceDelay)
            return;

        foreach (var (user, player) in rule.Players)
        {
            if (player.Team != rule.LargerTeam || !TryGetPlayingSession(user, player, out var session) ||
                !TryGetParticipant(player.Body, uid, rule, out _) || IsCarryingFlag(rule, player.Body))
                continue;

            SwitchTeam(player, session);
            SpawnPlayer(uid, rule, session, player);
            return;
        }
    }

    private void RespawnPlayer(EntityUid uid, CaptureTheFlagRuleComponent rule, ICommonSession session, CaptureTheFlagPlayer player)
    {
        RefreshBalance(rule);
        if (rule.LargerTeam == player.Team && Timing.CurTime - rule.LargerSince >= rule.RebalanceDelay)
            SwitchTeam(player, session);

        SpawnPlayer(uid, rule, session, player);
        RefreshBalance(rule);
    }

    private void SwitchTeam(CaptureTheFlagPlayer player, ICommonSession session)
    {
        player.Team = Opponent(player.Team);
        ChatManager.DispatchServerMessage(session, Loc.GetString("ctf-rebalanced", ("team", GetTeamName(player.Team))));
    }
}
