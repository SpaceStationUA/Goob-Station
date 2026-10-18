// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Maths.FixedPoint;
using Content.Server.Players.PlayTimeTracking;
using Content.Server.Power.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Events;
using Content.Shared.Chemistry.Prototypes;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Players.PlayTimeTracking;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Pirate.Newbies;

[TestFixture]
public sealed class NoviceExplosiveProtectionTest
{
    [TestCase(0, true)]
    [TestCase(23.99, true)]
    [TestCase(24, false)]
    [TestCase(25, false)]
    public async Task ProtectionBlocksOnlyDangerousInteractions(double hours, bool restricted)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        var session = pair.Player!;
        EntityUid cell = default;
        EntityUid syringe = default;

        await server.WaitAssertion(() =>
        {
            var user = entMan.SpawnEntity("MobHuman", map.GridCoords);
            server.PlayerMan.SetAttachedEntity(session, user);
            var playTime = server.ResolveDependency<PlayTimeTrackingManager>();
            playTime.GetTrackerTimes(session)[PlayTimeTrackingShared.TrackerOverall] = TimeSpan.FromHours(hours);

            cell = entMan.SpawnEntity("PowerCellSmall", map.GridCoords);
            syringe = entMan.SpawnEntity("Syringe", map.GridCoords);
            var solutions = server.System<SharedSolutionContainerSystem>();
            var injector = entMan.GetComponent<InjectorComponent>(syringe);
            Assert.That(solutions.TryGetSolution(syringe, injector.SolutionName, out var syringeSolution, out _), Is.True);
            Assert.That(solutions.TryAddReagent(syringeSolution!.Value, "Plasma", FixedPoint2.New(5)), Is.True);
            server.System<InjectorSystem>().ToggleMode((syringe, injector), user,
                server.ProtoMan.Index<InjectorModePrototype>("SyringeInjectMode"));
            Assert.That(server.System<SharedHandsSystem>().TryPickupAnyHand(user, syringe), Is.True);

            var inject = new AfterInteractEvent(user, syringe, cell, map.GridCoords, true);
            entMan.EventBus.RaiseLocalEvent(syringe, inject);

            // Ordinary medical injections must remain available to novices.
            var medical = new SelfBeforeInjectEvent(user, syringe, user);
            entMan.EventBus.RaiseLocalEvent(user, medical);
            Assert.That(medical.Cancelled, Is.False);

            var welder = entMan.SpawnEntity("Welder", map.GridCoords);
            var tank = entMan.SpawnEntity("WeldingFuelTankFull", map.GridCoords);
            var interaction = server.System<SharedInteractionSystem>();
            Assert.That(interaction.RangedInteractDoBefore(user, welder, tank, map.GridCoords, true), Is.False,
                "An unlit welder must remain usable for refuelling.");

            Assert.That(server.System<ItemToggleSystem>().TryActivate(welder, user), Is.True);
            Assert.That(interaction.RangedInteractDoBefore(user, welder, tank, map.GridCoords, true), Is.EqualTo(restricted));

            var waterTank = entMan.SpawnEntity("WaterTank", map.GridCoords);
            Assert.That(interaction.RangedInteractDoBefore(user, welder, waterTank, map.GridCoords, true), Is.False);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<RiggableComponent>(cell).IsRigged, Is.EqualTo(!restricted));
            var solutions = server.System<SharedSolutionContainerSystem>();
            var injector = entMan.GetComponent<InjectorComponent>(syringe);
            Assert.That(solutions.TryGetSolution(syringe, injector.SolutionName, out _, out var remaining), Is.True);
            Assert.That(remaining!.Volume, Is.EqualTo(FixedPoint2.New(restricted ? 5 : 0)),
                "Blocked injections must not consume the syringe's contents.");
        });

        await pair.CleanReturnAsync();
    }
}
