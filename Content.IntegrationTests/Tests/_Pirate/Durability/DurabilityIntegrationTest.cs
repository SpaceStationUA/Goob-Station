// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared._Pirate.Durability;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Materials;
using Content.Shared.Stacks;
using Content.Shared.Tools.Components;
using Content.Shared.Tools;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Pirate.Durability;

[TestFixture]
public sealed class DurabilityIntegrationTest
{
    private static readonly ProtoId<DamageTypePrototype> BluntDamageType = "Blunt";

    [TestPrototypes]
    private const string TestPrototypes = @"
- type: stack
  id: PirateDurabilityRepairStack
  name: pirate-durability-repair-stack
  spawn: PirateDurabilityRepairMaterial
  maxCount: 10

- type: entity
  id: PirateDurabilityRepairMaterial
  components:
  - type: Material
  - type: PhysicalComposition
    materialComposition:
      Steel: 100
  - type: Stack
    stackType: PirateDurabilityRepairStack
    count: 2
  - type: CustomDurabilityModifier
    maxDurabilityStateModifiers:
      Worn: 2, 1.5
      Broken: -2, 0.5

- type: entity
  id: PirateDurabilityRepairTool
  components:
  - type: Item
  - type: Tool
    qualities:
    - Welding

- type: entity
  id: PirateDurabilityRepairUser
  components:
  - type: DoAfter
  - type: Hands
    hands:
      hand:
        location: Middle
    sortedHands:
    - hand
  - type: ComplexInteraction

- type: entity
  id: PirateDurabilityDamageTarget
  components:
  - type: Damageable
    damageContainer: Inorganic

- type: entity
  id: PirateDurabilityAttacker
  components:
  - type: MeleeWeapon
    attackRate: 1
    damage:
      types:
        Blunt: 5

- type: entity
  id: PirateDurabilityTestWeapon
  components:
  - type: MeleeWeapon
    damage:
      types:
        Blunt: 10
  - type: Gun
    minAngle: 2
    maxAngle: 10
  - type: Durability
    durabilityThresholds:
      0: Pristine
      10: Worn
      20: Damaged
      30: Broken
      40: Destroyed
    damageProbability: 1
    minDamageRoll: 5
    maxDamageRoll: 5
    maxRepairBonus: 5
    deleteOnDestroyed: false
    repairable: true
    repairMaterials:
      Steel: 4, 4
    repairTool: Welding
    toolRepairAmount: 6, 6
    fuelCost: 0
    repairDoAfter: 0

- type: entity
  id: PirateDurabilityDeleteWeapon
  components:
  - type: Durability
    durabilityThresholds:
      0: Pristine
      1: Destroyed
    damageProbability: 1
    deleteOnDestroyed: true

- type: entity
  id: PirateDurabilityIrreparable
  components:
  - type: Durability
    durabilityThresholds:
      0: Pristine
    damageProbability: 1
    repairable: false
";

    [Test]
    public async Task DamageStatesScalingProbabilityAndRepairCapAreDeterministic()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var system = server.System<DurabilitySystem>();
            var bare = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var weapon = entMan.SpawnEntity("PirateDurabilityTestWeapon", MapCoordinates.Nullspace);
            var durability = entMan.GetComponent<DurabilityComponent>(weapon);

            Assert.That(system.DamageEntity(bare, 1), Is.False,
                "Entities without Durability must reject wear.");

            system.SetDamageProbability((weapon, durability), -5f);
            Assert.That(durability.DamageProbability, Is.Zero);
            Assert.That(system.DamageEntity(weapon, 100, durability), Is.False,
                "Zero probability must deterministically reject positive wear.");

