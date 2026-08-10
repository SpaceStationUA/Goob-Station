// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared.SSDIndicator;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Player;

namespace Content.IntegrationTests.Tests._Pirate.SSDIndicator;

[TestFixture]
[TestOf(typeof(SSDIndicatorSystem))]
public sealed class SSDIndicatorTests
{
    [Test]
    public async Task ActivePlayerRecoversFromStaleSSDState()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            DummyTicker = false,
            Dirty = true,
        });
        var server = pair.Server;
        var entMan = server.EntMan;
        var statusEffects = server.System<StatusEffectsSystem>();
        var player = server.PlayerMan.Sessions.Single().AttachedEntity!.Value;

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.HasComponent<ActorComponent>(player), Is.True);

            var ssd = entMan.GetComponent<SSDIndicatorComponent>(player);
            ssd.IsSSD = true;

            Assert.That(statusEffects.TryUpdateStatusEffectDuration(
                player,
                SSDIndicatorSystem.StatusEffectSSDSleeping), Is.True);
            Assert.That(statusEffects.HasStatusEffect(
                player,
                SSDIndicatorSystem.StatusEffectSSDSleeping), Is.True);
        });

        await pair.RunTicksSync(2);

        await server.WaitAssertion(() =>
        {
            var ssd = entMan.GetComponent<SSDIndicatorComponent>(player);

            Assert.That(entMan.HasComponent<ActorComponent>(player), Is.True);
            Assert.That(ssd.IsSSD, Is.False);
            Assert.That(ssd.FallAsleepTime, Is.EqualTo(TimeSpan.Zero));
            Assert.That(statusEffects.HasStatusEffect(
                player,
                SSDIndicatorSystem.StatusEffectSSDSleeping), Is.False);
        });

        await pair.CleanReturnAsync();
    }
}
