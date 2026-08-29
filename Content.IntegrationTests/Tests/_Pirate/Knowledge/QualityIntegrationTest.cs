// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Generic;
using Content.Goobstation.Maths.FixedPoint;
using Content.Server.Cargo.Components;
using Content.Server.Damage.Components;
using Content.Server.Destructible;
using Content.Server.Stack;
using Content.Shared._Pirate.CCVars;
using Content.Shared._Pirate.Durability;
using Content.Shared._Pirate.Knowledge;
using Content.Shared._Pirate.Knowledge.Quality;
using Content.Shared.Armor;
using Content.Shared.Blocking;
using Content.Shared.Clothing.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Destructible.Thresholds.Triggers;
using Content.Shared.Explosion.Components;
using Content.Shared.NameModifier.EntitySystems;
using Content.Shared.Projectiles;
using Content.Shared.Stacks;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Pirate.Knowledge;

[TestFixture]
public sealed class QualityIntegrationTest
{
    [TestPrototypes]
    private const string TestPrototypes = @"
- type: quality
  id: PirateQualityIntegration
  gun: 0.81
  armor: 0.82
  clothingDelay: 0.83
  explosionResist: 0.84
  staminaResist: 0.85
  health: 1.21
  selfDamage: 0.87
  damage: 1.07
  projectile: 1.08
  durability: 1.09
  shield: 0.91
  shieldFlat: 1.11
  meleeDamage: 1.12
  price: 1.31

- type: stack
  id: PirateQualityIntegrationStack
  name: pirate-quality-integration-stack
  spawn: PirateQualityIntegrationStackItem
  maxCount: 10

- type: entity
  parent: BaseItem
  id: PirateQualityIntegrationStackItem
  components:
  - type: Stack
    stackType: PirateQualityIntegrationStack
    count: 5

- type: entity
  parent: BaseItem
  id: PirateQualityIntegrationBareItem

- type: entity
  id: PirateQualityIntegrationHolder
  components:
  - type: KnowledgeHolder

- type: entity
  parent: BaseItem
  id: PirateQualityAllConsumers
  name: pirate quality integration item
  components:
  - type: Quality
    levelDeltas: {}
    quality: 2
    qualityFactors: PirateQualityIntegration
  - type: QualityOverride
    qualityOverride: 1.25
  - type: StaticPrice
    price: 100
  - type: Armor
    modifiers:
      coefficients:
        Blunt: 0.8
  - type: Clothing
    slots: [ HEAD ]
    equipDelay: 10
  - type: ExplosionResistance
    damageCoefficient: 0.7
  - type: StaminaResistance
    damageCoefficient: 0.6
  - type: DamageOtherOnHit
    damage:
      types:
        Blunt: 11
  - type: Durability
    damageProbability: 0.5
  - type: MeleeWeapon
    damage:
      types:
        Blunt: 12
  - type: Gun
    minAngle: 4
    maxAngle: 10
  - type: Projectile
    damage:
      types:
        Blunt: 14
  - type: Blocking
    passiveBlockFraction: 0.4
    activeBlockFraction: 0.9
    passiveBlockModifier:
      coefficients:
        Blunt: 0.75
      flatReductions:
        Blunt: 2
    activeBlockModifier:
      coefficients:
        Blunt: 0.5
      flatReductions:
        Blunt: 3
  - type: Destructible
    thresholds:
    - trigger:
        !type:DamageTrigger
        damage: 100
      behaviors: []
  - type: DamageOnHit
    damage:
      types:
        Blunt: 13
";

    [Test]
    public async Task EveryQualityConsumerScalesOnceWithItsOwnFactor()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var qualitySystem = server.System<QualitySystem>();

