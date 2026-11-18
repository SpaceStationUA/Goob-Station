// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.Bots;
using Content.Server.NPC.HTN.PrimitiveTasks.Operators.Specific;
using Content.Server.Storage.Components;
using Content.Shared.Emag.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Sound.Components;
using Robust.Shared.Audio.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Pirate.Silicons;

[TestFixture]
public sealed class AnnoyingDuckBotTest
{
    [Test]
    public async Task EmagReschedulesQuacksAndChaseEventPlaysOneSound()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entities = server.EntMan;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var duck = entities.SpawnEntity("MobAnnoyingDuckBot", map.GridCoords);
            var user = entities.SpawnEntity("MobHuman", map.GridCoords);
            var emag = entities.SpawnEntity("Emag", map.GridCoords);
            var emagSystem = server.System<EmagSystem>();
            var spam = entities.GetComponent<SpamEmitSoundComponent>(duck);
            var now = server.Timing.CurTime;
            spam.NextSound = now + TimeSpan.FromSeconds(15);

            Assert.That(emagSystem.TryEmagEffect(emag, user, duck), Is.True);
            var nextSound = spam.NextSound;
            Assert.That(nextSound - now, Is.InRange(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(6)));
            Assert.That(emagSystem.TryEmagEffect(emag, user, duck), Is.False);
            Assert.That(spam.NextSound, Is.EqualTo(nextSound));

            var sounds = entities.Count<AudioComponent>();
            entities.EventBus.RaiseLocalEvent(duck, new HTNRaisedEvent(duck, user, new AnnoyingDuckBotHonkEvent()));
            Assert.That(entities.Count<AudioComponent>(), Is.EqualTo(sounds + 1));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task UseInHandUnwrapsDuckEgg()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entities = server.EntMan;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var user = entities.SpawnEntity("MobHuman", map.GridCoords);
            var egg = entities.SpawnEntity("FoodEggAnnoyingDuck", map.GridCoords);
            Assert.That(server.System<SharedHandsSystem>().TryPickupAnyHand(user, egg), Is.True);

            var use = new UseInHandEvent(user);
            entities.EventBus.RaiseLocalEvent(egg, use);
            Assert.That(use.Handled, Is.True);
            Assert.That(entities.GetComponent<SpawnItemsOnUseComponent>(egg).Uses, Is.Zero);
            Assert.That(entities.IsQueuedForDeletion(egg), Is.True);
        });

        await pair.CleanReturnAsync();
    }
}
