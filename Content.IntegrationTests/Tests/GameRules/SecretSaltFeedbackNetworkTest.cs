// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Content.Goobstation.Client.RoundEnd;
using Content.Goobstation.Common.SecretSalt;
using Content.Goobstation.Server.StationEvents.SecretPlus;
using Content.Server.GameTicking;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;

namespace Content.IntegrationTests.Tests.GameRules;

/// <summary>
/// Exercises the actual client-to-server feedback event, not just the tracker.
/// No Discord secrets are present; server-side delivery is not contacted.
/// </summary>
[TestFixture]
[Category("GameRuleTests")]
public sealed class SecretSaltFeedbackNetworkTest
{
    private static SecretSaltFeedbackTracker GetTracker(SecretSaltFeedbackSystem system)
    {
        var field = typeof(SecretSaltFeedbackSystem).GetField(
            "_tracker", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        return (SecretSaltFeedbackTracker) field!.GetValue(system)!;
    }

    private static SecretSaltFeedbackWindow? GetFeedbackWindow(SecretSaltFeedbackClientSystem system)
    {
        var field = typeof(SecretSaltFeedbackClientSystem).GetField(
            "_window", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        return (SecretSaltFeedbackWindow?) field!.GetValue(system);
    }

    private static HashSet<NetUserId> GetParticipants(SecretSaltFeedbackSystem system)
    {
        var field = typeof(SecretSaltFeedbackSystem).GetField(
            "_participants", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        return (HashSet<NetUserId>) field!.GetValue(system)!;
    }

    [Test]
    public async Task TestOrdinarySecretNeverOffersSaltFeedback()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true,
            DummyTicker = false,
            Connected = true,
            InLobby = true
        });

        var server = pair.Server;
        var client = pair.Client;
        await server.WaitIdleAsync();

        await server.WaitAssertion(() => server.CfgMan.SetCVar(CCVars.GridFill, true));
        await pair.WaitClientCommand("toggleready True");
        await pair.WaitCommand("setgamepreset Secret 9999");
        await pair.WaitCommand("startround");
        await pair.RunTicksSync(10);

        var round = 0;
        await server.WaitAssertion(() =>
        {
            var systems = server.ResolveDependency<IEntitySystemManager>();
            var ticker = systems.GetEntitySystem<GameTicker>();
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));
            Assert.That(ticker.CurrentPreset?.ID, Is.EqualTo("Secret"));
            round = ticker.RoundId;
            Assert.That(GetParticipants(systems.GetEntitySystem<SecretSaltFeedbackSystem>()),
                Is.Empty, "Even a ready player must not qualify in another preset.");
            ticker.EndRound();
        });
        await pair.RunTicksSync(5);

        await client.WaitAssertion(() =>
        {
            var system = client.ResolveDependency<IEntitySystemManager>()
                .GetEntitySystem<SecretSaltFeedbackClientSystem>();
            Assert.That(GetFeedbackWindow(system), Is.Null,
                "Ordinary Secret must not offer Secret Salt's post-round dialog.");
            // A modified client must not be able to bypass the mode check.
            system.SubmitFeedback(round, 5, new[] { SecretSaltFeedbackReason.Other },
                "Not a Secret Salt round");
        });
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var systems = server.ResolveDependency<IEntitySystemManager>();
            var feedback = GetTracker(systems.GetEntitySystem<SecretSaltFeedbackSystem>())
                .GetReviews(round);
            Assert.That(feedback, Is.Empty);
            systems.GetEntitySystem<GameTicker>().RestartRound();
            server.CfgMan.SetCVar(CCVars.GridFill, CCVars.GridFill.DefaultValue);
        });
        await pair.RunTicksSync(3);
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TestActualClientSubmissionRejectedInRoundAndDeduplicatedAfterEnd()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true,
            DummyTicker = false,
            Connected = true,
            InLobby = true
        });

        var server = pair.Server;
        var client = pair.Client;
        await server.WaitIdleAsync();

        await server.WaitAssertion(() => server.CfgMan.SetCVar(CCVars.GridFill, true));
        await pair.WaitClientCommand("toggleready True");
        await pair.WaitCommand("setgamepreset SecretSalt 9999");
        await pair.WaitCommand("startround");
        await pair.RunTicksSync(10);

        var round = 0;
        await server.WaitAssertion(() =>
        {
            var systems = server.ResolveDependency<IEntitySystemManager>();
            var ticker = systems.GetEntitySystem<GameTicker>();
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));
            round = ticker.RoundId;

            var feedbackSystem = systems.GetEntitySystem<SecretSaltFeedbackSystem>();
            var registered = GetParticipants(feedbackSystem);
            Assert.That(registered.Count, Is.EqualTo(1),
                "A regular roundstart crew member must be eligible.");
            var account = registered.Single();

            // This simulates the *existing* GameTicker roster state used by
            // pre-spawn special antagonists (who call PlayerJoinGame instead
            // of raising PlayerSpawnCompleteEvent).
            var readyAtStart = new HashSet<NetUserId> { account };
            var statuses = new Dictionary<NetUserId, PlayerGameStatus>
            {
                [account] = PlayerGameStatus.JoinedGame
            };
            Assert.That(SecretSaltFeedbackEligibility.IsConfirmedRoundStartParticipant(
                account, readyAtStart, statuses), Is.True,
                "A special antagonist with ReadyToPlay -> JoinedGame is included.");

            // A late-joiner may have JoinedGame, but was not in the original
            // ReadyToPlay snapshot and must never gain review permission.
            readyAtStart.Clear();
            Assert.That(SecretSaltFeedbackEligibility.IsConfirmedRoundStartParticipant(
                account, readyAtStart, statuses), Is.False);

            // A ready player who never spawned receives no review permission.
            readyAtStart.Add(account);
            statuses[account] = PlayerGameStatus.ReadyToPlay;
            Assert.That(SecretSaltFeedbackEligibility.IsConfirmedRoundStartParticipant(
                account, readyAtStart, statuses), Is.False);
            statuses[account] = PlayerGameStatus.NotReadyToPlay;
            Assert.That(SecretSaltFeedbackEligibility.IsConfirmedRoundStartParticipant(
                account, readyAtStart, statuses), Is.False);
        });

        await client.WaitAssertion(() =>
        {
            var system = client.ResolveDependency<IEntitySystemManager>()
                .GetEntitySystem<SecretSaltFeedbackClientSystem>();
            system.SubmitFeedback(round, 5, new[] { SecretSaltFeedbackReason.Other }, "Too early");
        });
        await pair.RunTicksSync(3);

        await server.WaitAssertion(() =>
        {
            var system = server.ResolveDependency<IEntitySystemManager>()
                .GetEntitySystem<SecretSaltFeedbackSystem>();
            Assert.That(GetTracker(system).GetReviews(round), Is.Empty,
                "A feedback event during an active round must be ignored.");
            server.ResolveDependency<IEntitySystemManager>()
                .GetEntitySystem<GameTicker>().EndRound();
        });
        await pair.RunTicksSync(5);

        // A real spawned player was registered by the server. Temporarily
        // remove that participant to simulate a client submitting as an
        // unqualified observer. Reinstating eligibility permits a real review.
        NetUserId eligibleUser = default;
        await server.WaitAssertion(() =>
        {
            var feedback = server.ResolveDependency<IEntitySystemManager>()
                .GetEntitySystem<SecretSaltFeedbackSystem>();
            var participants = GetParticipants(feedback);
            Assert.That(participants.Count, Is.EqualTo(1));
            eligibleUser = participants.Single();
            participants.Clear();
        });
        await client.WaitAssertion(() =>
        {
            var system = client.ResolveDependency<IEntitySystemManager>()
                .GetEntitySystem<SecretSaltFeedbackClientSystem>();
            system.SubmitFeedback(round, 5, new[] { SecretSaltFeedbackReason.Other },
                "Unqualified account must be rejected");
        });
        await pair.RunTicksSync(3);
        await server.WaitAssertion(() =>
        {
            var feedback = server.ResolveDependency<IEntitySystemManager>()
                .GetEntitySystem<SecretSaltFeedbackSystem>();
            Assert.That(GetTracker(feedback).GetReviews(round), Is.Empty);
            GetParticipants(feedback).Add(eligibleUser);
        });

        await client.WaitAssertion(() =>
        {
            var system = client.ResolveDependency<IEntitySystemManager>()
                .GetEntitySystem<SecretSaltFeedbackClientSystem>();
            Assert.That(GetFeedbackWindow(system), Is.Not.Null,
                "The post-round scoreboard and SecretSalt offer must open the review window.");
            system.SubmitFeedback(round, 4,
                new[] { SecretSaltFeedbackReason.TooFewEvents }, "Мало подій @everyone");
            system.SubmitFeedback(round, 1,
                new[] { SecretSaltFeedbackReason.Other }, "Duplicate");
            system.SubmitFeedback(round, 0,
                new[] { SecretSaltFeedbackReason.Other }, "Invalid rating");
            system.SubmitFeedback(round + 1, 5,
                new[] { SecretSaltFeedbackReason.Other }, "Wrong round");
        });
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var systems = server.ResolveDependency<IEntitySystemManager>();
            var ticker = systems.GetEntitySystem<GameTicker>();
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.PostRound));

            var feedback = GetTracker(systems.GetEntitySystem<SecretSaltFeedbackSystem>())
                .GetReviews(round);
            Assert.That(feedback.Count, Is.EqualTo(1),
                "Only one valid, authenticated post-round review should be stored.");
            Assert.That(feedback.Single().Submission.Rating, Is.EqualTo(4));
            Assert.That(feedback.Single().Submission.Comment,
                Is.EqualTo("Мало подій @everyone"));

        });

        // Test-only time jump: end the 60-second collection without waiting
        // in real time and verify that the server closes the client's dialog.
        await server.WaitAssertion(() =>
        {
            var feedback = server.ResolveDependency<IEntitySystemManager>()
                .GetEntitySystem<SecretSaltFeedbackSystem>();
            var field = typeof(SecretSaltFeedbackSystem).GetField(
                "_feedbackDeadline", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field!.SetValue(feedback, TimeSpan.Zero);
        });
        await pair.RunTicksSync(5);
        await client.WaitAssertion(() =>
        {
            var feedback = client.ResolveDependency<IEntitySystemManager>()
                .GetEntitySystem<SecretSaltFeedbackClientSystem>();
            Assert.That(GetFeedbackWindow(feedback), Is.Null,
                "The review window must close when the server stops accepting reviews.");
        });

        await server.WaitAssertion(() =>
        {
            var ticker = server.ResolveDependency<IEntitySystemManager>()
                .GetEntitySystem<GameTicker>();
            ticker.RestartRound();
            server.CfgMan.SetCVar(CCVars.GridFill, CCVars.GridFill.DefaultValue);
        });
        await pair.RunTicksSync(3);
        await client.WaitAssertion(() =>
        {
            var feedback = client.ResolveDependency<IEntitySystemManager>()
                .GetEntitySystem<SecretSaltFeedbackClientSystem>();
            Assert.That(GetFeedbackWindow(feedback), Is.Null,
                "A closed review must not reappear in the next round's lobby.");
        });

        await pair.CleanReturnAsync();
    }
}
