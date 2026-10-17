// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.GameTicking;

namespace Content.IntegrationTests.Tests.GameRules;

[TestFixture]
[Category("GameRuleTests")]
public sealed class SecretSaltRotationTest
{
    [Test]
    public void TestTwoPlayedRoundsBlockThirdAndDifferentModeResets()
    {
        var rotation = new SecretSaltRotationTracker();

        Assert.That(rotation.IsBlocked, Is.False);
        Assert.That(rotation.RecordPlayedRound(100, "SecretSalt"), Is.True);
        Assert.That(rotation.ConsecutiveRounds, Is.EqualTo(1));
        Assert.That(rotation.IsBlocked, Is.False);

        // A duplicated event for the same real round cannot increment twice.
        Assert.That(rotation.RecordPlayedRound(100, "SecretSalt"), Is.False);
        Assert.That(rotation.ConsecutiveRounds, Is.EqualTo(1));

        Assert.That(rotation.RecordPlayedRound(101, "SecretSalt"), Is.True);
        Assert.That(rotation.ConsecutiveRounds, Is.EqualTo(2));
        Assert.That(rotation.IsBlocked, Is.True);

        // An attempted mode selection is not a played round; it cannot reset
        // or increase the counter.
        Assert.That(rotation.RecordPlayedRound(0, "Secret"), Is.False);
        Assert.That(rotation.IsBlocked, Is.True);

        Assert.That(rotation.RecordPlayedRound(102, "Secret"), Is.True);
        Assert.That(rotation.ConsecutiveRounds, Is.Zero);
        Assert.That(rotation.IsBlocked, Is.False);

        Assert.That(rotation.RecordPlayedRound(103, "SecretSalt"), Is.True);
        Assert.That(rotation.ConsecutiveRounds, Is.EqualTo(1));
    }

    [Test]
    public void TestOnlyPlayedRoundsChangeStreakAndCounterIsCapped()
    {
        var rotation = new SecretSaltRotationTracker();

        Assert.That(rotation.RecordPlayedRound(-2, "SecretSalt"), Is.False);
        Assert.That(rotation.RecordPlayedRound(1, "SecretSalt"), Is.True);
        Assert.That(rotation.RecordPlayedRound(1, "Secret"), Is.False);
        Assert.That(rotation.ConsecutiveRounds, Is.EqualTo(1));

        Assert.That(rotation.RecordPlayedRound(2, "SecretSalt"), Is.True);
        Assert.That(rotation.RecordPlayedRound(3, "SecretSalt"), Is.True);
        Assert.That(rotation.ConsecutiveRounds, Is.EqualTo(2),
            "The capped count must remain stable even if an admin bypass is later added.");

        Assert.That(rotation.RecordPlayedRound(4, "SecretSaltPlus"), Is.True);
        Assert.That(rotation.ConsecutiveRounds, Is.Zero,
            "A different preset resets the streak.");
    }

    [Test]
    public void TestInterruptedSequences()
    {
        var rotation = new SecretSaltRotationTracker();

        Assert.That(rotation.RecordPlayedRound(1, "SecretSalt"), Is.True);
        Assert.That(rotation.RecordPlayedRound(2, "Secret"), Is.True);
        Assert.That(rotation.RecordPlayedRound(3, "SecretSalt"), Is.True);
        Assert.That(rotation.IsBlocked, Is.False);
        Assert.That(rotation.RecordPlayedRound(4, "SecretSalt"), Is.True);
        Assert.That(rotation.IsBlocked, Is.True);
    }
}
