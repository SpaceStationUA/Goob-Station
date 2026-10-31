using Content.Server.Atmos.Components;
using Content.Shared.Inventory;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Pirate.StarlightSpecies;

[TestFixture]
public sealed class NeocyteInventoryTest
{
    [Test]
    public async Task FrameDoesNotWeakenSuitPressureProtection()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true, Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var inventory = em.System<InventorySystem>();
            var neocyte = em.Spawn("MobNeocyte", map.MapCoords);
            inventory.TryUnequip(neocyte, "outerClothing2", silent: true, force: true);
            var helmet = em.Spawn("ClothingHeadHelmetEVA", map.MapCoords);
            var suit = em.Spawn("ClothingOuterHardsuitEVA", map.MapCoords);
            var frame = em.Spawn("ClothingOuterArmorNeocyteHeavySpace", map.MapCoords);
            Assert.That(inventory.TryEquip(neocyte, helmet, "head", silent: true, force: true), Is.True);
            Assert.That(inventory.TryEquip(neocyte, suit, "outerClothing", silent: true, force: true), Is.True);
            var pressure = em.GetComponent<BarotraumaComponent>(neocyte);
            var suitHigh = pressure.HighPressureMultiplier;
            var suitLow = pressure.LowPressureMultiplier;
            Assert.That(suitHigh, Is.LessThan(1f));
            Assert.That(suitLow, Is.GreaterThan(1f));

            Assert.That(inventory.TryEquip(neocyte, frame, "outerClothing2", silent: true, force: true), Is.True);
            Assert.That(pressure.HighPressureMultiplier, Is.LessThanOrEqualTo(suitHigh));
            Assert.That(pressure.LowPressureMultiplier, Is.GreaterThanOrEqualTo(suitLow));

            Assert.That(inventory.TryUnequip(neocyte, "outerClothing", silent: true, force: true), Is.True);
            Assert.That(pressure.HighPressureMultiplier, Is.LessThan(1f));
            Assert.That(pressure.LowPressureMultiplier, Is.GreaterThan(1f));

            Assert.That(inventory.TryUnequip(neocyte, "head", silent: true, force: true), Is.True);
            Assert.That(pressure.HighPressureMultiplier, Is.EqualTo(1f));
            Assert.That(pressure.LowPressureMultiplier, Is.EqualTo(1f));
            em.DeleteEntity(neocyte);
            em.DeleteEntity(helmet);
            em.DeleteEntity(suit);
            em.DeleteEntity(frame);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SuitStorageRemainsUntilBothSupportingSlotsAreEmpty()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true, Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var inventory = em.System<InventorySystem>();
            var neocyte = em.Spawn("MobNeocyte", map.MapCoords);
            inventory.TryUnequip(neocyte, "outerClothing2", silent: true, force: true);
            var frame = em.Spawn("ClothingOuterArmorNeocyteHeavySpace", map.MapCoords);
            var suit = em.Spawn("ClothingOuterHardsuitEVA", map.MapCoords);
            var tank = em.Spawn("OxygenTankFilled", map.MapCoords);
            Assert.That(inventory.TryEquip(neocyte, frame, "outerClothing2", silent: true, force: true), Is.True);
            Assert.That(inventory.TryEquip(neocyte, suit, "outerClothing", silent: true, force: true), Is.True);
            Assert.That(inventory.TryEquip(neocyte, tank, "suitstorage", silent: true), Is.True);

            Assert.That(inventory.TryUnequip(neocyte, "outerClothing", silent: true, force: true), Is.True);
            Assert.That(inventory.TryGetSlotEntity(neocyte, "suitstorage", out var stored), Is.True);
            Assert.That(stored, Is.EqualTo(tank));

            Assert.That(inventory.TryUnequip(neocyte, "outerClothing2", silent: true, force: true), Is.True);
            Assert.That(inventory.TryGetSlotEntity(neocyte, "suitstorage", out _), Is.False);
            em.DeleteEntity(neocyte);
            em.DeleteEntity(frame);
            em.DeleteEntity(suit);
            em.DeleteEntity(tank);
        });
        await pair.CleanReturnAsync();
    }
}
