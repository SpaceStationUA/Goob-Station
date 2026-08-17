// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.Defibrillator;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Medical;
using Content.Shared.Tag;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Pirate.Medical;

/// <summary>
/// Checks that the belt defibrillators inherit their slots and feature components correctly,
/// so lathe-printed and locker-spawned belts behave as designed.
/// </summary>
[TestFixture]
public sealed class DefibrillatorTest
{
    [Test]
    public async Task BeltDefibrillatorComponentsAndSlotsAreConsistent()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var mapData = await pair.CreateTestMap();

        var entMan = server.EntMan;
        var itemSlots = server.System<ItemSlotsSystem>();
        var tagSystem = server.System<TagSystem>();

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                // The standard belt (techfab product) must carry both slots, a starting cell and paddles,
                // and every feature component needed for the detachable-paddles gameplay.
                var compact = entMan.SpawnEntity("DefibrillatorBeltCompact", mapData.GridCoords);
                Assert.That(itemSlots.TryGetSlot(compact, "cell_slot", out var compactCell), "Compact belt lost its cell slot");
                Assert.That(itemSlots.TryGetSlot(compact, "paddles_slot", out var compactPaddles), "Compact belt lost its paddles slot");
                Assert.That(compactCell!.Item, Is.Not.Null, "Compact belt should spawn with a power cell");
                Assert.That(compactPaddles!.Item is { } compactPaddleItem
                    && entMan.HasComponent<DefibrillatorPaddlesComponent>(compactPaddleItem),
                    "Compact belt should spawn with paddles");
                Assert.That(compactCell.Blacklist?.Components, Does.Contain("BatterySelfRecharger"),
                    "Compact belt should blacklist self-recharging cells");
                Assert.That(entMan.HasComponent<DefibrillatorComponent>(compact), "Belt must have Defibrillator component");
                Assert.That(entMan.HasComponent<DefibrillatorEmagComponent>(compact), "Belt must be emaggable");
                Assert.That(entMan.HasComponent<DefibrillatorChargeVisualsComponent>(compact), "Belt must show charge visuals");
                Assert.That(entMan.HasComponent<DefibrillatorHideInHandComponent>(compact), "Belt must be hidden in hand");
                Assert.That(entMan.HasComponent<DefibrillatorSelfDrainComponent>(compact), "Standard belt must self-drain");
                Assert.That(entMan.HasComponent<DefibrillatorSelfRechargeComponent>(compact), Is.False, "Standard belt must not self-recharge");
                entMan.DeleteEntity(compact);

                // The Empty variant is what the techfab actually prints: it must still have both slots
                // (so paddles can be stored and ejected) but spawn without a power cell.
                var compactEmpty = entMan.SpawnEntity("DefibrillatorBeltCompactEmpty", mapData.GridCoords);
                Assert.That(itemSlots.TryGetSlot(compactEmpty, "cell_slot", out var emptyCell), "Printed belt lost its cell slot");
                Assert.That(itemSlots.TryGetSlot(compactEmpty, "paddles_slot", out _), "Printed belt lost its paddles slot");
                Assert.That(emptyCell!.Item, Is.Null, "Printed belt must spawn without a cell");
                entMan.DeleteEntity(compactEmpty);

                // Premium CMO belt: self-recharging, spawns with its own paddles.
                var cmo = entMan.SpawnEntity("DefibrillatorBeltCMO", mapData.GridCoords);
                Assert.That(itemSlots.TryGetSlot(cmo, "paddles_slot", out var cmoPaddles)
                    && cmoPaddles!.Item is { }, "CMO belt should spawn with paddles");
                Assert.That(entMan.HasComponent<DefibrillatorSelfRechargeComponent>(cmo), "CMO belt must self-recharge");
                Assert.That(entMan.HasComponent<DefibrillatorSelfDrainComponent>(cmo), Is.False, "CMO belt must not keep the standard self-drain");
                entMan.DeleteEntity(cmo);

                // Combat belt: syndicate contraband, must be emag-immune.
                var combat = entMan.SpawnEntity("DefibrillatorBeltCombat", mapData.GridCoords);
                Assert.That(tagSystem.HasTag(combat, "EmagImmune"), "Combat belt must be emag-immune");
                entMan.DeleteEntity(combat);
            });
        });

        await pair.CleanReturnAsync();
    }
}
