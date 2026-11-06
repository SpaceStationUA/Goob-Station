// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Maths.FixedPoint;
using Content.Goobstation.Shared.Chemistry;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server.Players.PlayTimeTracking;
using Content.Server.Power.EntitySystems;
using Content.Shared._Pirate.Chemistry;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Prototypes;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Power.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Pirate.Newbies;

public sealed class NoviceChemicalProtectionTest : InteractionTest
{
    [TestCase("Sodium", 0, true)]
    [TestCase("Sodium", 23.99, true)]
    [TestCase("Sodium", 24, false)]
    [TestCase("Sodium", 25, false)]
    [TestCase("Potassium", 0, true)]
    [TestCase("Potassium", 23.99, true)]
    [TestCase("Potassium", 24, false)]
    [TestCase("Potassium", 25, false)]
    public async Task TransfersRespectPlaytimeInBothDirections(string metal, double hours, bool restricted)
    {
        await SetPlaytime(hours);
        await Server.WaitAssertion(() =>
        {
            foreach (var reverse in new[] { false, true })
            {
                // Keep allowed explosions off the test map while exercising real solution reactions.
                var source = SEntMan.SpawnEntity("Bucket", MapCoordinates.Nullspace);
                var target = SEntMan.SpawnEntity("Bucket", MapCoordinates.Nullspace);
                var incoming = Fill(source, reverse ? "Water" : metal, 50);
                var receiving = Fill(target, reverse ? metal : "Water", 200);
                var transferred = Server.System<SolutionTransferSystem>().Transfer(
                    new SolutionTransferData(SPlayer, source, incoming, target, receiving, FixedPoint2.New(50)));

                Assert.That(transferred, Is.EqualTo(FixedPoint2.New(restricted ? 0 : 50)));
                Assert.That(incoming.Comp.Solution.Volume, Is.EqualTo(FixedPoint2.New(restricted ? 50 : 0)));
                Assert.That(receiving.Comp.Solution.Volume, Is.EqualTo(FixedPoint2.New(restricted ? 200 : 150)),
                    "Blocked transfers preserve both solutions; unlocked transfers run the normal reaction.");
                SEntMan.DeleteEntity(source);
                SEntMan.DeleteEntity(target);
            }
        });
    }

    [TestCase("JugSodium")]
    [TestCase("JugPotassium")]
    public async Task PouringAJugIntoAWaterBucketIsBlocked(string jugPrototype)
    {
        await SetPlaytime(0);
        var bucket = ToServer(await SpawnTarget("Bucket"));
        var jug = ToServer(await PlaceInHands(jugPrototype));
        await Server.WaitPost(() => Fill(bucket, "Water", 200));
        await Interact();
        await Server.WaitAssertion(() =>
        {
            Assert.That(Drainable(bucket).Comp.Solution.Volume, Is.EqualTo(FixedPoint2.New(200)));
            Assert.That(Drainable(jug).Comp.Solution.Volume, Is.EqualTo(FixedPoint2.New(200)));
        });
    }

