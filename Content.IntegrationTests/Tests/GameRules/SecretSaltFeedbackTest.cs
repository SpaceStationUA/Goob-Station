// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using Content.Goobstation.Common.SecretSalt;
using Content.Goobstation.Server.StationEvents.SecretPlus;

namespace Content.IntegrationTests.Tests.GameRules;

/// <summary>
/// Validates player-supplied reviews and the one-per-account-per-round guard.
/// The actual post-round UI and network handlers are separate, pending work.
/// </summary>
[TestFixture]
[Category("GameRuleTests")]
public sealed class SecretSaltFeedbackTest
{
    private static SecretSaltFeedbackSubmission Review(
        int round = 123,
        int rating = 5,
        SecretSaltFeedbackReason[]? reasons = null,
        string? comment = null)
    {
        return new SecretSaltFeedbackSubmission
        {
            RoundId = round,
            Rating = rating,
            Reasons = reasons ?? new[] { SecretSaltFeedbackReason.Other },
            Comment = comment
        };
    }

    [Test]
    public void TestRatingAndReasonValidation()
    {
        Assert.That(SecretSaltFeedbackValidator.IsValid(Review(rating: 1)), Is.True);
        Assert.That(SecretSaltFeedbackValidator.IsValid(Review(rating: 5)), Is.True);
        Assert.That(SecretSaltFeedbackValidator.IsValid(Review(rating: 0)), Is.False);
        Assert.That(SecretSaltFeedbackValidator.IsValid(Review(rating: 6)), Is.False);
        Assert.That(SecretSaltFeedbackValidator.IsValid(Review(round: 0)), Is.False);
        Assert.That(SecretSaltFeedbackValidator.IsValid(Review(
            reasons: Array.Empty<SecretSaltFeedbackReason>())), Is.True);
        Assert.That(SecretSaltFeedbackValidator.IsValid(Review(
            reasons: new[] { SecretSaltFeedbackReason.TooChaotic, SecretSaltFeedbackReason.TooChaotic })), Is.False);
        Assert.That(SecretSaltFeedbackValidator.IsValid(Review(
            reasons: new[] { (SecretSaltFeedbackReason) 99 })), Is.False);
    }

    [Test]
    public void TestCommentLengthWithoutContentCensorship()
    {
        Assert.That(SecretSaltFeedbackValidator.IsValid(Review(comment: new string('x', 500))), Is.True);
        Assert.That(SecretSaltFeedbackValidator.IsValid(Review(comment: new string('x', 501))), Is.False);
        Assert.That(SecretSaltFeedbackValidator.IsValid(Review(
            comment: "@everyone\nНегативний відгук, але він дозволений.")), Is.True);
    }

    [Test]
    public void TestOneReviewPerAccountPerRound()
    {
        var tracker = new SecretSaltFeedbackTracker();

        Assert.That(tracker.TryRecord(123, "account-A", Review()), Is.EqualTo(SecretSaltFeedbackResult.Accepted));
        Assert.That(tracker.TryRecord(123, "account-A", Review(rating: 2)), Is.EqualTo(SecretSaltFeedbackResult.AlreadySubmitted));
        Assert.That(tracker.TryRecord(123, "account-B", Review(rating: 2)), Is.EqualTo(SecretSaltFeedbackResult.Accepted));
        Assert.That(tracker.Count, Is.EqualTo(2));

        // A malicious or stale client must not clear the current round's guard.
        Assert.That(tracker.TryRecord(124, "account-C", Review(round: 123)), Is.EqualTo(SecretSaltFeedbackResult.WrongRound));
        Assert.That(tracker.Count, Is.EqualTo(2));
        Assert.That(tracker.TryRecord(123, "", Review()), Is.EqualTo(SecretSaltFeedbackResult.MissingServerIdentity));

        Assert.That(tracker.TryRecord(124, "account-A", Review(round: 124)), Is.EqualTo(SecretSaltFeedbackResult.Accepted));
        Assert.That(tracker.Count, Is.EqualTo(1));
    }
}
