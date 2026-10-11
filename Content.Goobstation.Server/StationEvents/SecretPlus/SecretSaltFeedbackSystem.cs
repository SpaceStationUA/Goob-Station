// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Content.Goobstation.Common.CCVar;
using Content.Goobstation.Common.SecretSalt;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Robust.Server.Player;
using Content.Shared.GameTicking;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Goobstation.Server.StationEvents.SecretPlus;

/// <summary>
/// Receives only authenticated post-round SecretSalt feedback and delivers a
/// second author-only Discord report after the review window closes.
/// </summary>
public sealed class SecretSaltFeedbackSystem : EntitySystem
{
    // Clients may decline, and the server may leave PostRound before the
    // deadline; in that case close and report immediately on transition.
    private static readonly TimeSpan ReviewWindow = TimeSpan.FromSeconds(60);
    private static readonly HttpClient AuthorReportClient = new();

    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly ILogManager _log = default!;
    [Dependency] private readonly IPlayerManager _players = default!;

    private readonly SecretSaltFeedbackTracker _tracker = new();

    // Capture the ready pool BEFORE roundstart antag assignment consumes its
    // members. Finalize only after the GameTicker has spawned everyone.
    // This includes roundstart pre-spawn antagonists, who call PlayerJoinGame
    // without firing PlayerSpawnCompleteEvent.
    private readonly HashSet<NetUserId> _roundStartReady = new();
    private readonly HashSet<NetUserId> _participants = new();
    private int _participantsRound;
    private ISawmill _sawmill = default!;
    private int _feedbackRound;
    private TimeSpan _feedbackDeadline;
    private bool _reportRequested;

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = _log.GetSawmill("secret_salt_feedback");
        SubscribeNetworkEvent<SecretSaltFeedbackRequestEvent>(OnFeedbackRequest);
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEnd);
        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStarting);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);
    }

    private void OnRoundStarting(RoundStartingEvent ev)
    {
        _roundStartReady.Clear();
        _participants.Clear();
        _participantsRound = 0;

        if (ev.Id <= 0 || _ticker.CurrentPreset?.ID != "SecretSalt")
            return;

        _participantsRound = ev.Id;
        foreach (var (userId, status) in _ticker.PlayerGameStatuses)
        {
            if (status == PlayerGameStatus.ReadyToPlay)
                _roundStartReady.Add(userId);
        }
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _roundStartReady.Clear();
        _participants.Clear();
        _participantsRound = 0;
    }

    private void OnRoundEnd(RoundEndTextAppendEvent ev)
    {
        if (_ticker.RunLevel != GameRunLevel.PostRound ||
            _ticker.CurrentPreset?.ID != "SecretSalt")
            return;

        var roundId = _ticker.RoundId;
        if (roundId <= 0 || _feedbackRound == roundId)
            return;

        _feedbackRound = roundId;
        _feedbackDeadline = _timing.CurTime + ReviewWindow;
        _reportRequested = false;
        _tracker.StartRound(roundId);

        // Send offers privately to actual participants, not all spectators.
        if (_participantsRound != roundId)
            return;

        foreach (var player in _players.Sessions)
        {
            if (_participants.Contains(player.UserId))
                RaiseNetworkEvent(new SecretSaltFeedbackOfferEvent(roundId), player.Channel);
        }
    }

    private void OnFeedbackRequest(SecretSaltFeedbackRequestEvent msg, EntitySessionEventArgs args)
    {
        if (_ticker.RunLevel != GameRunLevel.PostRound ||
            _ticker.CurrentPreset?.ID != "SecretSalt" ||
            _feedbackRound != msg.RoundId ||
            _reportRequested ||
            _timing.CurTime >= _feedbackDeadline ||
            _participantsRound != msg.RoundId ||
            !_participants.Contains(args.SenderSession.UserId))
            return;

        // Reject malformed client payloads before tracking them.
        if (msg.Reasons == null)
            return;

        var submission = new SecretSaltFeedbackSubmission
        {
            RoundId = msg.RoundId,
            Rating = msg.Rating,
            Reasons = msg.Reasons,
            Comment = msg.Comment
        };

        // Authenticated session owns both the account ID and player nickname.
        // Neither is ever taken from the client message.
        _tracker.TryRecord(_ticker.RoundId,
            args.SenderSession.UserId.ToString(),
            args.SenderSession.Name,
            submission);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_feedbackRound > 0 && !_reportRequested &&
            _ticker.RunLevel == GameRunLevel.PostRound &&
            _timing.CurTime >= _feedbackDeadline)
            CompleteFeedbackWindow();
    }

    private void OnRunLevelChanged(GameRunLevelChangedEvent ev)
    {
        if (ev.New == GameRunLevel.InRound &&
            ev.Old != GameRunLevel.InRound &&
            _participantsRound == _ticker.RoundId &&
            _ticker.CurrentPreset?.ID == "SecretSalt")
        {
            // By now special antagonists selected by RulePlayerSpawningEvent
            // and normally spawned crew have both been recorded as JoinedGame.
            // Late joins happen after this snapshot and cannot add themselves.
            foreach (var userId in _roundStartReady)
            {
                if (SecretSaltFeedbackEligibility.IsConfirmedRoundStartParticipant(
                    userId, _roundStartReady, _ticker.PlayerGameStatuses))
                    _participants.Add(userId);
            }

            _roundStartReady.Clear();
        }

        if (ev.Old == GameRunLevel.PostRound && ev.New != GameRunLevel.PostRound)
            CompleteFeedbackWindow();
    }

    private void CompleteFeedbackWindow()
    {
        if (_feedbackRound <= 0 || _reportRequested)
            return;

        _reportRequested = true;

        // The client must stop accepting input when the server closes the
        // sixty-second window, even if the round has not restarted yet.
        RaiseNetworkEvent(new SecretSaltFeedbackClosedEvent(_feedbackRound));

        var report = SecretSaltFeedbackReport.Format(
            _feedbackRound, _tracker.GetReviews(_feedbackRound));

        // This is the same mandatory confidential destination used by the
        // technical report; never fall back to a public station webhook.
        var webhook = _cfg.GetCVar(GoobCVars.SecretSaltAuthorDiscordWebhook);
        if (string.IsNullOrWhiteSpace(webhook))
        {
            _sawmill.Warning("Secret Salt feedback report NOT DELIVERED: mandatory private Discord webhook not configured.");
            return;
        }

        _ = DeliverFeedbackAsync(webhook, report);
    }

    private async Task DeliverFeedbackAsync(string webhook, string report)
    {
        try
        {
            await SecretSaltPlusDiscordSender.SendReportAsync(AuthorReportClient, webhook, report);
        }
        catch (Exception)
        {
            // Never include URL tokens, usernames or comment text in logs.
            _sawmill.Warning("Secret Salt private feedback report delivery failed.");
        }
    }
}

/// <summary>
/// Uses the existing GameTicker roster, not a separate gameplay population
/// counter. The caller snapshots ready users before the roundstart rule pool
/// is consumed and checks their JoinedGame status after spawning completes.
/// </summary>
public static class SecretSaltFeedbackEligibility
{
    public static bool IsConfirmedRoundStartParticipant(
        NetUserId userId,
        IReadOnlySet<NetUserId> readyAtStart,
        IReadOnlyDictionary<NetUserId, PlayerGameStatus> statuses)
    {
        return readyAtStart.Contains(userId) &&
            statuses.TryGetValue(userId, out var status) &&
            status == PlayerGameStatus.JoinedGame;
    }
}
