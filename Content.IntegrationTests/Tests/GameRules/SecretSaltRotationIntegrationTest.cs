// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.GameTicking;
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
            DummyTicker = false,
            Connected = true,
            InLobby = true
        });

        var server = pair.Server;
        await server.WaitIdleAsync();
        await server.WaitAssertion(() => server.CfgMan.SetCVar(CCVars.GridFill, true));

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
            ticker.SetGamePreset("SecretSalt");
            Assert.That(ticker.Preset?.ID, Is.EqualTo("Secret"));
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

            ticker.EndRound();
            ticker.RestartRound();
            server.CfgMan.SetCVar(CCVars.GridFill, CCVars.GridFill.DefaultValue);
        });

        await pair.CleanReturnAsync();
    }
}
