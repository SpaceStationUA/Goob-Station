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
        SubscribeNetworkEvent<RoundEndMessageEvent>(OnRoundEnd);
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

    private void TryOpen()
    {
        if (_offeredRound <= 0 || _offeredRound != _scoreboardRound ||
            _handledRound == _offeredRound || _window != null)
            return;

        _handledRound = _offeredRound;
        var round = _offeredRound;
        _window = new SecretSaltFeedbackWindow();
        _window.Submitted += (rating, reasons, comment) =>
            RaiseNetworkEvent(new SecretSaltFeedbackRequestEvent(round, rating, reasons, comment));
        _window.OnClose += () => _window = null;
        _window.OpenCentered();
    }
}
