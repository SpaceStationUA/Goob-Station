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
using Content.Shared.CombatMode;
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
        EntityUid user = default;
        EntityUid welder = default;
        EntityUid unlitTank = default;
        EntityUid tank = default;
        EntityUid waterTank = default;
        EntityUid wideUser = default;
        EntityUid wideWelder = default;
        EntityUid wideTank = default;
        EntityUid wideWaterTank = default;

        EntityCoordinates Coordinates(float x, float y) =>
            new(map.GridCoords.EntityId, map.GridCoords.Position + new Vector2(x, y));

        await server.WaitAssertion(() =>
        {
            // CreateTestMap supplies one tile; both melee scenes need a proper floor and physics update.
            var maps = server.System<SharedMapSystem>();
            for (var x = -2; x <= 6; x++)
            for (var y = -2; y <= 3; y++)
            {
                maps.SetTile(map.Grid.Owner, map.Grid.Comp, Coordinates(x, y), map.Tile.Tile);
            }

            user = entMan.SpawnEntity("MobHuman", map.GridCoords);
            server.PlayerMan.SetAttachedEntity(session, user);
            server.ResolveDependency<PlayTimeTrackingManager>()
                .GetTrackerTimes(session)[PlayTimeTrackingShared.TrackerOverall] = TimeSpan.FromHours(hours);

            var hands = server.System<SharedHandsSystem>();
            welder = entMan.SpawnEntity(welderPrototype, map.GridCoords);
            Assert.That(hands.TryPickupAnyHand(user, welder), Is.True);
            unlitTank = entMan.SpawnEntity("WeldingFuelTankFull", Coordinates(1, 0));
            tank = entMan.SpawnEntity("WeldingFuelTankFull", Coordinates(0, 1));
            waterTank = entMan.SpawnEntity("WaterTank", Coordinates(-1, 0));

            wideUser = entMan.SpawnEntity("MobHuman", Coordinates(4, 0));
            wideWelder = entMan.SpawnEntity(welderPrototype, Coordinates(4, 0));
            Assert.That(hands.TryPickupAnyHand(wideUser, wideWelder), Is.True);
            wideTank = entMan.SpawnEntity("WeldingFuelTankFull", Coordinates(3.5f, 1));
            wideWaterTank = entMan.SpawnEntity("WaterTank", Coordinates(4.5f, 1));
        });

        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            var melee = server.System<MeleeWeaponSystem>();
            var weapon = entMan.GetComponent<MeleeWeaponComponent>(welder);
            var unlitDamage = entMan.GetComponent<DamageableComponent>(unlitTank);

            void Hit(EntityUid target)
            {
                Assert.That(melee.InRange(user, target, weapon.Range, null), Is.True,
                    "The melee target must be reachable in the test scene.");
                var attack = new LightAttackEvent(entMan.GetNetEntity(target), entMan.GetNetEntity(welder),
                    entMan.GetNetCoordinates(entMan.GetComponent<TransformComponent>(target).Coordinates));
                melee.DoLightAttack(user, attack, welder, weapon, null);
            }

            Hit(unlitTank);
            Assert.That(unlitDamage.TotalDamage, Is.GreaterThan(FixedPoint2.Zero),
                "An unlit welder must remain usable as an ordinary melee weapon.");

            var tankDamage = entMan.GetComponent<DamageableComponent>(tank);
            var waterDamage = entMan.GetComponent<DamageableComponent>(waterTank);
            Assert.That(server.System<ItemToggleSystem>().TryActivate(welder, user), Is.True);

            Hit(waterTank);
            Assert.That(waterDamage.Damage.DamageDict["Heat"], Is.GreaterThan(FixedPoint2.Zero),
                "Lit welders must remain usable against targets other than fuel tanks.");

            Hit(tank);
            Assert.That(tankDamage.TotalDamage > FixedPoint2.Zero, Is.EqualTo(!restricted),
                "A novice's lit welder attack must be cancelled before any fuel tank damage is applied.");

            server.PlayerMan.SetAttachedEntity(session, wideUser);
            server.System<SharedCombatModeSystem>().SetInCombatMode(wideUser, true);
            Assert.That(server.System<ItemToggleSystem>().TryActivate(wideWelder, wideUser), Is.True);
            var wideWeapon = entMan.GetComponent<MeleeWeaponComponent>(wideWelder);
            var wideTankDamage = entMan.GetComponent<DamageableComponent>(wideTank);
            Assert.That(melee.AttemptHeavyAttack(wideUser, wideWelder, wideWeapon, [wideWaterTank, wideTank],
                Coordinates(4, wideWeapon.Range)), Is.True);
            Assert.That(wideTankDamage.TotalDamage > FixedPoint2.Zero, Is.EqualTo(!restricted),
                "A wide attack must be cancelled before it damages a fuel tank.");
        });

        await pair.CleanReturnAsync();
    }
}