        await server.WaitAssertion(() =>
        {
            var uid = entMan.SpawnEntity("PirateQualityAllConsumers", MapCoordinates.Nullspace);
            var quality = entMan.GetComponent<QualityComponent>(uid);

            var beforeName = new RefreshNameModifiersEvent("test item");
            entMan.EventBus.RaiseLocalEvent(uid, ref beforeName);
            Assert.That(beforeName.ModifierCount, Is.Zero,
                "Unapplied quality must not modify an entity name.");

            Assert.That(qualitySystem.ApplyQuality((uid, quality)), Is.True);
            Assert.That(quality.Applied, Is.True);
            AssertScaled();

            var afterName = new RefreshNameModifiersEvent("test item");
            entMan.EventBus.RaiseLocalEvent(uid, ref afterName);
            Assert.That(afterName.ModifierCount, Is.EqualTo(1));

            Assert.That(qualitySystem.ApplyQuality((uid, quality)), Is.False,
                "Quality effects must be immutable after the first application.");
            AssertScaled();

            var factors = server.ProtoMan.Index<QualityPrototype>("PirateQualityIntegration");
            Assert.Multiple(() =>
            {
                Assert.That(factors.Price, Is.EqualTo(1.31f));
                Assert.That(entMan.GetComponent<QualityOverrideComponent>(uid).QualityOverride,
                    Is.EqualTo(1.25f));
            });

            entMan.DeleteEntity(uid);
            return;

            void AssertScaled()
            {
                var armor = entMan.GetComponent<ArmorComponent>(uid);
                var clothing = entMan.GetComponent<ClothingComponent>(uid);
                var explosion = entMan.GetComponent<ExplosionResistanceComponent>(uid);
                var stamina = entMan.GetComponent<StaminaResistanceComponent>(uid);
                var thrown = entMan.GetComponent<DamageOtherOnHitComponent>(uid);
                var durability = entMan.GetComponent<DurabilityComponent>(uid);
                var melee = entMan.GetComponent<MeleeWeaponComponent>(uid);
                var gun = entMan.GetComponent<GunComponent>(uid);
                var projectile = entMan.GetComponent<ProjectileComponent>(uid);
                var blocking = entMan.GetComponent<BlockingComponent>(uid);
                var destructible = entMan.GetComponent<DestructibleComponent>(uid);
                var selfDamage = entMan.GetComponent<DamageOnHitComponent>(uid);
                var price = entMan.GetComponent<StaticPriceComponent>(uid);

                Assert.Multiple(() =>
                {
                    Assert.That(Damage(armor.Modifiers.Coefficients, "Blunt"),
                        Is.EqualTo(0.8f * Modifier(0.82f)).Within(0.01f));
                    Assert.That(clothing.EquipDelay.TotalSeconds,
                        Is.EqualTo(10f * Modifier(0.83f)).Within(0.001f));
                    Assert.That(explosion.DamageCoefficient,
                        Is.EqualTo(0.7f * Modifier(0.84f)).Within(0.0001f));
                    Assert.That(stamina.DamageCoefficient,
                        Is.EqualTo(0.6f * Modifier(0.85f)).Within(0.0001f));
                    Assert.That(Damage(thrown.Damage.DamageDict, "Blunt"),
                        Is.EqualTo(11f * Modifier(1.07f)).Within(0.01f));
                    Assert.That(durability.DamageProbability,
                        Is.EqualTo(0.5f / Modifier(1.09f)).Within(0.0001f));
                    Assert.That(Damage(melee.Damage.DamageDict, "Blunt"),
                        Is.EqualTo(12f * Modifier(1.12f)).Within(0.01f));
                    Assert.That(gun.MinAngleModified.Degrees,
                        Is.EqualTo(4f * Modifier(0.81f)).Within(0.001f));
                    Assert.That(gun.MaxAngleModified.Degrees,
                        Is.EqualTo(10f * Modifier(0.81f)).Within(0.001f));
                    Assert.That(Damage(projectile.Damage.DamageDict, "Blunt"),
                        Is.EqualTo(14f * Modifier(1.08f)).Within(0.01f));
                    Assert.That(price.Price, Is.EqualTo(100f * Modifier(1.31f)).Within(0.001f));
                    Assert.That(blocking.PassiveBlockFraction,
                        Is.EqualTo(0.4f * Modifier(1.11f)).Within(0.0001f));
                    Assert.That(blocking.ActiveBlockFraction,
                        Is.EqualTo(0.9f * Modifier(1.11f)).Within(0.0001f));
                    Assert.That(Damage(blocking.PassiveBlockDamageModifer.Coefficients, "Blunt"),
                        Is.EqualTo(0.75f * Modifier(0.91f)).Within(0.01f));
                    Assert.That(Damage(blocking.ActiveBlockDamageModifier.Coefficients, "Blunt"),
                        Is.EqualTo(0.5f * Modifier(0.91f)).Within(0.01f));
                    Assert.That(Damage(blocking.PassiveBlockDamageModifer.FlatReduction, "Blunt"),
                        Is.EqualTo(2f * Modifier(1.11f)).Within(0.01f));
                    Assert.That(Damage(blocking.ActiveBlockDamageModifier.FlatReduction, "Blunt"),
                        Is.EqualTo(3f * Modifier(1.11f)).Within(0.01f));
                    Assert.That(((DamageTrigger) destructible.Thresholds[0].Trigger!).Damage.Float(),
                        Is.EqualTo(100f * Modifier(1.21f)).Within(0.01f));
                    Assert.That(Damage(selfDamage.Damage.DamageDict, "Blunt"),
                        Is.EqualTo(13f * Modifier(0.87f)).Within(0.01f));
                });
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task QualityTransfersSplitsAndOnlyMergesIdenticalStacks()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var qualitySystem = server.System<QualitySystem>();
        var stacks = server.System<StackSystem>();

        await server.WaitAssertion(() =>
        {
            var source = entMan.SpawnEntity("PirateQualityIntegrationStackItem", MapCoordinates.Nullspace);
            var sourceQuality = entMan.EnsureComponent<QualityComponent>(source);
            sourceQuality.LevelDeltas = new Dictionary<EntProtoId, int>
            {
                ["FabricationKnowledge"] = 2,
                ["MetalworkingKnowledge"] = 3,
            };
            sourceQuality.Quality = 2;
            sourceQuality.QualityModifiers = 4;
            sourceQuality.QualityFactors = "PirateQualityIntegration";
            Assert.That(qualitySystem.ApplyQuality((source, sourceQuality)), Is.True);

            var transferred = entMan.SpawnEntity("PirateQualityIntegrationBareItem", MapCoordinates.Nullspace);
            var transfer = new QualityTransferEvent(transferred);
            entMan.EventBus.RaiseLocalEvent(source, ref transfer);
            var transferredQuality = entMan.GetComponent<QualityComponent>(transferred);
            Assert.Multiple(() =>
            {
                Assert.That(transferredQuality.Applied, Is.True);
                Assert.That(transferredQuality.Quality, Is.EqualTo(sourceQuality.Quality));
                Assert.That(transferredQuality.QualityModifiers, Is.EqualTo(sourceQuality.QualityModifiers));
                Assert.That(transferredQuality.QualityFactors, Is.EqualTo(sourceQuality.QualityFactors));
                Assert.That(transferredQuality.LevelDeltas, Is.EquivalentTo(sourceQuality.LevelDeltas));
                Assert.That(transferredQuality.LevelDeltas, Is.Not.SameAs(sourceQuality.LevelDeltas));
            });

            transferredQuality.LevelDeltas["FabricationKnowledge"] = 99;
            Assert.That(sourceQuality.LevelDeltas["FabricationKnowledge"], Is.EqualTo(2),
                "Transferred quality requirements must not alias the source dictionary.");

            var existing = entMan.SpawnEntity("PirateQualityIntegrationBareItem", MapCoordinates.Nullspace);
            var existingQuality = entMan.EnsureComponent<QualityComponent>(existing);
            existingQuality.Quality = -4;
            existingQuality.QualityModifiers = 7;
            existingQuality.QualityFactors = "Cardboard";
            existingQuality.LevelDeltas["WeaponsKnowledge"] = 1;
            var existingTransfer = new QualityTransferEvent(existing);
            entMan.EventBus.RaiseLocalEvent(source, ref existingTransfer);
            Assert.Multiple(() =>
            {
                Assert.That(existingQuality.Quality, Is.EqualTo(-4));
                Assert.That(existingQuality.QualityModifiers, Is.EqualTo(17),
                    "An existing quality roll must receive source quality as a roll modifier.");
                Assert.That(existingQuality.QualityFactors, Is.EqualTo((ProtoId<QualityPrototype>) "Cardboard"));
                Assert.That(existingQuality.LevelDeltas,
                    Is.EquivalentTo(new Dictionary<EntProtoId, int> { ["WeaponsKnowledge"] = 1 }));
                Assert.That(existingQuality.Applied, Is.False);
            });

            var sourceStack = entMan.GetComponent<StackComponent>(source);
            var split = stacks.Split((source, sourceStack), 2, entMan.GetComponent<TransformComponent>(source).Coordinates);
            Assert.That(split, Is.Not.Null);
            var splitUid = split!.Value;
            var splitStack = entMan.GetComponent<StackComponent>(splitUid);
            var splitQuality = entMan.GetComponent<QualityComponent>(splitUid);
            Assert.Multiple(() =>
            {
                Assert.That(sourceStack.Count, Is.EqualTo(3));
                Assert.That(splitStack.Count, Is.EqualTo(2));
                Assert.That(splitQuality.Applied, Is.True);
                Assert.That(splitQuality.Quality, Is.EqualTo(sourceQuality.Quality));
                Assert.That(splitQuality.QualityModifiers, Is.EqualTo(sourceQuality.QualityModifiers));
                Assert.That(splitQuality.QualityFactors, Is.EqualTo(sourceQuality.QualityFactors));
                Assert.That(splitQuality.LevelDeltas, Is.EquivalentTo(sourceQuality.LevelDeltas));
                Assert.That(splitQuality.LevelDeltas, Is.Not.SameAs(sourceQuality.LevelDeltas));
            });

            Assert.That(stacks.TryMergeStacks((splitUid, splitStack), (source, sourceStack), out var splitTransferred),
                Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(splitTransferred, Is.EqualTo(2));
                Assert.That(sourceStack.Count, Is.EqualTo(5));
                Assert.That(splitStack.Count, Is.Zero);
            });

            AssertRejected(entMan.SpawnEntity("PirateQualityIntegrationStackItem", MapCoordinates.Nullspace),
                "A quality stack merged with a stack without quality metadata.");

            AssertRejectedWithQuality(component => component.Quality = -2,
                "Stacks with different quality levels merged.");
            AssertRejectedWithQuality(component => component.QualityModifiers++,
                "Stacks with different roll modifiers merged.");
            AssertRejectedWithQuality(component => component.QualityFactors = "Cardboard",
                "Stacks with different quality-factor prototypes merged.");
            AssertRejectedWithQuality(component => component.LevelDeltas["FabricationKnowledge"] = 99,
                "Stacks with different skill requirements merged.");
            AssertRejectedWithQuality(component => component.LevelDeltas.Remove("MetalworkingKnowledge"),
                "Stacks with different requirement counts merged.");

            var compatible = entMan.SpawnEntity("PirateQualityIntegrationStackItem", MapCoordinates.Nullspace);
            qualitySystem.CopyQuality((source, sourceQuality), compatible);
            var compatibleStack = entMan.GetComponent<StackComponent>(compatible);
            Assert.That(stacks.TryMergeStacks((compatible, compatibleStack), (source, sourceStack), out var compatibleMoved),
                Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(compatibleMoved, Is.EqualTo(5));
                Assert.That(sourceStack.Count, Is.EqualTo(10));
                Assert.That(compatibleStack.Count, Is.Zero);
            });

            entMan.DeleteEntity(existing);
            entMan.DeleteEntity(transferred);
            entMan.DeleteEntity(source);
            return;

            void AssertRejected(EntityUid candidate, string message)
            {
                var candidateStack = entMan.GetComponent<StackComponent>(candidate);
                Assert.That(stacks.TryMergeStacks((candidate, candidateStack), (source, sourceStack), out var moved),
                    Is.False, message);
                Assert.Multiple(() =>
                {
                    Assert.That(moved, Is.Zero);
                    Assert.That(candidateStack.Count, Is.EqualTo(5));
                    Assert.That(sourceStack.Count, Is.EqualTo(5));
                });
                entMan.DeleteEntity(candidate);
            }

            void AssertRejectedWithQuality(Action<QualityComponent> mutate, string message)
            {
                var candidate = entMan.SpawnEntity("PirateQualityIntegrationStackItem", MapCoordinates.Nullspace);
                qualitySystem.CopyQuality((source, sourceQuality), candidate);
                mutate(entMan.GetComponent<QualityComponent>(candidate));
                AssertRejected(candidate, message);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task QualityRollUsesSkillDeltasModifiersAndDeterministicBoundaries()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var qualitySystem = server.System<QualitySystem>();
        var knowledge = server.System<SharedKnowledgeSystem>();

        await server.WaitAssertion(() =>
        {
            var holder = entMan.SpawnEntity("PirateQualityIntegrationHolder", MapCoordinates.Nullspace);
            var store = knowledge.EnsureKnowledgeContainer(holder);
            Assert.That(knowledge.EnsureKnowledge(store, "FabricationKnowledge", 50, popup: false), Is.Not.Null);
            Assert.That(knowledge.EnsureKnowledge(store, "MetalworkingKnowledge", 25, popup: false), Is.Not.Null);
            var fabrication = knowledge.GetKnowledge(store, "FabricationKnowledge")!.Value.Comp;
            var metalworking = knowledge.GetKnowledge(store, "MetalworkingKnowledge")!.Value.Comp;
            var fabricationExperience = fabrication.Experience;
            var metalworkingExperience = metalworking.Experience;

            var skilledItem = SpawnQuality(quality: 1, modifiers: 4);
            skilledItem.Comp.LevelDeltas = new Dictionary<EntProtoId, int>
            {
                ["FabricationKnowledge"] = 0,
                ["MetalworkingKnowledge"] = 3,
            };

            var delta = qualitySystem.FindLowestDelta(store, skilledItem.Comp.LevelDeltas);
            Assert.Multiple(() =>
            {
                Assert.That(delta.Primary, Is.EqualTo((EntProtoId) "FabricationKnowledge"));
                Assert.That(delta.Lowest, Is.EqualTo((EntProtoId?) "MetalworkingKnowledge"));
                Assert.That(delta.LowestDelta, Is.EqualTo(-2));
                Assert.That(delta.RequiredMastery, Is.EqualTo(3));
            });

            Assert.That(qualitySystem.RollQuality(skilledItem, holder, roll: 5), Is.EqualTo(3),
                "50 primary skill - 30 mastery deficit + 1 base + 4 modifiers - 5 roll must map to quality 3.");
            Assert.That(skilledItem.Comp.Applied, Is.True);
            Assert.That(qualitySystem.RollQuality(skilledItem, holder, roll: 99), Is.EqualTo(3),
                "An applied quality roll must be immutable.");
            Assert.Multiple(() =>
            {
                Assert.That(fabrication.Experience, Is.EqualTo(fabricationExperience),
                    "Crafting experience was intentionally removed by the Trauma baseline.");
                Assert.That(metalworking.Experience, Is.EqualTo(metalworkingExperience),
                    "Quality rolls must not provide unlimited practical-skill experience.");
            });

            server.CfgMan.SetCVar(KnowledgeCVars.SkillsEnabled, false);
            var lowRoll = SpawnQuality();
            var highRoll = SpawnQuality();
            Assert.Multiple(() =>
            {
                Assert.That(qualitySystem.RollQuality(lowRoll, holder, roll: 0), Is.EqualTo(5),
                    "Explicit rolls below one must clamp to one.");
                Assert.That(qualitySystem.RollQuality(highRoll, holder, roll: 100), Is.EqualTo(-5),
                    "Explicit rolls above 99 must clamp to 99.");
            });
            server.CfgMan.SetCVar(KnowledgeCVars.SkillsEnabled, true);

            var noStoreUser = entMan.SpawnEntity("PirateQualityIntegrationBareItem", MapCoordinates.Nullspace);
            var noStoreItem = SpawnQuality(quality: 2);
            Assert.Multiple(() =>
            {
                Assert.That(qualitySystem.RollQuality(noStoreItem, noStoreUser, roll: 99), Is.EqualTo(2),
                    "Crafting without a knowledge store must preserve configured quality.");
                Assert.That(noStoreItem.Comp.Applied, Is.True);
            });

            var defaultPrimary = qualitySystem.FindLowestDelta(store,
                new Dictionary<EntProtoId, int> { ["KnowledgeWeaponsEnergy"] = 2 });
            Assert.Multiple(() =>
            {
                Assert.That(defaultPrimary.Primary, Is.EqualTo((EntProtoId) "FabricationKnowledge"));
                Assert.That(defaultPrimary.Lowest, Is.EqualTo((EntProtoId?) "KnowledgeWeaponsEnergy"));
                Assert.That(defaultPrimary.LowestDelta, Is.EqualTo(-2));
                Assert.That(defaultPrimary.RequiredMastery, Is.EqualTo(2));
            });

            Assert.Multiple(() =>
            {
                Assert.That(QualitySystem.QualityFromModifier(88), Is.EqualTo(5));
                Assert.That(QualitySystem.QualityFromModifier(87), Is.EqualTo(4));
                Assert.That(QualitySystem.QualityFromModifier(44), Is.EqualTo(4));
                Assert.That(QualitySystem.QualityFromModifier(43), Is.EqualTo(3));
                Assert.That(QualitySystem.QualityFromModifier(20), Is.EqualTo(3));
                Assert.That(QualitySystem.QualityFromModifier(19), Is.EqualTo(2));
                Assert.That(QualitySystem.QualityFromModifier(10), Is.EqualTo(2));
                Assert.That(QualitySystem.QualityFromModifier(9), Is.EqualTo(1));
                Assert.That(QualitySystem.QualityFromModifier(5), Is.EqualTo(1));
                Assert.That(QualitySystem.QualityFromModifier(4), Is.EqualTo(0));
                Assert.That(QualitySystem.QualityFromModifier(0), Is.EqualTo(0));
                Assert.That(QualitySystem.QualityFromModifier(-1), Is.EqualTo(-1));
                Assert.That(QualitySystem.QualityFromModifier(-5), Is.EqualTo(-1));
                Assert.That(QualitySystem.QualityFromModifier(-6), Is.EqualTo(-2));
                Assert.That(QualitySystem.QualityFromModifier(-10), Is.EqualTo(-2));
                Assert.That(QualitySystem.QualityFromModifier(-11), Is.EqualTo(-3));
                Assert.That(QualitySystem.QualityFromModifier(-20), Is.EqualTo(-3));
                Assert.That(QualitySystem.QualityFromModifier(-21), Is.EqualTo(-4));
                Assert.That(QualitySystem.QualityFromModifier(-44), Is.EqualTo(-4));
                Assert.That(QualitySystem.QualityFromModifier(-45), Is.EqualTo(-5));
                Assert.That(QualitySystem.QualityModifier(2, 1.1f), Is.EqualTo(1.21f).Within(0.0001f));
            });

            entMan.DeleteEntity(noStoreItem.Owner);
            entMan.DeleteEntity(noStoreUser);
            entMan.DeleteEntity(highRoll.Owner);
            entMan.DeleteEntity(lowRoll.Owner);
            entMan.DeleteEntity(skilledItem.Owner);
            entMan.DeleteEntity(holder);
            return;

            Entity<QualityComponent> SpawnQuality(int quality = 0, int modifiers = 0)
            {
                var uid = entMan.SpawnEntity("PirateQualityIntegrationBareItem", MapCoordinates.Nullspace);
                var component = entMan.EnsureComponent<QualityComponent>(uid);
                component.Quality = quality;
                component.QualityModifiers = modifiers;
                component.QualityFactors = "PirateQualityIntegration";
                return (uid, component);
            }
        });

        await pair.CleanReturnAsync();
    }

    private static float Modifier(float power)
        => MathF.Pow(power, 2);

    private static float Damage(IReadOnlyDictionary<string, FixedPoint2> damage, string type)
        => damage[type].Float();

    private static float Damage(IReadOnlyDictionary<string, float> damage, string type)
        => damage[type];
}
