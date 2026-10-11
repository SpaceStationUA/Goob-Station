// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Linq;
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
public sealed record SecretSaltRecordedFeedback(string Nickname, SecretSaltFeedbackSubmission Submission);

public sealed class SecretSaltFeedbackTracker
{
    private readonly Dictionary<string, SecretSaltRecordedFeedback> _reviews = new();
    private int _roundId;

    public int Count => _reviews.Count;

    public void StartRound(int roundId)
    {
        if (_roundId == roundId)
            return;

        _reviews.Clear();
        _roundId = roundId;
    }

    // No account IDs are exposed to the Discord report renderer.
    public IReadOnlyList<SecretSaltRecordedFeedback> GetReviews(int roundId) =>
        _roundId == roundId ? _reviews.Values.ToArray() : Array.Empty<SecretSaltRecordedFeedback>();

    public SecretSaltFeedbackResult TryRecord(
        int currentRoundId,
        string authenticatedAccountId,
        string authenticatedNickname,
        SecretSaltFeedbackSubmission submission)
    {
        if (submission.RoundId != currentRoundId || currentRoundId <= 0)
            return SecretSaltFeedbackResult.WrongRound;

        if (string.IsNullOrWhiteSpace(authenticatedAccountId))
            return SecretSaltFeedbackResult.MissingServerIdentity;

        if (!SecretSaltFeedbackValidator.IsValid(submission))
            return SecretSaltFeedbackResult.InvalidSubmission;

        StartRound(currentRoundId);

        var nickname = string.IsNullOrWhiteSpace(authenticatedNickname)
            ? "Нікнейм невідомий" : authenticatedNickname;

        return _reviews.TryAdd(authenticatedAccountId, new SecretSaltRecordedFeedback(nickname, submission))
            ? SecretSaltFeedbackResult.Accepted
            : SecretSaltFeedbackResult.AlreadySubmitted;
    }
}
