// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Linq;
using System.Reflection;
using Content.Goobstation.Client.RoundEnd;
using Content.Goobstation.Common.SecretSalt;
using Content.Goobstation.Server.StationEvents.SecretPlus;
using Content.Server.GameTicking;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Robust.Shared.GameObjects;

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

        await client.WaitAssertion(() =>
        {
            var system = client.ResolveDependency<IEntitySystemManager>()
                .GetEntitySystem<SecretSaltFeedbackClientSystem>();
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

            ticker.RestartRound();
            server.CfgMan.SetCVar(CCVars.GridFill, CCVars.GridFill.DefaultValue);
        });

        await pair.CleanReturnAsync();
    }
}
