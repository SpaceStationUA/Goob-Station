// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Common.SecretSalt;
using Content.Server.GameTicking;
using Robust.Shared.Player;

namespace Content.Goobstation.Server.StationEvents.SecretPlus;

/// <summary>
/// Accepts reviews only for the completed SecretSalt round. The network event
/// carries no account identity; the authenticated sender is authoritative.
/// </summary>
public sealed class SecretSaltFeedbackSystem : EntitySystem
{
    [Dependency] private readonly GameTicker _ticker = default!;

    private readonly SecretSaltFeedbackTracker _tracker = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<SecretSaltFeedbackRequestEvent>(OnFeedbackRequest);
    }

    private void OnFeedbackRequest(SecretSaltFeedbackRequestEvent msg, EntitySessionEventArgs args)
    {
        if (_ticker.RunLevel != GameRunLevel.PostRound ||
            _ticker.CurrentPreset?.ID != "SecretSalt" ||
            msg.RoundId != _ticker.RoundId)
            return;

        // Reject malformed client payloads before passing them to the tracker.
        if (msg.Reasons == null)
            return;

        var submission = new SecretSaltFeedbackSubmission
        {
            RoundId = msg.RoundId,
            Rating = msg.Rating,
            Reasons = msg.Reasons,
            Comment = msg.Comment
        };

        // Never trust a username or user ID supplied by a client payload.
        _tracker.TryRecord(_ticker.RoundId, args.SenderSession.UserId.ToString(), submission);
    }
}
