// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Goobstation.Maths.FixedPoint;
using Content.Server.Players.PlayTimeTracking;
using Content.Server.Power.Components;
using Content.Server.Weapons.Melee;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Events;
using Content.Shared.Chemistry.Prototypes;
using Content.Shared.Damage.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Players.PlayTimeTracking;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
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

    [TestCase(0, true, "Welder")]
    [TestCase(23.99, true, "Welder")]
    [TestCase(24, false, "Welder")]
    [TestCase(25, false, "Welder")]
    [TestCase(0, true, "WelderExperimental")]
    [TestCase(24, false, "WelderExperimental")]
    public async Task LitWelderMeleeCannotIgniteFuelTanksForNovices(double hours, bool restricted, string welderPrototype)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        var session = pair.Player!;

        await server.WaitAssertion(() =>
        {
            var user = entMan.SpawnEntity("MobHuman", map.GridCoords);
            server.PlayerMan.SetAttachedEntity(session, user);
            server.ResolveDependency<PlayTimeTrackingManager>()
                .GetTrackerTimes(session)[PlayTimeTrackingShared.TrackerOverall] = TimeSpan.FromHours(hours);

            var welder = entMan.SpawnEntity(welderPrototype, map.GridCoords);
            Assert.That(server.System<SharedHandsSystem>().TryPickupAnyHand(user, welder), Is.True);
            var melee = server.System<MeleeWeaponSystem>();
            var weapon = entMan.GetComponent<MeleeWeaponComponent>(welder);
            var unlitTank = entMan.SpawnEntity("WeldingFuelTankFull",
                new EntityCoordinates(map.GridCoords.EntityId, map.GridCoords.Position + Vector2.UnitX));
            var unlitDamage = entMan.GetComponent<DamageableComponent>(unlitTank);

            void Hit(EntityUid target)
            {
                var attack = new LightAttackEvent(entMan.GetNetEntity(target), entMan.GetNetEntity(welder),
                    entMan.GetNetCoordinates(entMan.GetComponent<TransformComponent>(target).Coordinates));
                melee.DoLightAttack(user, attack, welder, weapon, null);
            }

            Hit(unlitTank);
            Assert.That(unlitDamage.TotalDamage, Is.GreaterThan(FixedPoint2.Zero),
                "An unlit welder must remain usable as an ordinary melee weapon.");

            var tank = entMan.SpawnEntity("WeldingFuelTankFull",
                new EntityCoordinates(map.GridCoords.EntityId, map.GridCoords.Position + Vector2.UnitY));
            var tankDamage = entMan.GetComponent<DamageableComponent>(tank);
            var waterTank = entMan.SpawnEntity("WaterTank",
                new EntityCoordinates(map.GridCoords.EntityId, map.GridCoords.Position - Vector2.UnitX));
            var waterDamage = entMan.GetComponent<DamageableComponent>(waterTank);
            Assert.That(server.System<ItemToggleSystem>().TryActivate(welder, user), Is.True);

            var wideHit = new MeleeHitEvent([waterTank, tank], user, welder, melee.GetDamage(welder, user, weapon),
                Vector2.UnitY, map.GridCoords);
            entMan.EventBus.RaiseLocalEvent(welder, wideHit, true);
            Assert.That(wideHit.Handled, Is.EqualTo(restricted),
                "Wide attacks must also be blocked when a fuel tank is among the targets.");

            Hit(waterTank);
            Assert.That(waterDamage.Damage.DamageDict["Heat"], Is.GreaterThan(FixedPoint2.Zero),
                "Lit welders must remain usable against targets other than fuel tanks.");

            Hit(tank);
            Assert.That(tankDamage.TotalDamage > FixedPoint2.Zero, Is.EqualTo(!restricted),
                "A novice's lit welder attack must be cancelled before any fuel tank damage is applied.");
        });

        await pair.CleanReturnAsync();
    }
}
