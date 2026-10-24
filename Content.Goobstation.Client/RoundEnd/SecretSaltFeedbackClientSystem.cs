// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Common.SecretSalt;
using Content.Shared.GameTicking;

namespace Content.Goobstation.Client.RoundEnd;

/// <summary>
/// Shows the voluntary offer only after the actual round-end scoreboard has
/// been received, never during active gameplay or on non-SecretSalt rounds.
/// </summary>
public sealed class SecretSaltFeedbackClientSystem : EntitySystem
{
    private SecretSaltFeedbackWindow? _window;
    private int _offeredRound;
    private int _scoreboardRound;
    private int _handledRound;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<SecretSaltFeedbackOfferEvent>(OnOffer);
        SubscribeNetworkEvent<SecretSaltFeedbackClosedEvent>(OnFeedbackClosed);
        SubscribeNetworkEvent<RoundEndMessageEvent>(OnRoundEnd);
        SubscribeNetworkEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnOffer(SecretSaltFeedbackOfferEvent ev)
    {
        _offeredRound = ev.RoundId;
        TryOpen();
    }

    private void OnRoundEnd(RoundEndMessageEvent ev)
    {
        _scoreboardRound = ev.RoundId;
        TryOpen();
    }

    private void OnFeedbackClosed(SecretSaltFeedbackClosedEvent ev)
    {
        if (ev.RoundId != _offeredRound && ev.RoundId != _scoreboardRound)
            return;

        _handledRound = ev.RoundId;
        _offeredRound = 0;
        _scoreboardRound = 0;
        CloseWindow();
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        // Cleanup also runs when the round ends before the normal feedback
        // deadline, or when a non-SecretSalt round begins.
        _offeredRound = 0;
        _scoreboardRound = 0;
        CloseWindow();
    }

    private void CloseWindow()
    {
        var window = _window;
        _window = null;
        if (window is { Disposed: false })
            window.Close();
    }

    /// <summary>
    /// Send a review through the same client-to-server path as the UI button.
    /// The server remains authoritative about the active mode, phase, sender,
    /// rate limit, and whether an account has already submitted this round.
    /// </summary>
    public void SubmitFeedback(int roundId, int rating, SecretSaltFeedbackReason[] reasons, string? comment)
    {
        RaiseNetworkEvent(new SecretSaltFeedbackRequestEvent(
            roundId, rating, SecretSaltFeedbackReasonMask.Encode(reasons), comment));
    }

    private void TryOpen()
    {
        if (_offeredRound <= 0 || _offeredRound != _scoreboardRound ||
            _handledRound == _offeredRound || _window != null)
            return;

        _handledRound = _offeredRound;
        var round = _offeredRound;
        _window = new SecretSaltFeedbackWindow();
        _window.Submitted += (rating, reasons, comment) =>
            SubmitFeedback(round, rating, reasons, comment);
        _window.OnClose += () => _window = null;
        _window.OpenCentered();
    }
}
