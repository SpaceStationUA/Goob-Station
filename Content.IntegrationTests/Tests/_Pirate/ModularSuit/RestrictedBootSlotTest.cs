// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Inventory;

namespace Content.IntegrationTests.Tests._Pirate.ModularSuit;

[TestFixture]
public sealed class RestrictedBootSlotTest
{
    [TestCase("MobHarpy")]
    [TestCase("MobDiona")]
    public async Task ModularBootsFitButOrdinaryShoesRemainBlocked(string species)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entities = server.EntMan;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var wearer = entities.SpawnEntity(species, map.GridCoords);
            var inventory = server.System<InventorySystem>();
            var ordinaryShoes = entities.SpawnEntity("ClothingShoesColorBlack", map.GridCoords);
            Assert.That(inventory.TryEquip(wearer, ordinaryShoes, "shoes", silent: true, force: true), Is.False,
                "The hidden foot slot must still reject ordinary footwear, even on a forced equip.");

            foreach (var prototype in new[] { "ClothingModularBootsStandard", "ClothingModsuitBootsStandard" })
            {
                var boots = entities.SpawnEntity(prototype, map.GridCoords);
                // Suit deployment uses a forced equip to bypass manual part-equipping restrictions.
                Assert.That(inventory.TryEquip(wearer, boots, "shoes", silent: true, force: true), Is.True,
                    $"{species} must accept {prototype} when the suit deploys its boots.");
                Assert.That(inventory.TryGetSlotEntity(wearer, "shoes", out var equipped), Is.True);
                Assert.That(equipped, Is.EqualTo(boots));
                Assert.That(inventory.TryUnequip(wearer, "shoes", silent: true, force: true), Is.True);
            }
        });

        await pair.CleanReturnAsync();
    }
}
