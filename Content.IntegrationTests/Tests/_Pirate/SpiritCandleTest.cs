// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Goobstation.Shared.Wraith.SpiritCandle;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Storage;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Pirate;

[TestFixture]
public sealed class SpiritCandleTest
{
    [TestCase("hand")]
    [TestCase("backpack")]
    [TestCase("floor")]
    public async Task ExtinguishAndBurnOutInPlace(string location)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var em = server.EntMan;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var candle = em.SpawnEntity("SpiritCandle", map.GridCoords);
            var containers = server.System<SharedContainerSystem>();
            if (location == "hand")
            {
                var holder = em.SpawnEntity("MobHuman", map.GridCoords);
                Assert.That(server.System<SharedHandsSystem>().TryPickupAnyHand(holder, candle), Is.True);
            }
            else if (location == "backpack")
            {
                var backpack = em.SpawnEntity("ClothingBackpack", map.GridCoords);
                Assert.That(containers.Insert(candle, em.GetComponent<StorageComponent>(backpack).Container), Is.True);
            }

            containers.TryGetContainingContainer(candle, out var original);
            Assert.That(original == null, Is.EqualTo(location == "floor"));
            var fire = server.System<FlammableSystem>();
            fire.Ignite(candle);
            Assert.That(em.GetComponent<SpiritCandleComponent>(candle).Active, Is.True);
            fire.Extinguish(candle);
            Assert.That(em.GetComponent<SpiritCandleComponent>(candle).Active, Is.False);
            containers.TryGetContainingContainer(candle, out var afterExtinguish);
            Assert.That(afterExtinguish, Is.SameAs(original));

            fire.Ignite(candle);
            var heat = new DamageSpecifier(server.ProtoMan.Index<DamageTypePrototype>("Heat"), 60);
            server.System<DamageableSystem>().TryChangeDamage(candle, heat, ignoreResistances: true);
            var skull = em.EntityQuery<MetaDataComponent>().Single(m => m.EntityPrototype?.ID == "HeadSkeleton").Owner;
            containers.TryGetContainingContainer(skull, out var afterBurnout);
            Assert.That(afterBurnout, Is.SameAs(original));
            if (original == null)
                Assert.That(em.GetComponent<TransformComponent>(skull).Coordinates, Is.EqualTo(map.GridCoords));
        });
        await pair.CleanReturnAsync();
    }
}
