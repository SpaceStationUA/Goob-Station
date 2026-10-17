// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.IntegrationTests.Tests.Movement;
using Content.Shared.Damage.Components;
using Content.Shared.Inventory;
using Content.Shared.Stunnable;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Pirate.StepTrigger;

public sealed class RevolutionaryBootsTest : MovementTest
{
    [TestCase("ClothingShoesBootsRevSteel", true, true)]
    [TestCase("ClothingShoesBootsRev", true, false)]
    [TestCase("ClothingShoesBootsRevSteel", false, false)]
    public async Task WorkbootsProtectAgainstCaltropsOnlyWhenReinforcedAndWorn(
        string bootsPrototype,
        bool worn,
        bool protectedFromCaltrops)
    {
        await SpawnTarget("Caltrops");

        if (worn)
        {
            await Server.WaitAssertion(() =>
            {
                var boots = SEntMan.SpawnEntity(bootsPrototype, SEntMan.GetCoordinates(PlayerCoords));
                Assert.That(Server.System<InventorySystem>().TryEquip(SPlayer, boots, "shoes", force: true), Is.True);
            });
        }
        else
        {
            await PlaceInHands(bootsPrototype);
        }

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<DamageableComponent>(SPlayer).TotalDamage.Float(), Is.Zero);
        });

        await Move(DirectionFlag.East, 1f);

        await Server.WaitAssertion(() =>
        {
            var damage = SEntMan.GetComponent<DamageableComponent>(SPlayer).TotalDamage.Float();
            if (protectedFromCaltrops)
            {
                Assert.That(Delta(), Is.LessThan(0), "The wearer must actually cross the caltrops.");
                Assert.That(damage, Is.Zero, "Steel-toed workboots must prevent caltrop damage.");
            }
            else
            {
                Assert.That(damage, Is.GreaterThan(0), "Ordinary or unworn workboots must not protect against caltrops.");
            }

            Assert.That(SEntMan.HasComponent<KnockedDownComponent>(SPlayer), Is.EqualTo(!protectedFromCaltrops));
        });
    }
}
