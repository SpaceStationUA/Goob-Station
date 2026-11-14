using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Stacks;

using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Pirate.Power;

public sealed class PlasmaCutterFuelTest : InteractionTest
{
    [TestCase("WeaponPlasmaCutterEmpty", 1500)]
    [TestCase("WeaponSuperPlasmaCutterEmpty", 1000)]
    public async Task RawPlasmaChargesAndConsumesWholePieces(string cutter, int capacity)
    {
        await SpawnTarget(cutter);
        await InteractUsing("PlasmaOre1");
        await Server.WaitAssertion(() =>
        {
            Assert.That(Charge(), Is.EqualTo(500));
            Assert.That(HandSys.GetActiveItem((SPlayer, Hands)), Is.Null);
            SEntMan.System<SharedBatterySystem>().SetCharge(STarget!.Value, capacity - 40);
        });

        // Even a top-up smaller than one ore unit must consume one whole piece.
        await InteractUsing("PlasmaOre", quantity: 2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Charge(), Is.EqualTo(capacity));
            var held = HandSys.GetActiveItem((SPlayer, Hands));
            Assert.That(held, Is.Not.Null);
            Assert.That(SEntMan.GetComponent<StackComponent>(held!.Value).Count, Is.EqualTo(1));
        });

        // A full cutter must not consume the remaining ore.
        await Interact();
        await Server.WaitAssertion(() =>
        {
            var held = HandSys.GetActiveItem((SPlayer, Hands));
            Assert.That(held, Is.Not.Null);
            Assert.That(SEntMan.GetComponent<StackComponent>(held!.Value).Count, Is.EqualTo(1));
        });
    }

    private float Charge()
    {
        var target = STarget!.Value;
        return SEntMan.System<SharedBatterySystem>().GetCharge((target, SEntMan.GetComponent<BatteryComponent>(target)));
    }
}
