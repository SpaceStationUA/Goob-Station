// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using Content.Goobstation.Common.Weapons.Ranged;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared._Pirate.CCVars;
using Content.Shared._Pirate.Knowledge;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Pirate.Knowledge;

[TestFixture]
public sealed class KnowledgeGameplayIntegrationTest
{
    [TestPrototypes]
    private const string TestPrototypes = @"
- type: entity
  id: PirateKnowledgeGameplayHolder
  components:
  - type: KnowledgeHolder

- type: entity
  id: PirateKnowledgeKnife
  components:
  - type: WeaponClass
    class: Knife

- type: entity
  id: PirateKnowledgeRifle
  components:
  - type: WeaponClass
    class: Rifle

- type: entity
  parent: BaseItem
  id: PirateKnowledgeDummyItem
";

    [Test]
    public async Task EveryGameplaySkillEventAppliesCurvesExperienceAndCaps()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var knowledge = server.System<SharedKnowledgeSystem>();

        await server.WaitAssertion(() =>
        {
            var holder = entMan.SpawnEntity("PirateKnowledgeGameplayHolder", MapCoordinates.Nullspace);
            var store = knowledge.EnsureKnowledgeContainer(holder);
            var gun = entMan.SpawnEntity("PirateKnowledgeDummyItem", MapCoordinates.Nullspace);

            var shooting = Ensure("ShootingKnowledge", 0);
            var firstAid = Ensure("FirstAidKnowledge", 100);
            var shield = Ensure("KnowledgeWeaponsShield", 100);
            var throwing = Ensure("ThrowingKnowledge", 100);
            var janitor = Ensure("JanitorKnowledge", 100);
            var cooking = Ensure("CookingKnowledge", 0);

            var spreadCurve = entMan.GetComponent<AimSpeedKnowledgeComponent>(shooting.Owner).Curve;
            var expectedSpreadByLevel = new (int Level, float Spread)[]
            {
                (0, 3f),
                (25, 1.9759616f),
                (26, 1f),
                (50, 1f),
                (75, 0.75f),
                (100, 0.05f),
            };

            foreach (var (level, expectedSpread) in expectedSpreadByLevel)
            {
                shooting.Comp.LearnedLevel = level;
                var spread = new GetRecoilModifiersEvent { Gun = gun, User = holder };
                entMan.EventBus.RaiseLocalEvent(holder, spread);
                Assert.Multiple(() =>
                {
                    Assert.That(spread.Modifier,
                        Is.EqualTo(1f / spreadCurve.GetCurve(level)).Within(0.0001f));
                    Assert.That(spread.Modifier, Is.EqualTo(expectedSpread).Within(0.0001f),
                        $"Marksmanship level {level} must reproduce the pre-rework random shot-angle spread.");
                });
            }

            var selfRecoil = new GetRecoilModifiersEvent { Gun = holder, User = holder };
            entMan.EventBus.RaiseLocalEvent(holder, selfRecoil);
            Assert.That(selfRecoil.Modifier, Is.EqualTo(1f), "Unarmed/self recoil must not be modified.");

            var injectCurve = entMan.GetComponent<InjectTimeKnowledgeComponent>(firstAid.Owner).Curve;
            var inject = new UserModifyInjectTimeEvent(holder, gun, TimeSpan.FromSeconds(10));
            entMan.EventBus.RaiseLocalEvent(holder, ref inject);
            Assert.That(inject.Delay.TotalSeconds,
                Is.EqualTo(10 * injectCurve.GetCurve(100)).Within(0.0001));

            var blockCurve = entMan.GetComponent<BlockFractionKnowledgeComponent>(shield.Owner).Curve;
            var block = new GetBlockFractionEvent(holder, gun, 0.5f);
            entMan.EventBus.RaiseLocalEvent(holder, ref block);
            Assert.That(block.Fraction, Is.EqualTo(0.5f * blockCurve.GetCurve(100)).Within(0.0001f));

            var speed = new ModifyThrownSpeedEvent(holder, 10f, 20f);
            entMan.EventBus.RaiseLocalEvent(holder, ref speed);
            var expectedSpeed = 10f * 0.75f * SharedKnowledgeSystem.SharpCurve(100, 200, 200);
            Assert.That(speed.BaseThrowSpeed, Is.EqualTo(expectedSpeed).Within(0.0001f));

            var insert = new ModifyThrowInsertChanceEvent(holder, 0.1f);
            entMan.EventBus.RaiseLocalEvent(holder, ref insert);
            var throwingCurve = entMan.GetComponent<ThrowInsertKnowledgeComponent>(throwing.Owner).Curve;
            var janitorCurve = entMan.GetComponent<ThrowInsertKnowledgeComponent>(janitor.Owner).Curve;
            Assert.That(insert.Chance,
                Is.EqualTo(0.1f + throwingCurve.GetCurve(100) + janitorCurve.GetCurve(100)).Within(0.0001f));

            shooting.Comp.LearnedLevel = 19;
            shooting.Comp.Experience = shooting.Comp.ExperienceCost - 1;
            shooting.Comp.TimeToNextExperience = TimeSpan.Zero;
            var shot = new AmmoShotUserEvent { Gun = gun, FiredProjectiles = [] };
            entMan.EventBus.RaiseLocalEvent(holder, shot);
            Assert.That(shooting.Comp.LearnedLevel, Is.EqualTo(20),
                "Firing XP must clamp to the shooting event's level-20 cap.");

            shooting.Comp.LearnedLevel = 9;
            shooting.Comp.Experience = shooting.Comp.ExperienceCost - 1;
            shooting.Comp.TimeToNextExperience = TimeSpan.Zero;
            var target = entMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            var projectile = entMan.SpawnEntity("BulletPistol", MapCoordinates.Nullspace);
            var hit = new ProjectileHitEvent(new DamageSpecifier(), target, holder);
            entMan.EventBus.RaiseLocalEvent(projectile, ref hit);
            Assert.That(shooting.Comp.LearnedLevel, Is.EqualTo(10),
                "Hitting a living target must clamp shooting XP to the hit event's level-10 cap.");

            cooking.Comp.LearnedLevel = 0;
            cooking.Comp.Experience = 0;
            cooking.Comp.TimeToNextExperience = TimeSpan.Zero;
            var cookedFirst = new CookedFoodEvent(holder, "PirateKnowledgeDummyItem", 1);
            entMan.EventBus.RaiseLocalEvent(holder, ref cookedFirst);
            var cookingEffect = entMan.GetComponent<ExperienceOnCookingComponent>(cooking.Owner);
            Assert.Multiple(() =>
            {
                Assert.That(cookingEffect.Limit, Is.EqualTo(1));
                Assert.That(cookingEffect.Cooked, Does.Contain((EntProtoId) "PirateKnowledgeDummyItem"));
                Assert.That(cooking.Comp.LearnedLevel, Is.EqualTo(1));
            });

            cooking.Comp.TimeToNextExperience = TimeSpan.Zero;
            var cookedDuplicate = new CookedFoodEvent(holder, "PirateKnowledgeDummyItem", 5);
            entMan.EventBus.RaiseLocalEvent(holder, ref cookedDuplicate);
            Assert.That(cookingEffect.Limit, Is.EqualTo(1), "Cooking the same result must not raise the diversity cap.");

            cooking.Comp.TimeToNextExperience = TimeSpan.Zero;
            var cookedSecond = new CookedFoodEvent(holder, "Ash", 1);
            entMan.EventBus.RaiseLocalEvent(holder, ref cookedSecond);
            Assert.Multiple(() =>
            {
                Assert.That(cookingEffect.Limit, Is.EqualTo(2));
                Assert.That(cooking.Comp.LearnedLevel, Is.EqualTo(2));
            });

            server.CfgMan.SetCVar(KnowledgeCVars.SkillsEnabled, false);
            var disabledInject = new UserModifyInjectTimeEvent(holder, gun, TimeSpan.FromSeconds(10));
            entMan.EventBus.RaiseLocalEvent(holder, ref disabledInject);
            Assert.That(disabledInject.Delay, Is.EqualTo(TimeSpan.FromSeconds(10)));
            server.CfgMan.SetCVar(KnowledgeCVars.SkillsEnabled, true);

            shooting.Comp.LearnedLevel = 0;
            shooting.Comp.Experience = 0;
            shooting.Comp.TimeToNextExperience = TimeSpan.Zero;
            server.CfgMan.SetCVar(KnowledgeCVars.SkillGain, false);
            entMan.EventBus.RaiseLocalEvent(holder,
                new AmmoShotUserEvent { Gun = gun, FiredProjectiles = [] });
            Assert.That(shooting.Comp.Experience, Is.Zero);
            server.CfgMan.SetCVar(KnowledgeCVars.SkillGain, true);

            entMan.DeleteEntity(projectile);
            entMan.DeleteEntity(target);
            entMan.DeleteEntity(gun);
            entMan.DeleteEntity(holder);
            return;

            Entity<KnowledgeComponent> Ensure(EntProtoId id, int level)
            {
                var result = knowledge.EnsureKnowledge(store, id, level, popup: false);
                Assert.That(result, Is.Not.Null, $"Could not create {id.Id}.");
                return result!.Value;
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task WeaponClassesModifyMeleeAndRecoilUsingTheirCatalogSkill()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var prototypes = server.ProtoMan;
        var knowledge = server.System<SharedKnowledgeSystem>();
        var classes = server.System<WeaponClassSystem>();

        await server.WaitAssertion(() =>
        {
            var holder = entMan.SpawnEntity("PirateKnowledgeGameplayHolder", MapCoordinates.Nullspace);
            var store = knowledge.EnsureKnowledgeContainer(holder);
            Assert.That(knowledge.EnsureKnowledge(store, "KnowledgeWeaponsShortBlade", 100, popup: false), Is.Not.Null);
            var weapon = entMan.SpawnEntity("PirateKnowledgeKnife", MapCoordinates.Nullspace);
            var component = entMan.GetComponent<WeaponClassComponent>(weapon);
            var prototype = prototypes.Index<WeaponClassPrototype>(component.Class);
            var blunt = prototypes.Index<DamageTypePrototype>("Blunt");

            var damage = new DamageSpecifier(blunt, 10);
            var melee = new GetMeleeDamageEvent(weapon, damage, new List<DamageModifierSet>(), holder);
            entMan.EventBus.RaiseLocalEvent(weapon, ref melee);
            Assert.That(melee.Damage.DamageDict["Blunt"].Float(),
                Is.EqualTo(10f * prototype.MeleeDamage.GetCurve(100)).Within(0.01f));

            var recoil = new GetRecoilModifiersEvent { Gun = weapon, User = holder };
            entMan.EventBus.RaiseLocalEvent(weapon, recoil);
            Assert.That(recoil.Modifier, Is.EqualTo(1f / prototype.AimSpeed.GetCurve(100)).Within(0.0001f));

            var selfRecoil = new GetRecoilModifiersEvent { Gun = weapon, User = weapon };
            entMan.EventBus.RaiseLocalEvent(weapon, selfRecoil);
            Assert.That(selfRecoil.Modifier, Is.EqualTo(1f));

            Assert.Multiple(() =>
            {
                Assert.That(classes.GetSkillLevel((weapon, component), holder), Is.EqualTo(100));
                Assert.That(classes.IsUnarmed(holder, holder), Is.True);
                Assert.That(classes.IsUnarmed(holder, weapon), Is.False);
            });

            component.Class = WeaponClassSystem.Unarmed;
            Assert.That(classes.IsUnarmed(holder, weapon), Is.True);

            var shooting = knowledge.EnsureKnowledge(store, "ShootingKnowledge", 0, popup: false);
            var rifleSkill = knowledge.EnsureKnowledge(store, "KnowledgeWeaponsRifle", 0, popup: false);
            Assert.Multiple(() =>
            {
                Assert.That(shooting, Is.Not.Null);
                Assert.That(rifleSkill, Is.Not.Null);
            });

            var rifle = entMan.SpawnEntity("PirateKnowledgeRifle", MapCoordinates.Nullspace);
            var rifleComponent = entMan.GetComponent<WeaponClassComponent>(rifle);
            var riflePrototype = prototypes.Index<WeaponClassPrototype>(rifleComponent.Class);

            // GunSystem's shot-angle calculation raises the same event on the user and then on the gun.
            // Keep both dispatches here so a broken one cannot make an AKM's skill appear inert.
            var lowRecoil = new GetRecoilModifiersEvent { Gun = rifle, User = holder };
            entMan.EventBus.RaiseLocalEvent(holder, lowRecoil);
            entMan.EventBus.RaiseLocalEvent(rifle, lowRecoil);
            var shootingCurve = entMan.GetComponent<AimSpeedKnowledgeComponent>(shooting!.Value.Owner).Curve;
            Assert.Multiple(() =>
            {
                Assert.That(1f / shootingCurve.GetCurve(0), Is.EqualTo(3f).Within(0.0001f));
                Assert.That(lowRecoil.Modifier,
                    Is.EqualTo(1f / shootingCurve.GetCurve(0) / riflePrototype.AimSpeed.GetCurve(0)).Within(0.0001f));
            });

            shooting.Value.Comp.LearnedLevel = 100;
            rifleSkill!.Value.Comp.LearnedLevel = 100;
            var highRecoil = new GetRecoilModifiersEvent { Gun = rifle, User = holder };
            entMan.EventBus.RaiseLocalEvent(holder, highRecoil);
            entMan.EventBus.RaiseLocalEvent(rifle, highRecoil);
            Assert.Multiple(() =>
            {
                Assert.That(1f / shootingCurve.GetCurve(100), Is.EqualTo(0.05f).Within(0.0001f));
                Assert.That(highRecoil.Modifier,
                    Is.EqualTo(1f / shootingCurve.GetCurve(100) / riflePrototype.AimSpeed.GetCurve(100)).Within(0.0001f));
                Assert.That(highRecoil.Modifier, Is.LessThan(lowRecoil.Modifier),
                    "Higher general and rifle skills must reduce the shot-spread modifier.");
            });

            entMan.DeleteEntity(rifle);
            entMan.DeleteEntity(weapon);
            entMan.DeleteEntity(holder);
        });

        await pair.CleanReturnAsync();
    }
}
