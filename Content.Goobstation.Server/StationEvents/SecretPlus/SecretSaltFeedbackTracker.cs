// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Generic;
using Content.Goobstation.Common.SecretSalt;

namespace Content.Goobstation.Server.StationEvents.SecretPlus;

public enum SecretSaltFeedbackResult
{
    Accepted,
    InvalidSubmission,
    WrongRound,
    MissingServerIdentity,
    AlreadySubmitted
}

/// <summary>
/// Server-owned feedback guard: only one review per authenticated account in
/// each round. This class does not accept player-supplied account identifiers.
/// Its caller must check that the current preset is SecretSalt, the round is
/// finished, and pass the account identity from the authenticated session.
/// </summary>
public sealed class SecretSaltFeedbackTracker
{
    private readonly Dictionary<string, SecretSaltFeedbackSubmission> _reviews = new();
    private int _roundId;

    public int Count => _reviews.Count;

    public SecretSaltFeedbackResult TryRecord(
        int currentRoundId,
        string authenticatedAccountId,
        SecretSaltFeedbackSubmission submission)
    {
        if (submission.RoundId != currentRoundId || currentRoundId <= 0)
            return SecretSaltFeedbackResult.WrongRound;

        if (string.IsNullOrWhiteSpace(authenticatedAccountId))
            return SecretSaltFeedbackResult.MissingServerIdentity;

        if (!SecretSaltFeedbackValidator.IsValid(submission))
            return SecretSaltFeedbackResult.InvalidSubmission;

        if (_roundId != currentRoundId)
        {
            _reviews.Clear();
            _roundId = currentRoundId;
        }

        return _reviews.TryAdd(authenticatedAccountId, submission)
            ? SecretSaltFeedbackResult.Accepted
            : SecretSaltFeedbackResult.AlreadySubmitted;
    }
}
