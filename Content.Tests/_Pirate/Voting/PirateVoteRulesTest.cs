// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using Content.Server._Pirate.Voting;
using NUnit.Framework;

namespace Content.Tests._Pirate.Voting;

[TestFixture]
public sealed class PirateVoteRulesTest
{
    [TestCase(10, 0.50f, 5, 5)]
    [TestCase(10, 0.25f, 2, 8)]
    [TestCase(3, 0.50f, 1, 2)]
    [TestCase(1, 0.50f, 0, 1)]
    [TestCase(0, 0.50f, 0, 0)]
    [TestCase(10, 0f, 0, 10)]
    [TestCase(10, 1f, 10, 0)]
    [TestCase(10, 2f, 10, 0)]
    [TestCase(10, -1f, 0, 10)]
    public void PenaltiesSubtractWholeVotesWithoutNegativeTotals(int votes, float penalty, int removed, int effective)
    {
        Assert.Multiple(() =>
        {
            Assert.That(PirateVoteRules.SubtractedVotes(votes, penalty), Is.EqualTo(removed));
            Assert.That(PirateVoteRules.EffectiveVotes(votes, penalty), Is.EqualTo(effective));
        });
    }

    [Test]
    public void FinalistsAreTopThreePositiveAdjustedTotals()
    {
        var finalists = PirateVoteRules.SelectFinalists(
        [
            ("Redshift", PirateVoteRules.EffectiveVotes(10, 0.50f)),
            ("A", 8),
            ("B", 7),
            ("C", 6),
            ("Zero", 0),
        ], 3);

        Assert.That(finalists, Is.EqualTo(new[] { "A", "B", "C" }));
    }

    [TestCase(3, 2)]
    [TestCase(2, 2)]
    [TestCase(1, 1)]
    [TestCase(0, 1)]
    public void FinalistLimitDoesNotAddZeroVoteEntries(int limit, int expected)
    {
        var finalists = PirateVoteRules.SelectFinalists([("A", 5), ("B", 3), ("Zero", 0)], limit);
        Assert.That(finalists.Length, Is.EqualTo(expected));
    }

    [Test]
    public void NoVotesProducesNoFinalists()
    {
        Assert.That(PirateVoteRules.SelectFinalists([("A", 0), ("B", 0)], 3), Is.Empty);
    }

    [Test]
    public void OneVotedOptionProducesOneFinalist()
    {
        Assert.That(PirateVoteRules.SelectFinalists([("A", 0), ("B", 1)], 3), Is.EqualTo(new[] { "B" }));
    }

    [Test]
    public void BoundaryTiesUseShuffledInputOrder()
    {
        Assert.That(PirateVoteRules.SelectFinalists([("C", 4), ("B", 4), ("A", 4)], 2),
            Is.EqualTo(new[] { "C", "B" }));
    }

    [Test]
    public void RoundCountsOnceAtTenMinutesAndNotBefore()
    {
        var history = new PirateVoteHistory();
        history.BeginRound("Redshift");

        Assert.That(history.TryRecord(TimeSpan.FromSeconds(599), TimeSpan.FromMinutes(10), 2), Is.False);
        Assert.That(history.GetPenalty("Redshift", [0.50f, 0.25f]), Is.Zero);
        Assert.That(history.TryRecord(TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10), 2), Is.True);
        Assert.That(history.TryRecord(TimeSpan.FromMinutes(20), TimeSpan.FromMinutes(10), 2), Is.False);

        Record(history, "Other");
        Assert.That(history.GetPenalty("Redshift", [0.50f, 0.25f]), Is.EqualTo(0.25f));
        Record(history, "Another");
        Assert.That(history.GetPenalty("Redshift", [0.50f, 0.25f]), Is.Zero);
    }

    [Test]
    public void BrokenRoundsDoNotEnterOrAdvanceHistory()
    {
        var history = new PirateVoteHistory();
        Record(history, "Redshift");
        history.BeginRound("Greenshift");
        Assert.That(history.TryRecord(TimeSpan.FromMinutes(9), TimeSpan.FromMinutes(10), 2), Is.False);
        history.BeginRound(null);

        Assert.That(history.GetPenalty("Redshift", [0.50f, 0.25f]), Is.EqualTo(0.50f));
        Assert.That(history.GetPenalty("Greenshift", [0.50f, 0.25f]), Is.Zero);

        Record(history, "Greenshift");
        Assert.That(history.GetPenalty("Redshift", [0.50f, 0.25f]), Is.EqualTo(0.25f));
        Assert.That(history.GetPenalty("Greenshift", [0.50f, 0.25f]), Is.EqualTo(0.50f));
    }

    [Test]
    public void RepeatedModeUsesMostRecentPenaltyWithoutStacking()
    {
        var history = new PirateVoteHistory();
        Record(history, "Redshift");
        Record(history, "Redshift");
        Assert.That(history.GetPenalty("Redshift", [0.50f, 0.25f]), Is.EqualTo(0.50f));
    }

    [Test]
    public void ThresholdAndPenaltyValuesAreConfigurable()
    {
        var history = new PirateVoteHistory();
        history.BeginRound("Redshift");
        Assert.That(history.TryRecord(TimeSpan.FromMinutes(4), TimeSpan.FromMinutes(5), 2), Is.False);
        Assert.That(history.TryRecord(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5), 2), Is.True);
        Assert.That(history.GetPenalty("Redshift", [0.60f, 0.10f]), Is.EqualTo(0.60f));
    }

    [Test]
    public void GreenshiftPenaltyUsesThreeValidRounds()
    {
        var history = new PirateVoteHistory();
        float[] penalties = [0.50f, 0.40f, 0.30f];

        Record(history, "PirateGreenshift", 3);
        Assert.That(history.GetPenalty("PirateGreenshift", penalties), Is.EqualTo(0.50f));
        Record(history, "Other", 3);
        Assert.That(history.GetPenalty("PirateGreenshift", penalties), Is.EqualTo(0.40f));

        history.BeginRound("Broken");
        Assert.That(history.TryRecord(TimeSpan.FromMinutes(9), TimeSpan.FromMinutes(10), 3), Is.False);
        history.BeginRound(null);
        Assert.That(history.GetPenalty("PirateGreenshift", penalties), Is.EqualTo(0.40f));

        Record(history, "Redshift", 3);
        Assert.That(history.GetPenalty("PirateGreenshift", penalties), Is.EqualTo(0.30f));
        Record(history, "Another", 3);
        Assert.That(history.GetPenalty("PirateGreenshift", penalties), Is.Zero);
        Assert.That(history.GetPenalty("Redshift", [0.50f, 0.25f]), Is.EqualTo(0.25f));
    }

    private static void Record(PirateVoteHistory history, string preset, int historyLength = 2)
    {
        history.BeginRound(preset);
        Assert.That(history.TryRecord(TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10), historyLength), Is.True);
    }
}
