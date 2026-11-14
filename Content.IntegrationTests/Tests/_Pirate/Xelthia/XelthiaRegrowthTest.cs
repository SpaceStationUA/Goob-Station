using System.Linq;
using Content.Goobstation.Maths.FixedPoint;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._Pirate.Xelthia;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Inventory;

using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Pirate.Xelthia;

public sealed class XelthiaRegrowthTest : InteractionTest
{
    [Test]
    public async Task MissingArmRegrowsWithoutLosingDigestiveOrgans()
    {
        await SpawnTarget("MobXelthia");

        await Server.WaitAssertion(() =>
        {
            var mob = STarget!.Value;
            var body = SEntMan.System<SharedBodySystem>();
            Assert.That(body.GetBodyOrganEntityComps<StomachComponent>(mob), Has.Count.EqualTo(1));
            Assert.That(SEntMan.System<InventorySystem>().TryGetSlot(mob, "gloves", out _), Is.False);
            Assert.That(SEntMan.GetComponent<XelthiaComponent>(mob).UseDelay, Is.EqualTo(TimeSpan.FromSeconds(150)));

            var arm = body.GetBodyChildrenOfType(mob, BodyPartType.Arm).First().Id;
            SEntMan.DeleteEntity(arm);
            Assert.That(body.GetBodyChildrenOfType(mob, BodyPartType.Arm).Count(), Is.EqualTo(1));

            SEntMan.EventBus.RaiseLocalEvent(mob, new ArmRegrowthEvent { Performer = mob });
            var solutions = SEntMan.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryGetSolution(mob, BloodstreamComponent.DefaultBloodSolutionName, out _, out var blood), Is.True);
            Assert.That(blood!.GetReagentQuantity(new ReagentId("XelthiaArmJuice", null)), Is.EqualTo(FixedPoint2.New(3.5)));
        });

        await RunTicks(2);
        await Server.WaitAssertion(() =>
        {
            var body = SEntMan.System<SharedBodySystem>();
            var mob = STarget!.Value;
            Assert.That(body.GetBodyChildrenOfType(mob, BodyPartType.Arm).Count(), Is.EqualTo(2));
            Assert.That(body.GetBodyChildrenOfType(mob, BodyPartType.Hand).Count(), Is.EqualTo(2));
            Assert.That(body.GetBodyOrganEntityComps<StomachComponent>(mob), Has.Count.EqualTo(1));
        });
    }

    [TearDown]
    public async Task DeleteBodyBeforeMap()
    {
        await Server.WaitPost(() =>
        {
            if (STarget is { } mob && !SEntMan.Deleted(mob))
                SEntMan.DeleteEntity(mob);
        });
        await RunTicks(2);
    }
}
