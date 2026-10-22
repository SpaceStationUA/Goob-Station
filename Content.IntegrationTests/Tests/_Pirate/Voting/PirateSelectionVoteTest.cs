// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Linq;
using System.Reflection;
using Content.Server._Pirate.Voting;
using Content.Server.Voting;
using Content.Server.Voting.Managers;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Moq;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;

namespace Content.IntegrationTests.Tests._Pirate.Voting;

[TestFixture]
public sealed class PirateSelectionVoteTest
{
    [TestCase(0, 0)]
    [TestCase(1, 0)]
    [TestCase(2, 2)]
    [TestCase(4, 3)]
    public async Task SelectionBallotsUseOnlySupportedFinalistsAndFreshVotes(int supported, int expectedFinalists)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var manager = (VoteManager) server.ResolveDependency<IVoteManager>();
            var resolved = false;
            var options = Options();
            StartSelection(manager, options, (_, _) => resolved = true);
            var first = manager.ActiveVotes.Single();
            Assert.That(first.Title, Is.EqualTo(options.Title));
            for (var i = 0; i < supported; i++)
                Cast(first, i, supported - i);

            // Resolving ballot 1 creates ballot 2 during the manager's update loop.
            manager.Update();
            Assert.That(first.Finished, Is.True);
            if (expectedFinalists == 0)
            {
                Assert.That(resolved, Is.True);
                Assert.That(manager.ActiveVotes, Is.Empty);
                return;
            }

            Assert.That(resolved, Is.False, "Ballot 1 must not apply a winner before ballot 2.");
            var second = manager.ActiveVotes.Single();
            Assert.That(second.Id, Is.Not.EqualTo(first.Id));
            Assert.That(second.Title, Is.EqualTo(options.Title));
            Assert.That(second.VotesPerOption.Keys,
                Is.EquivalentTo(options.Options.Take(expectedFinalists).Select(o => o.data)));
            Assert.That(second.VotesPerOption.Values, Is.All.Zero);

            Cast(second, 0, 1);
            manager.Update();
            Assert.That(resolved, Is.True);
            Assert.That(manager.ActiveVotes, Is.Empty, "There must not be a third ballot.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PenaltiesAffectBothBallotsAndResultTallies()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var manager = (VoteManager) server.ResolveDependency<IVoteManager>();
            var options = Options();
            options.PiratePenalties["A"] = 0.50f;
            object winner = null;
            StartSelection(manager, options, (_, args) => winner = args.Winner);
            var first = manager.ActiveVotes.Single();
            Cast(first, 0, 10);
            Cast(first, 1, 6);
            Cast(first, 2, 4);
            Cast(first, 3, 3);
            Assert.That(first.VotesPerOption["A"], Is.EqualTo(5));
            manager.Update();

            var second = manager.ActiveVotes.Single();
            var keys = second.VotesPerOption.Keys.ToList();
            Assert.That(keys, Is.EquivalentTo(new[] { "A", "B", "C" }));
            Cast(second, keys.IndexOf("A"), 10);
            Cast(second, keys.IndexOf("B"), 6);
            Assert.That(second.VotesPerOption["A"], Is.EqualTo(5));
            manager.Update();
            Assert.That(winner, Is.EqualTo("B"));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RestartCancelsSelectionWithoutApplyingWinner()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var manager = (VoteManager) server.ResolveDependency<IVoteManager>();
            var resolved = false;
            StartSelection(manager, Options(), (_, _) => resolved = true);
            var vote = manager.ActiveVotes.Single();
            Cast(vote, 0, 2);
            Cast(vote, 1, 1);
            server.EntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());
            manager.Update();
            Assert.That(vote.Cancelled, Is.True);
            Assert.That(resolved, Is.False);
            Assert.That(manager.ActiveVotes, Is.Empty);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DefaultConfigurationTargetsTheVoteEnabledModes()
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var config = pair.Server.System<PirateVotingSystem>().Configuration;
            Assert.That(config.FinalistCount, Is.EqualTo(3));
            Assert.That(CCVars.VoteMinimumPlayedMinutes.DefaultValue, Is.EqualTo(10f));
            Assert.That(config.ModePenalties["SecretSalt"], Is.EqualTo(new[] { 0.50f, 0.25f }));
            Assert.That(config.ModePenalties["PirateGreenshift"], Is.EqualTo(new[] { 0.50f, 0.40f, 0.30f }));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MinimumPlayedTimeTracksCVarChanges()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        await server.WaitAssertion(() =>
        {
            var system = server.System<PirateVotingSystem>();
            var original = server.CfgMan.GetCVar(CCVars.VoteMinimumPlayedMinutes);
            try
            {
                server.CfgMan.SetCVar(CCVars.VoteMinimumPlayedMinutes, 10f);
                Assert.That(system.MinimumPlayedTime, Is.EqualTo(TimeSpan.FromMinutes(10)));
                server.CfgMan.SetCVar(CCVars.VoteMinimumPlayedMinutes, 5.5f);
                Assert.That(system.MinimumPlayedTime, Is.EqualTo(TimeSpan.FromMinutes(5.5)));
                server.CfgMan.SetCVar(CCVars.VoteMinimumPlayedMinutes, -1f);
                Assert.That(system.MinimumPlayedTime, Is.EqualTo(TimeSpan.Zero));
            }
            finally
            {
                server.CfgMan.SetCVar(CCVars.VoteMinimumPlayedMinutes, original);
            }
        });
        await pair.CleanReturnAsync();
    }

    private static VoteOptions Options()
    {
        return new VoteOptions
        {
            Title = "Selection test",
            Duration = TimeSpan.Zero,
            Options = [("A", "A"), ("B", "B"), ("C", "C"), ("D", "D")],
        };
    }

    private static void StartSelection(VoteManager manager, VoteOptions options, VoteFinishedEventHandler onFinished)
    {
        // Use a small ballot to test the real manager without changing map pools or preset eligibility.
        typeof(VoteManager).GetMethod("CreatePirateSelectionVote", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(manager, [options, onFinished, false]);
    }

    private static void Cast(IVoteHandle vote, int option, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var session = new Mock<ICommonSession>();
            session.SetupGet(s => s.Name).Returns($"Voter {option}-{i}");
            vote.CastVote(session.Object, option);
        }
    }
}
