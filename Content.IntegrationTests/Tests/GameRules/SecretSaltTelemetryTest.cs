// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Goobstation.Server.StationEvents.SecretPlus;
using Content.Server.GameTicking;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Server.StationEvents.Components;
using Content.Server.StationEvents;
using Content.Goobstation.Server.StationEvents.Components;
using Content.Server.Antag.Components;
using Content.Shared.Random;
using Robust.Shared.Prototypes;
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
    public async Task TestSaltExpandedCandidatePrototypesAreUsable()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true
        });

        var server = pair.Server;
        await server.WaitIdleAsync();
        await server.WaitAssertion(() =>
        {
            var entityManager = server.ResolveDependency<IEntityManager>();
            var ticker = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<GameTicker>();
            var rules = new[]
            {
                "SecretSaltConspirators",
                "SecretSaltSiliconLiberation",
                "SecretSaltBloodCult",
                "SecretSaltGangwars",
                "Letoferol",
                "VoidwalkerSpawn",
                "SecretSaltTerrorSpidersSpawn",
                "AsakimShuttle",
                "TraitorMidround",
                "MalfAiMidround",
            };
            foreach (var id in rules)
            {
                // A rule being loadable is necessary but not sufficient to
                // prove it can be selected and spawned in a real Salt round.
                var uid = ticker.AddGameRule(id);
                Assert.That(entityManager.HasComponent<GameRuleComponent>(uid), Is.True,
                    $"Expanded Salt candidate '{id}' must be an actual game rule.");
            }

            foreach (var id in new[]
                     {
                         "SecretSaltTerrorSpidersSpawn",
                         "AsakimShuttle",
                         "TraitorMidround",
                         "VoidwalkerSpawn",
                         "MalfAiMidround",
                         "VoidwalkerSpawn",
                     })
            {
                var uid = ticker.GetAddedGameRules().First(rule =>
                    entityManager.GetComponent<MetaDataComponent>(rule).EntityPrototype?.ID == id);
                Assert.That(entityManager.HasComponent<StationEventComponent>(uid), Is.True,
                    $"Midround candidate '{id}' needs StationEvent to pass the event scheduler.");
            }

            ticker.ClearGameRules();
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TestNewMidroundEventsAreReachableFromSaltEventTable()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true
        });

        var server = pair.Server;
        await server.WaitIdleAsync();

        await server.WaitAssertion(() =>
        {
            var entityManager = server.ResolveDependency<IEntityManager>();
            var systems = server.ResolveDependency<IEntitySystemManager>();
            var ticker = systems.GetEntitySystem<GameTicker>();
            var events = systems.GetEntitySystem<EventManagerSystem>();
            var salt = ticker.AddGameRule("SecretSalt");
            var table = entityManager.GetComponent<SelectedGameRulesComponent>(salt);

            // Call the same table-flattening and eligibility methods as Salt.
            // The synthetic population and clock avoid relying on real clients.
            var available = events.AvailableEvents(
                playerCountOverride: 100,
                currentTimeOverride: TimeSpan.FromMinutes(90));
            Assert.That(events.TryBuildLimitedEvents(table.ScheduledGameRules, available, out var pool),
                Is.True, "Salt must have an eligible event pool.");

            foreach (var id in new[]
                     {
                         "SecretSaltTerrorSpidersSpawn",
                         "AsakimShuttle",
                         "TraitorMidround",
                     })
            {
                Assert.That(pool.Keys.Any(proto => proto.ID == id), Is.True,
                    $"'{id}' must be reachable from SecretSaltTable through the event scheduler.");
            }

            // Terror spiders must not bypass their minimum player requirement.
            var lowPopulation = events.AvailableEvents(
                playerCountOverride: 20,
                currentTimeOverride: TimeSpan.FromMinutes(90));
            Assert.That(lowPopulation.Keys.Any(proto => proto.ID == "SecretSaltTerrorSpidersSpawn"),
                Is.False, "Terror spiders are unavailable below 50 players.");
            Assert.That(lowPopulation.Keys.Any(proto => proto.ID == "VoidwalkerSpawn"),
                Is.False, "Voidwalker is unavailable below 35 players.");

            ticker.ClearGameRules();
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TestChimeraSaltWeightsAndRoundstartRule()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true
        });
        var server = pair.Server;
        await server.WaitIdleAsync();
        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var primary = prototypes.Index<WeightedRandomPrototype>("SecretSaltPrimary");
            var additional = prototypes.Index<WeightedRandomPrototype>("SecretSaltRoundstart");
            Assert.That(primary.Weights["Letoferol"], Is.EqualTo(0.4f));
            Assert.That(additional.Weights["Letoferol"], Is.EqualTo(0.4f));

            var ticker = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<GameTicker>();
            var entityManager = server.ResolveDependency<IEntityManager>();
            var rule = ticker.AddGameRule("Letoferol");
            Assert.That(entityManager.HasComponent<AntagSelectionComponent>(rule), Is.True,
                "Chimera must have real antagonist selection.");
            Assert.That(entityManager.GetComponent<GameRuleComponent>(rule).MinPlayers, Is.EqualTo(20));
            ticker.ClearGameRules();
        });
        await pair.CleanReturnAsync();
    }

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
    [Test]
    public async Task TestSummaryOnlyGeneratedAtRoundEnd()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true,
            DummyTicker = false,
            Connected = true,
            InLobby = true
        });

        var server = pair.Server;
        await server.WaitIdleAsync();

        await server.WaitAssertion(() => server.CfgMan.SetCVar(CCVars.GridFill, true));

        await pair.WaitClientCommand("toggleready True");
        await pair.WaitCommand("setgamepreset SecretSalt 9999");
        await pair.WaitCommand("startround");
        await pair.RunTicksSync(10);

        await server.WaitAssertion(() =>
        {
            var entMan = server.ResolveDependency<IEntityManager>();
            var ticker = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<GameTicker>();
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));

            var saltRule = ticker.GetAddedGameRules()
                .Single(rule => entMan.HasComponent<SecretSaltPlusComponent>(rule));
            var salt = entMan.GetComponent<SecretSaltPlusComponent>(saltRule);

            Assert.That(salt.TelemetryLastRoundSummary, Is.Null,
                "Round telemetry must not be published before the round ends.");
            Assert.That(salt.TelemetryAuthorReport, Is.Null,
                "Private nickname and role roster must not be generated before round end.");

            // Test-only forced tick: component write access is restricted to its
            // owning system, so use reflection here instead of exposing a public
            // gameplay API or relaxing production component access rules.
            var nextEventField = typeof(SecretSaltPlusComponent).GetField(
                nameof(SecretSaltPlusComponent.TimeNextEvent));
            Assert.That(nextEventField, Is.Not.Null);
            nextEventField!.SetValue(salt, TimeSpan.FromTicks(1));
        });

        await pair.RunTicksSync(2);

        await server.WaitAssertion(() =>
        {
            var entMan = server.ResolveDependency<IEntityManager>();
            var ticker = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<GameTicker>();
            var saltRule = ticker.GetAddedGameRules()
                .Single(rule => entMan.HasComponent<SecretSaltPlusComponent>(rule));
            var salt = entMan.GetComponent<SecretSaltPlusComponent>(saltRule);

            Assert.That(salt.TelemetryMidroundRolls, Is.GreaterThanOrEqualTo(1));
            Assert.That(salt.TelemetryMidroundRolls,
                Is.EqualTo(salt.TelemetryMidroundCandidates + salt.TelemetryMidroundNoCandidates));
            Assert.That(salt.TelemetryMidroundAdded,
                Is.LessThanOrEqualTo(salt.TelemetryMidroundCandidates));

            ticker.EndRound();

            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.PostRound));
            Assert.That(salt.TelemetryLastRoundSummary, Is.Not.Null);
            Assert.That(salt.TelemetryLastRoundSummary, Does.Contain("ROUND SUMMARY round="));
            Assert.That(salt.TelemetryAuthorReport, Is.Not.Null);
            Assert.That(salt.TelemetryAuthorReport, Does.Contain("Секрет Сіль 1.4"));
            Assert.That(salt.TelemetryAuthorReport, Does.Contain("Антагоністи наприкінці раунду"));
            Assert.That(salt.TelemetryAuthorReport, Does.Contain("ROUND SUMMARY round="));
            Assert.That(salt.TelemetryAuthorReportSendRequested, Is.False,
                "Private Discord delivery must be opt-in, with no webhook by default.");
            Assert.That(salt.TelemetryLastRoundSummary, Does.Contain($"roundstartAttempts={salt.TelemetryRoundstartAttempts}"));
            Assert.That(salt.TelemetryLastRoundSummary, Does.Contain($"roundstartAdded={salt.TelemetryRoundstartAdded}"));
            Assert.That(salt.TelemetryLastRoundSummary, Does.Contain("roundstartSkips=["));
            Assert.That(salt.TelemetryLastRoundSummary, Does.Contain($"trackedUniqueAssignedMinds={salt.TelemetryUniqueTrackedAssignedMinds}"));
            Assert.That(salt.TelemetryLastRoundSummary, Does.Contain($"antagonistMindsAtEnd={salt.TelemetryAntagonistMindsAtEnd}"));
            Assert.That(salt.TelemetryLastRoundSummary, Does.Contain($"antagonistRolesAtEnd={salt.TelemetryAntagonistRolesAtEnd}"));
            Assert.That(salt.TelemetryAntagonistRolesAtEnd, Is.GreaterThanOrEqualTo(salt.TelemetryAntagonistMindsAtEnd));
            Assert.That(salt.TelemetryUniqueTrackedAssignedMinds,
                Is.LessThanOrEqualTo(salt.TelemetryRules
                    .Where(rule => entMan.HasComponent<Content.Server.Antag.Components.AntagSelectionComponent>(rule.Key))
                    .Sum(rule => entMan.GetComponent<Content.Server.Antag.Components.AntagSelectionComponent>(rule.Key).AssignedMinds.Count)));
            Assert.That(salt.TelemetryLastRoundSummary, Does.Contain($"initialPlayers={salt.TelemetryInitialPlayers}"));
            Assert.That(salt.TelemetryLastRoundSummary, Does.Contain($"initialChaos={salt.TelemetryInitialChaos}"));
            Assert.That(salt.TelemetryLastRoundSummary, Does.Contain($"midroundRolls={salt.TelemetryMidroundRolls}"));
            Assert.That(salt.TelemetryLastRoundSummary, Does.Contain($"midroundCandidates={salt.TelemetryMidroundCandidates}"));
            Assert.That(salt.TelemetryLastRoundSummary, Does.Contain($"midroundAdded={salt.TelemetryMidroundAdded}"));
            Assert.That(salt.TelemetryLastRoundSummary, Does.Contain($"midroundNoCandidates={salt.TelemetryMidroundNoCandidates}"));

            ticker.RestartRound();
            server.CfgMan.SetCVar(CCVars.GridFill, CCVars.GridFill.DefaultValue);
        });

        await pair.CleanReturnAsync();
    }

}
