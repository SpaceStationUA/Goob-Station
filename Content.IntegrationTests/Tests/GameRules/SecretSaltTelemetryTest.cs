// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Goobstation.Server.StationEvents.SecretPlus;
using Content.Server.GameTicking;
using Content.Shared.GameTicking.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.GameRules;

/// <summary>
/// Checks that Secret Salt roundstart telemetry describes the actual rules added by
/// the scheduler, without equating a random pick to an antagonist assignment.
/// </summary>
[TestFixture]
[Category("GameRuleTests")]
public sealed class SecretSaltTelemetryTest
{
    [Test]
    public async Task TestRoundstartCountersAndRuleStarts()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true,
            Connected = true,
            InLobby = true
        });

        var server = pair.Server;
        await server.WaitIdleAsync();

        await server.WaitAssertion(() =>
        {
            var entMan = server.ResolveDependency<IEntityManager>();
            var ticker = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<GameTicker>();

            var schedulerUid = ticker.AddGameRule("SecretSalt");
            var salt = entMan.GetComponent<SecretSaltPlusComponent>(schedulerUid);

            // Every random candidate ends in a queued rule or one of the
            // recorded rejection reasons. Exhausting the pool isn't a pick.
            var rejectedPicks = salt.TelemetryRoundstartSkips
                .Where(pair => pair.Key != "noRoundstartAntags" && pair.Key != "noPositiveWeights")
                .Sum(pair => pair.Value);

            Assert.That(salt.TelemetryRoundstartAttempts,
                Is.EqualTo(salt.TelemetryRoundstartAdded + rejectedPicks));
            Assert.That(salt.TelemetryRoundstartAdded, Is.EqualTo(salt.TelemetryRules.Count));
            Assert.That(salt.TelemetryStartedRules, Is.Empty,
                "Roundstart rules must not be reported as started while still in the lobby.");

            // Same transition used by the game ticker to start added rules.
            ticker.StartGamePresetRules();

            Assert.That(salt.TelemetryStartedRules.All(salt.TelemetryRules.ContainsKey), Is.True);
            foreach (var ruleUid in salt.TelemetryStartedRules)
            {
                Assert.That(entMan.HasComponent<ActiveGameRuleComponent>(ruleUid)
                    || entMan.HasComponent<EndedGameRuleComponent>(ruleUid), Is.True);
            }

            ticker.ClearGameRules();
        });

        await pair.CleanReturnAsync();
    }
}