    [TestCase("Sodium", false)]
    [TestCase("Sodium", true)]
    [TestCase("Potassium", false)]
    [TestCase("Potassium", true)]
    public async Task SyringeInjectionAndDrawingCannotBypassProtection(string metal, bool draw)
    {
        await SetPlaytime(0);
        var bucket = ToServer(await SpawnTarget("Bucket"));
        var syringe = ToServer(await PlaceInHands("Syringe"));
        Entity<SolutionComponent> syringeSolution = default;
        await Server.WaitAssertion(() =>
        {
            Fill(bucket, draw ? metal : "Water", 20);
            var injector = SEntMan.GetComponent<InjectorComponent>(syringe);
            var solutions = Server.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryGetSolution(syringe, injector.SolutionName, out var solution, out _), Is.True);
            syringeSolution = solution!.Value;
            Assert.That(solutions.TryAddReagent(syringeSolution, draw ? "Water" : metal, FixedPoint2.New(5)), Is.True);
            Server.System<InjectorSystem>().ToggleMode((syringe, injector), SPlayer,
                Server.ProtoMan.Index<InjectorModePrototype>(draw ? "SyringeDrawMode" : "SyringeInjectMode"));
        });
        await Interact();
        await Server.WaitAssertion(() =>
        {
            Assert.That(Drainable(bucket).Comp.Solution.Volume, Is.EqualTo(FixedPoint2.New(20)));
            Assert.That(syringeSolution.Comp.Solution.Volume, Is.EqualTo(FixedPoint2.New(5)));
        });
    }

    [Test]
    public async Task EnergyDispenserAndSavedRecipeDoNotConsumeChargeWhenBlocked()
    {
        await SetPlaytime(0);
        await Server.WaitAssertion(() =>
        {
            var dispenser = SEntMan.SpawnEntity("EnergyChemDispenser", MapData.GridCoords);
            var beaker = SEntMan.SpawnEntity("LargeBeaker", MapData.GridCoords);
            var solution = Fill(beaker, "Water", 20);
            Assert.That(Server.System<ItemSlotsSystem>().TryInsert(dispenser,
                SharedEnergyReagentDispenser.OutputSlotName, beaker, SPlayer), Is.True);
            var battery = SEntMan.GetComponent<BatteryComponent>(dispenser);
            Server.System<BatterySystem>().SetCharge(dispenser, battery.MaxCharge);
            var charge = battery.LastCharge;

            Send(dispenser, new EnergyReagentDispenserSetDispenseAmountMessage(EnergyReagentDispenserDispenseAmount.U5));
            Send(dispenser, new EnergyReagentDispenserDispenseReagentMessage("Sodium"));
            Assert.That(solution.Comp.Solution.Volume, Is.EqualTo(FixedPoint2.New(20)));
            Assert.That(battery.LastCharge, Is.EqualTo(charge));

            Server.System<SharedSolutionContainerSystem>().RemoveAllSolution(solution);
            Send(dispenser, new ReagentDispenserStartRecipeRecordingMessage());
            Send(dispenser, new EnergyReagentDispenserDispenseReagentMessage("Water"));
            Send(dispenser, new EnergyReagentDispenserDispenseReagentMessage("Potassium"));
            Send(dispenser, new ReagentDispenserSaveRecipeMessage("restricted"));
            Send(dispenser, new ReagentDispenserDispenseRecipeMessage("restricted"));
            Assert.That(solution.Comp.Solution.Volume, Is.EqualTo(FixedPoint2.Zero));
            Assert.That(battery.LastCharge, Is.EqualTo(charge));

            // An ordinary dispense through the same message path remains available.
            Send(dispenser, new EnergyReagentDispenserDispenseReagentMessage("Water"));
            Assert.That(solution.Comp.Solution.Volume, Is.EqualTo(FixedPoint2.New(5)));
            Assert.That(battery.LastCharge, Is.LessThan(charge));
            SEntMan.DeleteEntity(dispenser);
        });
    }

    [Test]
    public async Task OrdinaryMedicineAndLoadedVeteransRemainAllowed()
    {
        await SetPlaytime(0);
        await Server.WaitAssertion(() =>
        {
            var source = SEntMan.SpawnEntity("Bucket", MapCoordinates.Nullspace);
            var target = SEntMan.SpawnEntity("Bucket", MapCoordinates.Nullspace);
            var incoming = Fill(source, "Saline", 10);
            var receiving = Fill(target, "Water", 20);
            Assert.That(Server.System<SolutionTransferSystem>().Transfer(
                new SolutionTransferData(SPlayer, source, incoming, target, receiving, FixedPoint2.New(10))),
                Is.EqualTo(FixedPoint2.New(10)));
            Assert.That(receiving.Comp.Solution.Volume, Is.EqualTo(FixedPoint2.New(30)));
            SEntMan.DeleteEntity(source);
            SEntMan.DeleteEntity(target);
        });

        await SetPlaytime(24);
        await Server.WaitAssertion(() =>
        {
            var tracking = Server.ResolveDependency<PlayTimeTrackingManager>();
            var mixing = Server.System<ChemicalMixingSystem>();
            var source = new Solution("Sodium", FixedPoint2.New(5));
            var target = new Solution("Water", FixedPoint2.New(5));
            NoviceTestPlaytime.SetLoaded(tracking, ServerSession, false);
            try
            {
                Assert.That(mixing.CanMix(SPlayer, source, target), Is.False);
            }
            finally
            {
                NoviceTestPlaytime.SetLoaded(tracking, ServerSession, true);
            }
            Assert.That(mixing.CanMix(SPlayer, source, target), Is.True);
        });
    }

    private void Send(EntityUid target, BoundUserInterfaceMessage message)
    {
        message.Actor = SPlayer;
        SEntMan.EventBus.RaiseLocalEvent(target, (object) message);
    }

    private Entity<SolutionComponent> Fill(EntityUid container, string reagent, int amount)
    {
        var solution = Drainable(container);
        var solutions = Server.System<SharedSolutionContainerSystem>();
        solutions.RemoveAllSolution(solution);
        Assert.That(solutions.TryAddReagent(solution, reagent, FixedPoint2.New(amount)), Is.True);
        return solution;
    }

    private Entity<SolutionComponent> Drainable(EntityUid container)
    {
        Assert.That(Server.System<SharedSolutionContainerSystem>().TryGetDrainableSolution(container, out var solution, out _), Is.True);
        return solution!.Value;
    }

    private Task SetPlaytime(double hours) => Server.WaitPost(() =>
        NoviceTestPlaytime.Set(Server.ResolveDependency<PlayTimeTrackingManager>(), ServerSession, hours));
}
