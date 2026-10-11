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
    public void TestBoundedNetworkReasonMask()
    {
        var all = new[]
        {
            SecretSaltFeedbackReason.TooFewAntagonists,
            SecretSaltFeedbackReason.TooManyAntagonists,
            SecretSaltFeedbackReason.TooFewEvents,
            SecretSaltFeedbackReason.TooChaotic,
            SecretSaltFeedbackReason.Other
        };

        var mask = SecretSaltFeedbackReasonMask.Encode(all);
        Assert.That(mask, Is.EqualTo(0b0001_1111));
        Assert.That(SecretSaltFeedbackReasonMask.TryDecode(mask, out var decoded), Is.True);
        Assert.That(decoded, Is.EquivalentTo(all));

        Assert.That(SecretSaltFeedbackReasonMask.TryDecode(0, out var empty), Is.True);
        Assert.That(empty, Is.Empty);
        Assert.That(SecretSaltFeedbackReasonMask.TryDecode(0b0010_0000, out _), Is.False);
        Assert.That(SecretSaltFeedbackReasonMask.TryDecode(byte.MaxValue, out _), Is.False);

        Assert.Throws<ArgumentException>(() =>
            SecretSaltFeedbackReasonMask.Encode(new[] { SecretSaltFeedbackReason.Other, SecretSaltFeedbackReason.Other }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SecretSaltFeedbackReasonMask.Encode(new[] { (SecretSaltFeedbackReason) 99 }));

        // Guard the wire contract against accidental reintroduction of
        // client-sized reason arrays before server-side validation.
        Assert.That(typeof(SecretSaltFeedbackRequestEvent).GetProperty("Reasons"), Is.Null);
        Assert.That(typeof(SecretSaltFeedbackRequestEvent).GetProperty("ReasonsMask")?.PropertyType,
            Is.EqualTo(typeof(byte)));
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

        Assert.That(tracker.TryRecord(123, "account-A", "Гравець A", Review()), Is.EqualTo(SecretSaltFeedbackResult.Accepted));
        Assert.That(tracker.TryRecord(123, "account-A", "Підмінене ім’я", Review(rating: 2)), Is.EqualTo(SecretSaltFeedbackResult.AlreadySubmitted));
        Assert.That(tracker.TryRecord(123, "account-B", "Гравець B", Review(rating: 2)), Is.EqualTo(SecretSaltFeedbackResult.Accepted));
        Assert.That(tracker.Count, Is.EqualTo(2));
        Assert.That(tracker.GetReviews(123)[0].Nickname, Is.EqualTo("Гравець A"));
        Assert.That(tracker.GetReviews(123).Count, Is.EqualTo(2));

        // A malicious or stale client must not clear the current round's guard.
        Assert.That(tracker.TryRecord(124, "account-C", "Гравець C", Review(round: 123)), Is.EqualTo(SecretSaltFeedbackResult.WrongRound));
        Assert.That(tracker.Count, Is.EqualTo(2));
        Assert.That(tracker.TryRecord(123, "", "Невідомий", Review()), Is.EqualTo(SecretSaltFeedbackResult.MissingServerIdentity));

        Assert.That(tracker.TryRecord(124, "account-A", "Гравець A", Review(round: 124)), Is.EqualTo(SecretSaltFeedbackResult.Accepted));
        Assert.That(tracker.Count, Is.EqualTo(1));
    }
    [Test]
    public void TestPrivateAuthorReportSanitizesDiscordAndOmitsAccountIds()
    {
        var tracker = new SecretSaltFeedbackTracker();
        var submission = Review(
            reasons: new[] { SecretSaltFeedbackReason.TooChaotic, SecretSaltFeedbackReason.Other },
            comment: "Надто багато подій! @everyone\\nГравець: admin");
        Assert.That(tracker.TryRecord(123, "private-user-id-123", "@here Player",
            submission), Is.EqualTo(SecretSaltFeedbackResult.Accepted));

        var report = SecretSaltFeedbackReport.Format(123, tracker.GetReviews(123));
        Assert.That(report, Does.Contain("Секрет Сіль 1.4"));
        Assert.That(report, Does.Contain("Отримано відгуків: 1"));
        Assert.That(report, Does.Contain("Оцінка: 5/5"));
        Assert.That(report, Does.Contain("Надмірний хаос, Інше"));
        Assert.That(report, Does.Contain("＠everyone"));
        Assert.That(report, Does.Contain("＠here Player"));
        Assert.That(report, Does.Not.Contain("private-user-id-123"));
        Assert.That(report, Does.Not.Contain("@everyone"));
        Assert.That(report, Does.Not.Contain("@here"));
    }

    [Test]
    public void TestFeedbackSummaryAverageAndMostCommonReasons()
    {
        var tracker = new SecretSaltFeedbackTracker();
        Assert.That(tracker.TryRecord(123, "private-A", "А", Review(rating: 5,
            reasons: new[] { SecretSaltFeedbackReason.TooFewEvents, SecretSaltFeedbackReason.TooChaotic })),
            Is.EqualTo(SecretSaltFeedbackResult.Accepted));
        Assert.That(tracker.TryRecord(123, "private-B", "Б", Review(rating: 4,
            reasons: new[] { SecretSaltFeedbackReason.TooFewEvents })),
            Is.EqualTo(SecretSaltFeedbackResult.Accepted));
        Assert.That(tracker.TryRecord(123, "private-C", "В", Review(rating: 2,
            reasons: new[] { SecretSaltFeedbackReason.TooChaotic, SecretSaltFeedbackReason.Other })),
            Is.EqualTo(SecretSaltFeedbackResult.Accepted));

        var report = SecretSaltFeedbackReport.Format(123, tracker.GetReviews(123));
        Assert.That(report, Does.Contain("Отримано відгуків: 3"));
        Assert.That(report, Does.Contain("Середня оцінка: 3,67/5"));
        Assert.That(report, Does.Contain(
            "Найчастіші причини: Замало подій — 2; Надмірний хаос — 2; Інше — 1"));
        Assert.That(report, Does.Not.Contain("private-A"));
        Assert.That(report, Does.Not.Contain("private-B"));
        Assert.That(report, Does.Not.Contain("private-C"));
    }

    [Test]
    public void TestFeedbackSummaryWithoutSelectedReasons()
    {
        var tracker = new SecretSaltFeedbackTracker();
        Assert.That(tracker.TryRecord(123, "private-D", "Г", Review(
            reasons: Array.Empty<SecretSaltFeedbackReason>())),
            Is.EqualTo(SecretSaltFeedbackResult.Accepted));
        var report = SecretSaltFeedbackReport.Format(123, tracker.GetReviews(123));
        Assert.That(report, Does.Contain("Середня оцінка: 5,00/5"));
        Assert.That(report, Does.Contain("Найчастіші причини: Не вказано"));
    }

    [Test]
    public void TestNoReviewReportAndRoundIsolation()
    {
        var tracker = new SecretSaltFeedbackTracker();
        tracker.StartRound(123);
        var emptyReport = SecretSaltFeedbackReport.Format(123, tracker.GetReviews(123));
        Assert.That(emptyReport, Does.Contain("Гравці не залишили відгуків."));
        Assert.That(emptyReport, Does.Contain("Середня оцінка: немає даних."));
        Assert.That(emptyReport, Does.Contain("Найчастіші причини: немає даних."));
        Assert.That(tracker.TryRecord(123, "account-A", "Гравець A", Review()),
            Is.EqualTo(SecretSaltFeedbackResult.Accepted));
        tracker.StartRound(124);
        Assert.That(tracker.GetReviews(123), Is.Empty);
        Assert.That(tracker.GetReviews(124), Is.Empty);
    }

}
