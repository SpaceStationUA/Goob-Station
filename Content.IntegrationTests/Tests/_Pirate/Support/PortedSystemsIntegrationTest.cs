// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Numerics;
using Content.Client._Pirate.Temperature;
using Content.IntegrationTests.Tests.Helpers;
using Content.Server._Pirate.Screens;
using Content.Server.Temperature.Systems;
using Content.Shared._Goobstation.Wizard.Projectiles;
using Content.Shared._Pirate.Access.Components;
using Content.Shared._Pirate.Access.Systems;
using Content.Shared._Pirate.Parry;
using Content.Shared._Pirate.Silicons;
using Content.Shared._Pirate.Temperature;
using Content.Shared.Access.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceNetwork;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Projectiles;
using Content.Shared.TextScreen;
using Content.Shared.Weapons.Reflect;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._Pirate.Support;

[TestFixture]
public sealed class PortedSystemsIntegrationTest
{
    [TestPrototypes]
    private const string TestPrototypes = @"
- type: entity
  id: PirateSystemsAccessScanner
  components:
  - type: AccessScanner
    settings:
    - range: 2
      power: 0
  - type: AccessReader
    enabled: false

- type: entity
  id: PirateSystemsGaussMagazine
  parent: BaseMagazineGaussgun

- type: entity
  id: PirateSystemsRemovableCellGun
  parent: BaseWeaponPowerCell

- type: entity
  id: PirateSystemsSignalScreen
  components:
  - type: SignalScreen
    changeCooldown: 0
  - type: Appearance

- type: entity
  id: PirateSystemsSignalScreenCooldown
  components:
  - type: SignalScreen
    changeCooldown: 10
  - type: Appearance

- type: entity
  id: PirateSystemsPatrolCommander
  components:
  - type: PatrolCommander
    waypointId: PirateSystemsPatrolWaypoint

- type: entity
  id: PirateSystemsPatrolWaypoint
  components:
  - type: PatrolWaypoint
";

    [Test]
    public async Task AccessScannerUsesBoundedRangeFiltersAndPowerState()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var scanner = entMan.SpawnEntity("PirateSystemsAccessScanner", map.GridCoords);
            var nearby = entMan.SpawnEntity(null, new EntityCoordinates(map.Grid, new Vector2(1f, 0f)));
            var distant = entMan.SpawnEntity(null, new EntityCoordinates(map.Grid, new Vector2(4f, 0f)));
            entMan.EnsureComponent<IdCardComponent>(nearby).FullName = "Nearby";
            entMan.EnsureComponent<IdCardComponent>(distant).FullName = "Distant";

            var component = entMan.GetComponent<AccessScannerComponent>(scanner);
            var reader = entMan.GetComponent<AccessReaderComponent>(scanner);
            var source = entMan.GetComponent<DeviceLinkSourceComponent>(scanner);
            var system = server.System<AccessScannerSystem>();

            Assert.That(source.Ports, Is.EquivalentTo(new[]
            {
                component.ActivePort,
                component.NamePort,
                component.JobPort,
            }), "Scanner startup must register every declared DeviceLink source port.");

            system.ScanNearby((scanner, component, reader), powered: true);
            Assert.Multiple(() =>
            {
                Assert.That(component.Scanned, Is.EquivalentTo(new[] { nearby }));
                Assert.That(component.Active, Is.True);
            });

            entMan.EnsureComponent<AccessScannerBlacklistComponent>(nearby);
            system.ScanNearby((scanner, component, reader), powered: true);
            Assert.Multiple(() =>
            {
                Assert.That(component.Scanned, Is.Empty);
                Assert.That(component.Active, Is.False);
            });

            entMan.RemoveComponent<AccessScannerBlacklistComponent>(nearby);
            system.ScanNearby((scanner, component, reader), powered: true);
            Assert.That(component.Scanned, Is.EquivalentTo(new[] { nearby }));

            system.ScanNearby((scanner, component, reader), powered: false);
            Assert.Multiple(() =>
            {
                Assert.That(component.Scanned, Is.Empty);
                Assert.That(component.Active, Is.False);
            });

            entMan.DeleteEntity(distant);
            entMan.DeleteEntity(nearby);
            entMan.DeleteEntity(scanner);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AccessScannerFindsIdCardsInNestedNearbyContainers()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.EntMan;
        var containers = server.System<SharedContainerSystem>();