            system.SetDamageProbability((weapon, durability), 5f);
            Assert.That(durability.DamageProbability, Is.EqualTo(1f));
            Assert.That(system.DamageEntity(weapon, 9, durability), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(durability.Damage, Is.EqualTo(FixedPoint2.New(9)));
                Assert.That(durability.DurabilityState, Is.EqualTo(DurabilityState.Pristine));
                Assert.That(system.GetModifier(durability), Is.EqualTo(1f));
            });

            Assert.That(system.DamageEntity(weapon, 1, durability), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(durability.DurabilityState, Is.EqualTo(DurabilityState.Worn));
                Assert.That(system.GetModifier(durability), Is.EqualTo(0.9f).Within(0.001f));
            });

            system.ScaleDamageProbability((weapon, durability), 2f);
            Assert.That(durability.DamageProbability, Is.EqualTo(0.5f));
            system.ScaleDamageProbability((weapon, durability), 0f);
            Assert.That(durability.DamageProbability, Is.EqualTo(0.5f),
                "A zero divisor must be a no-op.");
            system.SetDamageProbability((weapon, durability), 1f);

            Assert.That(system.DamageEntity(weapon, -100, durability), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(durability.Damage, Is.EqualTo(FixedPoint2.New(-5)),
                    "Repair must clamp at MaxRepairBonus.");
                Assert.That(durability.DurabilityState, Is.EqualTo(DurabilityState.Reinforced));
                Assert.That(system.GetModifier(durability), Is.EqualTo(1f));
            });

            system.SetScale((weapon, durability), FixedPoint2.New(2));
            Assert.That(system.DamageEntity(weapon, 24, durability), Is.True);
            Assert.That(durability.DurabilityState, Is.EqualTo(DurabilityState.Pristine),
                "Scaled Worn threshold should be 20 damage.");
            Assert.That(system.DamageEntity(weapon, 1, durability), Is.True);
            Assert.That(durability.DurabilityState, Is.EqualTo(DurabilityState.Worn));

            entMan.DeleteEntity(weapon);
            entMan.DeleteEntity(bare);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MeleeGunWearModifiersAndDestroyedGuardsWork()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var system = server.System<DurabilitySystem>();
            var blunt = server.ProtoMan.Index<DamageTypePrototype>(BluntDamageType);
            var weapon = entMan.SpawnEntity("PirateDurabilityTestWeapon", MapCoordinates.Nullspace);
            var attacker = entMan.SpawnEntity("PirateDurabilityAttacker", MapCoordinates.Nullspace);
            var target = entMan.SpawnEntity("PirateDurabilityDamageTarget", MapCoordinates.Nullspace);
            var durability = entMan.GetComponent<DurabilityComponent>(weapon);
            var melee = entMan.GetComponent<MeleeWeaponComponent>(weapon);
            var gun = entMan.GetComponent<GunComponent>(weapon);

            var miss = new MeleeHitEvent([], attacker, weapon, new DamageSpecifier(blunt, 10), null, EntityCoordinates.Invalid)
            {
                IsHit = false,
            };
            entMan.EventBus.RaiseLocalEvent(weapon, miss);
            var nonDamageableHit = new MeleeHitEvent([attacker], attacker, weapon,
                new DamageSpecifier(blunt, 10), null, EntityCoordinates.Invalid);
            entMan.EventBus.RaiseLocalEvent(weapon, nonDamageableHit);
            Assert.That(durability.Damage, Is.EqualTo(FixedPoint2.New(0)),
                "Misses and hits without a Damageable target must not wear the weapon.");

            var hit = new MeleeHitEvent([target], attacker, weapon,
                new DamageSpecifier(blunt, 10), null, EntityCoordinates.Invalid);
            entMan.EventBus.RaiseLocalEvent(weapon, hit);
            Assert.That(durability.Damage, Is.EqualTo(FixedPoint2.New(5)));

            var shot = new GunShotEvent(attacker, []);
            entMan.EventBus.RaiseLocalEvent(weapon, ref shot);
            Assert.Multiple(() =>
            {
                Assert.That(durability.Damage, Is.EqualTo(FixedPoint2.New(10)));
                Assert.That(durability.DurabilityState, Is.EqualTo(DurabilityState.Worn));
            });

            var meleeDamage = new GetMeleeDamageEvent(
                weapon,
                new DamageSpecifier(blunt, 10),
                [],
                attacker);
            entMan.EventBus.RaiseLocalEvent(weapon, ref meleeDamage);
            Assert.That(meleeDamage.Damage.DamageDict["Blunt"].Float(), Is.EqualTo(9f).Within(0.001f));

            var refresh = new GunRefreshModifiersEvent(
                (weapon, gun),
                null,
                1f,
                Angle.FromDegrees(2),
                Angle.FromDegrees(4),
                Angle.FromDegrees(10),
                Angle.FromDegrees(2),
                1,
                2f,
                1f,
                3f,
                4f,
                attacker);
            entMan.EventBus.RaiseLocalEvent(weapon, ref refresh);
            Assert.Multiple(() =>
            {
                Assert.That(refresh.FireRate, Is.EqualTo(1.8f).Within(0.001f));
                Assert.That(refresh.BurstFireRate, Is.EqualTo(2.7f).Within(0.001f));
                Assert.That(refresh.AngleIncrease.Theta, Is.EqualTo(Angle.FromDegrees(1.8).Theta).Within(0.001));
                Assert.That(refresh.AngleDecay.Theta, Is.EqualTo(Angle.FromDegrees(3.6).Theta).Within(0.001));
                Assert.That(refresh.MaxAngle.Theta, Is.EqualTo(Angle.FromDegrees(10 / 0.9).Theta).Within(0.001));
                Assert.That(refresh.MinAngle.Theta, Is.EqualTo(Angle.FromDegrees(2 / 0.9).Theta).Within(0.001));
                Assert.That(refresh.BurstCooldown, Is.EqualTo(4f / 0.9f).Within(0.001f));
            });

            Assert.That(system.DamageEntity(weapon, 30, durability, attacker), Is.True);
            Assert.That(durability.DurabilityState, Is.EqualTo(DurabilityState.Destroyed));

            var meleeAttempt = new AttemptMeleeEvent(attacker, weapon, melee, false);
            entMan.EventBus.RaiseLocalEvent(weapon, ref meleeAttempt);
            var shootAttempt = new AttemptShootEvent(attacker, null);
            entMan.EventBus.RaiseLocalEvent(weapon, ref shootAttempt);
            Assert.Multiple(() =>
            {
                Assert.That(meleeAttempt.Cancelled, Is.True);
                Assert.That(meleeAttempt.Message, Is.Not.Null.And.Not.Empty);
                Assert.That(shootAttempt.Cancelled, Is.True);
                Assert.That(shootAttempt.Message, Is.Not.Null.And.Not.Empty);
            });

            var destroyedDamage = new GetMeleeDamageEvent(
                weapon,
                new DamageSpecifier(blunt, 10),
                [],
                attacker);
            entMan.EventBus.RaiseLocalEvent(weapon, ref destroyedDamage);
            Assert.That(destroyedDamage.Damage.GetTotal().Float(), Is.Zero);

            var destroyedRefresh = new GunRefreshModifiersEvent(
                (weapon, gun),
                null,
                1f,
                Angle.FromDegrees(2),
                Angle.FromDegrees(4),
                Angle.FromDegrees(10),
                Angle.FromDegrees(2),
                1,
                2f,
                1f,
                3f,
                4f,
                attacker);
            entMan.EventBus.RaiseLocalEvent(weapon, ref destroyedRefresh);
            Assert.Multiple(() =>
            {
                Assert.That(destroyedRefresh.FireRate, Is.Zero);
                Assert.That(destroyedRefresh.BurstFireRate, Is.Zero);
                Assert.That(destroyedRefresh.MaxAngle.Theta, Is.EqualTo(Angle.FromDegrees(10).Theta));
                Assert.That(destroyedRefresh.MinAngle.Theta, Is.EqualTo(Angle.FromDegrees(2).Theta));
                Assert.That(destroyedRefresh.BurstCooldown, Is.EqualTo(4f));
            });

            entMan.DeleteEntity(target);
            entMan.DeleteEntity(attacker);
            entMan.DeleteEntity(weapon);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MaterialAndToolRepairInteractionsConsumeExactlyAndRespectCaps()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        EntityUid weapon = default;
        EntityUid user = default;
        EntityUid material = default;
        EntityUid tool = default;

        await server.WaitAssertion(() =>
        {
            var system = server.System<DurabilitySystem>();
            weapon = entMan.SpawnEntity("PirateDurabilityTestWeapon", MapCoordinates.Nullspace);
            user = entMan.SpawnEntity("PirateDurabilityRepairUser", MapCoordinates.Nullspace);
            material = entMan.SpawnEntity("PirateDurabilityRepairMaterial", MapCoordinates.Nullspace);
            tool = entMan.SpawnEntity("PirateDurabilityRepairTool", MapCoordinates.Nullspace);
            Assert.That(server.System<SharedHandsSystem>().TryPickupAnyHand(user, tool), Is.True,
                "The repair tool must be held because tool DoAfters enforce NeedHand.");
            var durability = entMan.GetComponent<DurabilityComponent>(weapon);
            Assert.That(system.DamageEntity(weapon, 20, durability), Is.True);
        });

        async Task Interact(EntityUid used, bool expectedHandled = true)
        {
            await server.WaitAssertion(() =>
            {
                var interaction = new InteractUsingEvent(user, used, weapon, EntityCoordinates.Invalid);
                entMan.EventBus.RaiseLocalEvent(weapon, interaction);
                Assert.That(interaction.Handled, Is.EqualTo(expectedHandled));
            });
            await pair.RunTicksSync(2);
        }

        await Interact(material);
        await server.WaitAssertion(() =>
        {
            var durability = entMan.GetComponent<DurabilityComponent>(weapon);
            var stack = entMan.GetComponent<StackComponent>(material);
            Assert.Multiple(() =>
            {
                Assert.That(durability.Damage, Is.EqualTo(FixedPoint2.New(16)));
                Assert.That(stack.Count, Is.EqualTo(1));
                Assert.That(durability.CustomDurabilityModifiers[DurabilityState.Worn],
                    Is.EqualTo(1.2f).Within(0.001f),
                    "The repair material must apply its Worn damage modifier once.");
            });
        });

        await Interact(material);
        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.Deleted(material), Is.True,
                "The second repair must consume the final stack item.");
            Assert.That(entMan.GetComponent<DurabilityComponent>(weapon).Damage,
                Is.EqualTo(FixedPoint2.New(12)));
        });

        await Interact(tool);
        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<DurabilityComponent>(weapon).Damage,
                Is.EqualTo(FixedPoint2.New(6)));
            Assert.That(entMan.Deleted(tool), Is.False, "A repair tool must not be consumed.");
        });

        await Interact(tool);
        await Interact(tool);
        await server.WaitAssertion(() =>
        {
            var durability = entMan.GetComponent<DurabilityComponent>(weapon);
            Assert.Multiple(() =>
            {
                Assert.That(durability.Damage, Is.EqualTo(FixedPoint2.New(-5)));
                Assert.That(durability.DurabilityState, Is.EqualTo(DurabilityState.Reinforced));
            });
        });

        await Interact(tool, expectedHandled: false);
        await server.WaitAssertion(() =>
        {
            var irreparable = entMan.SpawnEntity("PirateDurabilityIrreparable", MapCoordinates.Nullspace);
            var interaction = new InteractUsingEvent(user, tool, irreparable, EntityCoordinates.Invalid);
            entMan.EventBus.RaiseLocalEvent(irreparable, interaction);
            Assert.That(interaction.Handled, Is.False);
            entMan.DeleteEntity(irreparable);
            entMan.DeleteEntity(tool);
            entMan.DeleteEntity(user);
            entMan.DeleteEntity(weapon);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CustomModifiersArePrunedAndDestroyedItemsQueueDeletion()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid destroyed = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var system = server.System<DurabilitySystem>();
            var weapon = entMan.SpawnEntity("PirateDurabilityTestWeapon", MapCoordinates.Nullspace);
            var material = entMan.SpawnEntity("PirateDurabilityRepairMaterial", MapCoordinates.Nullspace);
            var durability = entMan.GetComponent<DurabilityComponent>(weapon);

            Assert.That(system.DamageEntity(weapon, 10, durability, used: material), Is.True);
            Assert.That(durability.CustomDurabilityModifiers[DurabilityState.Worn],
                Is.EqualTo(1.2f).Within(0.001f));
            Assert.That(system.GetModifier(durability), Is.EqualTo(1.2f).Within(0.001f));

            Assert.That(system.DamageEntity(weapon, 10, durability, used: material), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(durability.DurabilityState, Is.EqualTo(DurabilityState.Damaged));
                Assert.That(durability.CustomDurabilityModifiers, Does.Not.ContainKey(DurabilityState.Worn));
                Assert.That(system.GetModifier(durability), Is.EqualTo(0.7f).Within(0.001f));
            });

            Assert.That(system.DamageEntity(weapon, 10, durability, used: material), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(durability.DurabilityState, Is.EqualTo(DurabilityState.Broken));
                Assert.That(durability.CustomDurabilityModifiers[DurabilityState.Broken],
                    Is.EqualTo(0.8f).Within(0.001f));
            });

            Assert.That(system.DamageEntity(weapon, -20, durability, used: material), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(durability.DurabilityState, Is.EqualTo(DurabilityState.Worn));
                Assert.That(durability.CustomDurabilityModifiers, Does.Not.ContainKey(DurabilityState.Broken));
                Assert.That(durability.CustomDurabilityModifiers[DurabilityState.Worn],
                    Is.EqualTo(1.2f).Within(0.001f));
            });

            destroyed = entMan.SpawnEntity("PirateDurabilityDeleteWeapon", MapCoordinates.Nullspace);
            Assert.That(system.DamageEntity(destroyed, 1), Is.True);
            Assert.That(entMan.Deleted(destroyed), Is.False,
                "DeleteOnDestroyed uses queued deletion and must not delete during the event stack.");

            entMan.DeleteEntity(material);
            entMan.DeleteEntity(weapon);
        });

        await pair.RunTicksSync(1);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.Deleted(destroyed), Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EveryDurabilityPrototypeHasValidThresholdsAndRepairSources()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ProtoMan;
            var factory = server.ResolveDependency<IComponentFactory>();
            var entities = prototypes.EnumeratePrototypes<EntityPrototype>().ToArray();
            var failures = new List<string>();
            var durableCount = 0;

            foreach (var entity in entities)
            {
                if (!entity.TryGetComponent<DurabilityComponent>(out var durability, factory))
                    continue;

                durableCount++;
                var owner = entity.ID;
                if (durability!.DurabilityScale <= 0)
                    failures.Add($"{owner}: durabilityScale must be positive");
                if (durability.DamageProbability is < 0f or > 1f)
                    failures.Add($"{owner}: damageProbability must be between 0 and 1");
                if (durability.MinDamageRoll < 0 || durability.MaxDamageRoll < durability.MinDamageRoll)
                    failures.Add($"{owner}: invalid damage roll range {durability.MinDamageRoll}-{durability.MaxDamageRoll}");
                if (durability.MaxRepairBonus < 0 || durability.RepairDoAfter < TimeSpan.Zero)
                    failures.Add($"{owner}: repair bonus and delay cannot be negative");
                if (durability.DurabilityThresholds.Count > 0)
                {
                    foreach (var (threshold, state) in durability.DurabilityThresholds)
                    {
                        if (threshold != 0 || state != DurabilityState.Pristine)
                            failures.Add($"{owner}: non-empty thresholds must start with Pristine at zero");
                        break;
                    }
                }

                var previous = DurabilityState.Reinforced;
                foreach (var (threshold, state) in durability.DurabilityThresholds)
                {
                    if (threshold < 0 || state < previous)
                        failures.Add($"{owner}: thresholds/states are not monotonic at {threshold}:{state}");
                    previous = state;
                }

                foreach (var (materialId, range) in durability.RepairMaterials)
                {
                    if (!prototypes.HasIndex<MaterialPrototype>(materialId))
                        failures.Add($"{owner}: unknown repair material {materialId.Id}");
                    if (range.X <= 0f || range.Y < range.X)
                        failures.Add($"{owner}: invalid repair range {range} for {materialId.Id}");

                    var hasSource = entities.Any(candidate =>
                        !candidate.Abstract &&
                        candidate.TryGetComponent<MaterialComponent>(out _, factory) &&
                        candidate.TryGetComponent<PhysicalCompositionComponent>(out var composition, factory) &&
                        composition!.MaterialComposition.ContainsKey(materialId.Id));
                    if (!hasSource)
                        failures.Add($"{owner}: repair material {materialId.Id} has no concrete player-usable source");
                }

                if (durability.RepairTool is not { } quality)
                    continue;

                if (!prototypes.HasIndex<ToolQualityPrototype>(quality))
                    failures.Add($"{owner}: unknown repair tool quality {quality.Id}");
                if (durability.ToolRepairAmount.X <= 0f || durability.ToolRepairAmount.Y < durability.ToolRepairAmount.X)
                    failures.Add($"{owner}: invalid tool repair range {durability.ToolRepairAmount}");

                var hasTool = false;
                foreach (var candidate in entities)
                {
                    if (candidate.Abstract ||
                        !candidate.TryGetComponent<ToolComponent>(out var tool, factory))
                    {
                        continue;
                    }

                    foreach (var candidateQuality in tool!.Qualities)
                    {
                        if (candidateQuality != quality)
                            continue;

                        hasTool = true;
                        break;
                    }

                    if (hasTool)
                        break;
                }
                if (!hasTool)
                    failures.Add($"{owner}: no concrete tool provides {quality.Id}");
            }

            Assert.That(durableCount, Is.GreaterThan(0), "No durable item prototypes were loaded.");
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public void DurabilitySystemHasNoPerFrameUpdateLoop()
    {
        var updates = typeof(DurabilitySystem)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(method => method.DeclaringType == typeof(DurabilitySystem) && method.Name == "Update")
            .ToArray();

        Assert.That(updates, Is.Empty,
            "Durability must remain event-driven and never scan world entities every frame.");
    }
}
