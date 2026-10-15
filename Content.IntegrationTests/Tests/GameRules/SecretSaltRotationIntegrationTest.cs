// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.GameTicking;
using Content.Server._Pirate.RoundEnd.PhotoAlbum;
using System.Collections.Generic;
using Content.Shared.CCVar;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.GameRules;

/// <summary>
/// Real ticker transitions: selecting a preset in the lobby is not counted,
/// and after two played Salt rounds the ticker substitutes Secret automatically.
/// </summary>
[TestFixture]
[Category("GameRuleTests")]
public sealed class SecretSaltRotationIntegrationTest
{
    [Test]
    public async Task TestTwoPlayedSaltRoundsPreventThirdAndDifferentRoundResets()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true,
            Fresh = true,
            Destructive = true,
            AsynchronousDatabase = true,
            DummyTicker = false,
            Connected = true,
            InLobby = true
        });

        var server = pair.Server;
        await server.WaitIdleAsync();
        await server.WaitAssertion(() =>
        {
            Assert.That(server.CfgMan.GetCVar(CCVars.DatabaseSynchronous), Is.False,
                "This test must queue unrelated background SQLite operations.");
            server.CfgMan.SetCVar(CCVars.GridFill, true);
            // Reproduce the server where Salt is the default preset:
            // the one-round Secret substitution must return to Salt.
            server.CfgMan.SetCVar(CCVars.GameLobbyDefaultPreset, "SecretSalt");

            // We test preset rotation, not autonomous NPC food evaluation.
            // Random NPC spawns during game rounds can otherwise fail in
            // NPCUtilitySystem.TotalFoodUtilityConditioned and make this
            // unrelated integration test flaky. This is test-only.
            server.CfgMan.SetCVar(CCVars.NPCEnabled, false);
        });

        for (var count = 1; count <= 2; count++)
        {
            await pair.WaitClientCommand("toggleready True");
            await pair.WaitCommand("setgamepreset SecretSalt 9999");
            await pair.WaitCommand("startround");
            await pair.RunTicksSync(10);

            var expected = count;
            await server.WaitAssertion(() =>
            {
                var systems = server.ResolveDependency<IEntitySystemManager>();
                var ticker = systems.GetEntitySystem<GameTicker>();
                var rotation = systems.GetEntitySystem<SecretSaltRotationSystem>();
                Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));
                Assert.That(ticker.CurrentPreset?.ID, Is.EqualTo("SecretSalt"));
                Assert.That(rotation.ConsecutiveRounds, Is.EqualTo(expected));
                Assert.That(rotation.IsBlocked, Is.EqualTo(expected == 2));
                // Station albums are unrelated to the rotation assertions.
                // Do not kick off asynchronous album writes while the upstream
                // LinkAccount system is refreshing patrons during rapid test-only
                // EndRound/RestartRound transitions. No production gameplay change.
                RemoveTestPhotoAlbumPersistence(server.ResolveDependency<IEntityManager>());
                ticker.EndRound();
                ticker.RestartRound();
            });
            await pair.RunTicksSync(3);
        }

        await server.WaitAssertion(() =>
        {
            var systems = server.ResolveDependency<IEntitySystemManager>();
            var ticker = systems.GetEntitySystem<GameTicker>();
            var rotation = systems.GetEntitySystem<SecretSaltRotationSystem>();
            Assert.That(rotation.IsBlocked, Is.True);

            // A command or a stale vote winner selecting Salt must be replaced
            // with a valid different preset, not a third Salt start.
            // An explicitly requested long resetDelay must not make the
            // substitute Secret preset permanent.
            ticker.SetGamePreset("SecretSalt", resetDelay: 9999);
            Assert.That(ticker.Preset?.ID, Is.EqualTo("Secret"));
            Assert.That(ticker.ResetCountdown, Is.EqualTo(0));
            Assert.That(rotation.ConsecutiveRounds, Is.EqualTo(2));
        });

        await pair.WaitClientCommand("toggleready True");
        await pair.WaitCommand("startround");
        await pair.RunTicksSync(10);

        await server.WaitAssertion(() =>
        {
            var systems = server.ResolveDependency<IEntitySystemManager>();
            var ticker = systems.GetEntitySystem<GameTicker>();
            var rotation = systems.GetEntitySystem<SecretSaltRotationSystem>();
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));
            Assert.That(ticker.CurrentPreset?.ID, Is.EqualTo("Secret"));
            Assert.That(rotation.ConsecutiveRounds, Is.Zero,
                "Only a real different played round clears the block.");
            Assert.That(rotation.IsBlocked, Is.False);

            RemoveTestPhotoAlbumPersistence(server.ResolveDependency<IEntityManager>());
            ticker.EndRound();
            ticker.RestartRound();

            // The actual played non-Salt round cleared rotation. Its
            // temporary fallback must now restore the configured default.
            Assert.That(ticker.Preset?.ID, Is.EqualTo("SecretSalt"));
            Assert.That(ticker.ResetCountdown, Is.Null);

            server.CfgMan.SetCVar(CCVars.GameLobbyDefaultPreset, CCVars.GameLobbyDefaultPreset.DefaultValue);
            server.CfgMan.SetCVar(CCVars.GridFill, CCVars.GridFill.DefaultValue);
            server.CfgMan.SetCVar(CCVars.NPCEnabled, CCVars.NPCEnabled.DefaultValue);
        });

        await pair.CleanReturnAsync();
        // Non-reusable pairs skip the pool's automatic log check on disposal,
        // so explicitly retain the same error-log assertions.
        Assert.That(pair.ServerLogHandler.FailingLogs, Is.Empty);
        Assert.That(pair.ClientLogHandler.FailingLogs, Is.Empty);
    }

    /// <summary>
    /// The integration test asserts round transitions, not persistence.
    /// Remove persistence only from disposable test entities so unrelated
    /// photo-album background database writes cannot race LinkAccount refresh.
    /// </summary>
    private static void RemoveTestPhotoAlbumPersistence(IEntityManager entMan)
    {
        var albums = new List<EntityUid>();
        var query = entMan.EntityQueryEnumerator<PersistentPhotoAlbumComponent>();
        while (query.MoveNext(out var uid, out _))
            albums.Add(uid);

        foreach (var uid in albums)
            entMan.RemoveComponent<PersistentPhotoAlbumComponent>(uid);
    }
}