        await server.WaitAssertion(() =>
        {
            var scanner = entMan.SpawnEntity("PirateSystemsAccessScanner", map.GridCoords);
            var carrier = entMan.SpawnEntity(null, new EntityCoordinates(map.Grid, new Vector2(1f, 0f)));
            var pda = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var card = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            entMan.EnsureComponent<IdCardComponent>(card).FullName = "Contained";

            var pdaSlot = containers.EnsureContainer<ContainerSlot>(carrier, "pda");
            var idSlot = containers.EnsureContainer<ContainerSlot>(pda, "id");
            Assert.That(containers.Insert(pda, pdaSlot), Is.True);
            Assert.That(containers.Insert(card, idSlot), Is.True);

            var component = entMan.GetComponent<AccessScannerComponent>(scanner);
            var reader = entMan.GetComponent<AccessReaderComponent>(scanner);
            server.System<AccessScannerSystem>().ScanNearby((scanner, component, reader), powered: true);

            Assert.Multiple(() =>
            {
                Assert.That(component.Scanned, Is.EquivalentTo(new[] { card }));
                Assert.That(component.Active, Is.True);
            });

            entMan.DeleteEntity(carrier);
            entMan.DeleteEntity(scanner);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SignalScreenAcceptsSupportedPayloadsAndHonorsCooldown()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var cooldownScreen = entMan.SpawnEntity("PirateSystemsSignalScreenCooldown", map.GridCoords);
            var cooldownComponent = entMan.GetComponent<SignalScreenComponent>(cooldownScreen);
            var screen = entMan.SpawnEntity("PirateSystemsSignalScreen", map.GridCoords);
            var component = entMan.GetComponent<SignalScreenComponent>(screen);
            var system = server.System<SignalScreenSystem>();
            var appearance = server.System<SharedAppearanceSystem>();

            Assert.That(system.TrySetText((cooldownScreen, cooldownComponent), cooldownComponent.TextPort,
                new NetworkPayload { ["logic_string"] = "alpha" }), Is.True);
            AssertScreenText(cooldownScreen, "alpha");

            Assert.That(system.TrySetText((cooldownScreen, cooldownComponent), cooldownComponent.TextPort,
                new NetworkPayload { ["logic_int"] = 42 }), Is.False,
                "A second update inside the configured cooldown must be rejected.");
            AssertScreenText(cooldownScreen, "alpha");

            Assert.That(system.TrySetText((screen, component), component.TextPort,
                new NetworkPayload { ["logic_int"] = 42 }), Is.True);
            AssertScreenText(screen, "42");

            Assert.That(system.TrySetText((screen, component), component.TextPort,
                new NetworkPayload { [DeviceNetworkConstants.LogicState] = SignalState.High }), Is.True);
            AssertScreenText(screen, "true");
            Assert.That(system.TrySetText((screen, component), component.TextPort,
                new NetworkPayload { [DeviceNetworkConstants.LogicState] = SignalState.Momentary }), Is.True);
            AssertScreenText(screen, "pulse");

            Assert.Multiple(() =>
            {
                Assert.That(system.TrySetText((screen, component), "Wrong", new NetworkPayload { ["logic_string"] = "bad" }), Is.False);
                Assert.That(system.TrySetText((screen, component), component.TextPort, null), Is.False);
                Assert.That(system.TrySetText((screen, component), component.TextPort,
                    new NetworkPayload { ["unsupported"] = true }), Is.False);
            });
            AssertScreenText(screen, "pulse");

            entMan.DeleteEntity(cooldownScreen);
            entMan.DeleteEntity(screen);

            void AssertScreenText(EntityUid target, string expected)
            {
                Assert.That(appearance.TryGetData<string>(target, TextScreenVisuals.ScreenText, out var actual), Is.True);
                Assert.That(actual, Is.EqualTo(expected));
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ParryBlocksOnceReflectsProjectilesAndRegeneratesLazily()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.EntMan;

        EntityUid weapon = default;
        EntityUid user = default;
        EntityUid attacker = default;
        EntityUid projectile = default;

        await server.WaitAssertion(() =>
        {
            weapon = entMan.SpawnEntity(null, map.GridCoords);
            user = entMan.SpawnEntity(null, map.GridCoords);
            attacker = entMan.SpawnEntity(null, new EntityCoordinates(map.Grid, new Vector2(1f, 0f)));
            projectile = entMan.SpawnEntity("GaussgunBearing", new EntityCoordinates(map.Grid, new Vector2(2f, 0f)));

            var parry = entMan.EnsureComponent<ParryComponent>(weapon);
            parry.ParryMinSkill = 0;
            parry.ReflectMinSkill = 0;
            parry.ParryExhaustionCost = 0.5f;
            parry.ReflectExhaustionCost = 0.25f;
            parry.Reflects = ReflectType.NonEnergy;
            var toggle = entMan.EnsureComponent<ItemToggleComponent>(weapon);
            var parrySystem = server.System<ParrySystem>();

            Assert.Multiple(() =>
            {
                Assert.That(parrySystem.TryParry((weapon, parry), user, user), Is.False,
                    "A player must not be able to parry their own attack.");
                Assert.That(parrySystem.TryParry((weapon, parry), user, attacker), Is.False,
                    "An inactive parry item must not block attacks.");
            });

            toggle.Activated = true;
            Assert.That(parrySystem.TryParry((weapon, parry), user, attacker), Is.True);
            var exhaustion = entMan.GetComponent<ParryExhaustionComponent>(user);
            Assert.Multiple(() =>
            {
                Assert.That(exhaustion.Exhaustion, Is.EqualTo(0.5f).Within(0.001f));
                Assert.That(parrySystem.TryParry((weapon, parry), user, attacker), Is.False,
                    "The default exhaustion limit permits only one 0.5-cost parry before recovery.");
            });

            exhaustion.Exhaustion = 0f;
            var projectileComponent = entMan.GetComponent<ProjectileComponent>(projectile);
            projectileComponent.Shooter = attacker;
            projectileComponent.Weapon = attacker;
            var homing = entMan.EnsureComponent<HomingProjectileComponent>(projectile);
            homing.Target = user;
            Assert.That(parrySystem.TryReflectProjectile((weapon, parry), user, projectile), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(projectileComponent.Shooter, Is.EqualTo(user));
                Assert.That(projectileComponent.Weapon, Is.EqualTo(user));
                Assert.That(exhaustion.Exhaustion, Is.EqualTo(0.25f).Within(0.001f));
                Assert.That(homing.LifeStage, Is.GreaterThanOrEqualTo(ComponentLifeStage.Stopping),
                    "A reflected homing projectile must not steer back toward its former target.");
            });

            exhaustion.Exhaustion = 0f;
            Assert.That(parrySystem.TryReflectHitscan((weapon, parry), user, attacker, attacker,
                Vector2.UnitX, ReflectType.NonEnergy, out var reflectedDirection), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(reflectedDirection, Is.Not.Null);
                Assert.That(reflectedDirection!.Value.Length(), Is.EqualTo(1f).Within(0.001f));
            });

            exhaustion.Exhaustion = 0.75f;
            exhaustion.ExhaustionRegenDelay = TimeSpan.Zero;
            exhaustion.ExhaustionRegenRate = 1f;
            var timing = server.ResolveDependency<IGameTiming>();
            exhaustion.LastUpdate = timing.CurTime - TimeSpan.FromSeconds(1);
            exhaustion.ExhaustionRegenTimer = exhaustion.LastUpdate;
            Assert.That(parrySystem.RefreshExhaustion((user, exhaustion)), Is.Zero);

            parry.ParryExhaustionCost = 1.1f;
            Assert.That(parrySystem.TryParry((weapon, parry), user, attacker), Is.False,
                "Costs above one intentionally disable the action instead of overfilling exhaustion.");

            entMan.DeleteEntity(projectile);
            entMan.DeleteEntity(attacker);
            entMan.DeleteEntity(user);
            entMan.DeleteEntity(weapon);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MultiMagazineGunRequiresEveryProviderAndConsumesScaledBatteryCharge()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var gun = entMan.SpawnEntity("WeaponGaussgun", map.GridCoords);
            var magazine = entMan.SpawnEntity("PirateSystemsGaussMagazine", map.GridCoords);
            var cell = entMan.SpawnEntity("PowerCellHigh", map.GridCoords);
            var slots = server.System<ItemSlotsSystem>();

            AssertAmmoCount(0, 0);
            Assert.That(slots.TryInsert(gun, "gun_magazine", magazine, null), Is.True);
            AssertAmmoCount(0, 0);
            Assert.That(slots.TryInsert(gun, "gun_magazine_cell", cell, null), Is.True);
            AssertAmmoCount(15, 15);

            var appearance = server.System<SharedAppearanceSystem>();
            var effectiveCell = new GetAmmoCountEvent { FireCostMultiplier = 0.5f };
            entMan.EventBus.RaiseLocalEvent(cell, ref effectiveCell);
            var magazineAmmo = new GetAmmoCountEvent();
            entMan.EventBus.RaiseLocalEvent(magazine, ref magazineAmmo);
            Assert.Multiple(() =>
            {
                Assert.That(appearance.TryGetData<int>(gun, AmmoVisuals.AmmoCount, out var displayedCount), Is.True);
                Assert.That(displayedCount, Is.EqualTo(Math.Min(magazineAmmo.Count, effectiveCell.Count)),
                    "Composite magazine visuals must match the limiting effective provider.");
                Assert.That(appearance.TryGetData<int>(gun, AmmoVisuals.AmmoMax, out var displayedCapacity), Is.True);
                Assert.That(displayedCapacity, Is.EqualTo(Math.Min(magazineAmmo.Capacity, effectiveCell.Capacity)));
            });

            var battery = entMan.GetComponent<BatteryComponent>(cell);
            var batteries = server.System<SharedBatterySystem>();
            var initialCharge = batteries.GetCharge((cell, battery));
            var take = new TakeAmmoEvent(1, new(), map.GridCoords, null);
            entMan.EventBus.RaiseLocalEvent(gun, take);

            Assert.Multiple(() =>
            {
                Assert.That(take.Ammo, Has.Count.EqualTo(1),
                    "Only the ballistic magazine may supply the projectile; the power cell is charge-only.");
                Assert.That(batteries.GetCharge((cell, battery)), Is.EqualTo(initialCharge - 67.5f).Within(0.01f),
                    "The gauss gun must apply its configured 0.5 multiplier to the cell fire cost.");
            });
            AssertAmmoCount(14, 15);

            // Drain the ballistic magazine to one round, then request a three-round burst. The
            // charge-only cell must pay for the one projectile actually supplied.
            for (var i = 0; i < 13; i++)
            {
                var drain = new TakeAmmoEvent(1, new(), map.GridCoords, null);
                entMan.EventBus.RaiseLocalEvent(gun, drain);
                DeleteAmmo(drain);
            }

            AssertAmmoCount(1, 15);
            var chargeBeforeShortBurst = batteries.GetCharge((cell, battery));
            var shortBurst = new TakeAmmoEvent(3, new(), map.GridCoords, null);
            entMan.EventBus.RaiseLocalEvent(gun, shortBurst);
            Assert.Multiple(() =>
            {
                Assert.That(shortBurst.Ammo, Has.Count.EqualTo(1));
                Assert.That(batteries.GetCharge((cell, battery)),
                    Is.EqualTo(chargeBeforeShortBurst - 67.5f).Within(0.01f));
            });
            DeleteAmmo(shortBurst);
            AssertAmmoCount(0, 15);

            DeleteAmmo(take);
            entMan.DeleteEntity(cell);
            entMan.DeleteEntity(magazine);
            entMan.DeleteEntity(gun);

            void DeleteAmmo(TakeAmmoEvent ammoEvent)
            {
                foreach (var ammo in ammoEvent.Ammo)
                {
                    if (ammo.Entity is { } spawned)
                        entMan.DeleteEntity(spawned);
                }
            }

            void AssertAmmoCount(int expectedCount, int expectedCapacity)
            {
                var count = new GetAmmoCountEvent();
                entMan.EventBus.RaiseLocalEvent(gun, ref count);
                Assert.Multiple(() =>
                {
                    Assert.That(count.Count, Is.EqualTo(expectedCount));
                    Assert.That(count.Capacity, Is.EqualTo(expectedCapacity));
                });
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RemovingPowerCellPreservesFractionalAmmoCapacity()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var gun = entMan.SpawnEntity("PirateSystemsRemovableCellGun", map.GridCoords);
            var provider = entMan.GetComponent<BatteryAmmoProviderComponent>(gun);
            var gunSystem = server.System<SharedGunSystem>();
            var slots = server.System<ItemSlotsSystem>();

            gunSystem.UpdateShots((gun, provider));
            var capacity = provider.CapacityFloat;
            Assert.That(capacity, Is.GreaterThan(0f));
            Assert.That(slots.TryEject(gun, "cell_slot", null, out var cell, doAfter: false), Is.True);

            gunSystem.UpdateShots((gun, provider));
            var ammo = new GetAmmoCountEvent();
            entMan.EventBus.RaiseLocalEvent(gun, ref ammo);
            Assert.Multiple(() =>
            {
                Assert.That(provider.ShotsFloat, Is.Zero);
                Assert.That(provider.CapacityFloat, Is.EqualTo(capacity).Within(0.0001f));
                Assert.That(ammo.Count, Is.Zero);
                Assert.That(ammo.Capacity, Is.EqualTo((int) capacity));
            });

            if (cell is { } ejected)
                entMan.DeleteEntity(ejected);
            entMan.DeleteEntity(gun);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BlackBodyPublishesTemperatureAndCapsEmissiveColor()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var ingot = entMan.SpawnEntity("SteelIngot", map.GridCoords);
            var appearance = server.System<SharedAppearanceSystem>();
            var temperature = server.System<TemperatureSystem>();

            Assert.That(appearance.TryGetData<float>(ingot, BlackBodyVisuals.Temperature, out var initial), Is.True);
            Assert.That(initial, Is.EqualTo(entMan.GetComponent<Content.Shared.Temperature.Components.TemperatureComponent>(ingot).CurrentTemperature));

            temperature.ForceChangeTemperature(ingot, 800f);
            Assert.That(appearance.TryGetData<float>(ingot, BlackBodyVisuals.Temperature, out var updated), Is.True);
            Assert.That(updated, Is.EqualTo(800f));

            var belowThreshold = BlackBodyVisualizerSystem.GetEmissiveColor(599f);
            var threshold = BlackBodyVisualizerSystem.GetEmissiveColor(600f);
            var capped = BlackBodyVisualizerSystem.GetEmissiveColor(6000f);
            var aboveCap = BlackBodyVisualizerSystem.GetEmissiveColor(9000f);
            Assert.Multiple(() =>
            {
                Assert.That(belowThreshold.A, Is.Zero);
                Assert.That(threshold.A, Is.GreaterThan(0f));
                Assert.That(capped, Is.EqualTo(aboveCap));
                Assert.That(float.IsFinite(threshold.R) && float.IsFinite(threshold.G) && float.IsFinite(threshold.B), Is.True);
            });

            entMan.DeleteEntity(ingot);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PotyzhnobotWaypointCleanupPrunesDeletedAndQueuesLiveWaypoints()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.EntMan;
        EntityUid liveWaypoint = default;

        await server.WaitAssertion(() =>
        {
            var commander = entMan.SpawnEntity("PirateSystemsPatrolCommander", map.GridCoords);
            liveWaypoint = entMan.SpawnEntity("PirateSystemsPatrolWaypoint", map.GridCoords);
            var deletedWaypoint = entMan.SpawnEntity("PirateSystemsPatrolWaypoint", map.GridCoords);
            var component = entMan.GetComponent<PatrolCommanderComponent>(commander);
            component.Waypoints.Add(liveWaypoint);
            component.Waypoints.Add(deletedWaypoint);
            component.IsPatrolling = true;
            entMan.DeleteEntity(deletedWaypoint);

            var system = server.System<WandskySystem>();
            system.PruneDeletedWaypoints((commander, component));
            Assert.Multiple(() =>
            {
                Assert.That(component.Waypoints, Is.EquivalentTo(new[] { liveWaypoint }));
                Assert.That(component.IsPatrolling, Is.True);
            });

            system.ClearWaypoints((commander, component));
            Assert.Multiple(() =>
            {
                Assert.That(component.Waypoints, Is.Empty);
                Assert.That(component.IsPatrolling, Is.False);
            });

            entMan.DeleteEntity(commander);
        });

        await pair.RunTicksSync(3);
        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.Deleted(liveWaypoint), Is.True,
                "Clearing a commander route must queue every remaining waypoint for deletion.");
        });

        await pair.CleanReturnAsync();
    }
}
