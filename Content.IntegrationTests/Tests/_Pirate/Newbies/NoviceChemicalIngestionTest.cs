// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Goobstation.Maths.FixedPoint;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server.Players.PlayTimeTracking;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Nutrition.EntitySystems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Pirate.Newbies;

public sealed class NoviceChemicalIngestionTest : InteractionTest
{
    protected override string PlayerPrototype => "MobHuman";

    [TestCase("Sodium", 0, false, false)]
    [TestCase("Potassium", 23.99, false, false)]
    [TestCase("Sodium", 0, true, false)]
    [TestCase("Potassium", 0, true, false)]
    [TestCase("Sodium", 24, false, true)]
    [TestCase("Potassium", 24, false, true)]
    [TestCase("Potassium", 24, true, true)]
    [TestCase("Water", 0, false, true)]
    [TestCase("Saline", 0, false, true)]
    public async Task DrinkingAndForceFeedingRespectTheActorsPlaytime(
        string reagent, double hours, bool forceFed, bool allowed)
    {
        await SetPlaytime(hours);
        var cup = ToServer(await PlaceInHands("Bucket"));
        Entity<SolutionComponent> source = default;
        EntityUid target = SPlayer;

        await Server.WaitAssertion(() =>
        {
            source = Fill(cup, reagent, 5);
            if (forceFed)
                target = SEntMan.SpawnEntity("MobHuman", SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates);

            Assert.That(Server.System<IngestionSystem>().TryIngest(SPlayer, target, cup), Is.EqualTo(allowed));
            Assert.That(source.Comp.Solution.Volume, Is.EqualTo(FixedPoint2.New(5)));
        });

        await AwaitDoAfters();
        await Server.WaitAssertion(() =>
        {
            Assert.That(source.Comp.Solution.Volume, Is.EqualTo(FixedPoint2.New(allowed ? 0 : 5)));
            if (forceFed)
                SEntMan.DeleteEntity(target);
        });
    }

    [TestCase("Sodium", false)]
    [TestCase("Potassium", false)]
    [TestCase("Sodium", true)]
    [TestCase("Potassium", true)]
    public async Task WaterCannotReactWithMetalAlreadyInsideTheBody(string metal, bool inBloodstream)
    {
        await SetPlaytime(0);
        var cup = ToServer(await PlaceInHands("Bucket"));

        await Server.WaitAssertion(() =>
        {
            var source = Fill(cup, "Water", 10);
            var destination = BodySolution(inBloodstream);
            var solutions = Server.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryAddReagent(destination, metal, FixedPoint2.New(5)), Is.True);
            var before = destination.Comp.Solution.Volume;

            Assert.That(Server.System<IngestionSystem>().TryIngest(SPlayer, cup), Is.False);
            Assert.That(source.Comp.Solution.Volume, Is.EqualTo(FixedPoint2.New(10)));
            Assert.That(destination.Comp.Solution.Volume, Is.EqualTo(before));
            Assert.That(destination.Comp.Solution.GetTotalPrototypeQuantity(metal), Is.EqualTo(FixedPoint2.New(5)));
        });
    }

    [TestCase("Sodium")]
    [TestCase("Potassium")]
    public async Task StomachChangesDuringDrinkingAreCheckedBeforeConsumingWater(string metal)
    {
        await SetPlaytime(0);
        var cup = ToServer(await PlaceInHands("Bucket"));
        Entity<SolutionComponent> source = default;
        Entity<SolutionComponent> stomach = default;

        await Server.WaitAssertion(() =>
        {
            source = Fill(cup, "Water", 10);
            Assert.That(Server.System<IngestionSystem>().TryIngest(SPlayer, cup), Is.True);
            stomach = BodySolution(false);
            Assert.That(Server.System<SharedSolutionContainerSystem>().TryAddReagent(stomach, metal, FixedPoint2.New(5)), Is.True);
        });

        await AwaitDoAfters();
        await Server.WaitAssertion(() =>
        {
            Assert.That(source.Comp.Solution.Volume, Is.EqualTo(FixedPoint2.New(10)));
            Assert.That(stomach.Comp.Solution.GetTotalPrototypeQuantity(metal), Is.EqualTo(FixedPoint2.New(5)));
            Assert.That(stomach.Comp.Solution.GetTotalPrototypeQuantity("Water"), Is.EqualTo(FixedPoint2.Zero));
        });
    }

    private Entity<SolutionComponent> BodySolution(bool bloodstream)
    {
        Assert.That(Server.System<SharedBodySystem>()
            .TryGetBodyOrganEntityComps<StomachComponent>(SPlayer, out var stomachs), Is.True);
        var stomach = stomachs!.First();
        Assert.That(Server.System<SharedSolutionContainerSystem>().TryGetSolution(
            bloodstream ? SPlayer : stomach.Owner,
            bloodstream ? stomach.Comp1.BodySolutionName : StomachSystem.DefaultSolutionName,
            out var solution, out _), Is.True);
        return solution!.Value;
    }

    private Entity<SolutionComponent> Fill(EntityUid container, string reagent, int amount)
    {
        var solutions = Server.System<SharedSolutionContainerSystem>();
        Assert.That(solutions.TryGetDrainableSolution(container, out var solution, out _), Is.True);
        solutions.RemoveAllSolution(solution!.Value);
        Assert.That(solutions.TryAddReagent(solution.Value, reagent, FixedPoint2.New(amount)), Is.True);
        return solution.Value;
    }

    private Task SetPlaytime(double hours) => Server.WaitPost(() =>
        NoviceTestPlaytime.Set(Server.ResolveDependency<PlayTimeTrackingManager>(), ServerSession, hours));
}
