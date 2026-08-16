// SPDX-License-Identifier: AGPL-3.0-or-later

#nullable enable

using System.Collections.Generic;
using System.Linq;
using Content.Goobstation.Maths.FixedPoint;
using Content.Goobstation.Shared.Enchanting.Components;
using Content.IntegrationTests.Tests.Interaction;
using Content.Pirate.Shared.Enchanting;
using Content.Pirate.Shared.Familiar;
using Content.Pirate.Shared.Spawners;
using Content.Server.Atmos.Components;
using Content.Server.Chemistry.Components;
using Content.Server.Construction.Conditions;
using Content.Server.Destructible;
using Content.Server.Destructible.Thresholds.Behaviors;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Spawners.Components;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Construction.Prototypes;
using Content.Shared.Construction.Steps;
using Content.Shared.Destructible.Thresholds.Triggers;
using Content.Shared.EntityTable.EntitySelectors;
using Content.Shared.Ghost.Roles.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Slippery;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;

namespace Content.IntegrationTests.Tests._Pirate;

/// <summary>
/// Covers the complete Pirate demonology loop: crafting, inks, runes and summoned demons.
/// </summary>
public sealed class DemonologyTest : InteractionTest
{
    protected override string PlayerPrototype => "DemonologyTestMob";

    [TestPrototypes]
    private const string TestPrototypes = @"
- type: entity
  id: DemonologyTestMob
  parent: InteractionTestMob
  components:
  - type: CanEnchant
";

    private sealed record InkCase(
        string Ink,
        string Enchant,
        string Target,
        params string[] AddedComponents);

    private sealed record DemonCase(
        string Id,
        int DestructionDamage,
        int SoulMin,
        int SoulMax,
        params string[] AbilityComponents);

    private static readonly InkCase[] Inks =
    [
        new("MagicInkAshartine", "EnchantSharpness", "CombatKnife"),
        new("MagicInkAsimel", "EnchantFireAspect", "CombatKnife"),
        new("MagicInkAzurapetra", "EnchantFortune", "Pickaxe"),
        new("MagicInkCatwink", "EnchantInsulated", "ClothingHandsGlovesColorYellow", "Insulated"),
        new("MagicInkEndozult", "EnchantKnockback", "CombatKnife"),
        new("MagicInkHithine", "EnchantLavaforged", "ClothingShoesColorBlack"),
        new("MagicInkHoundsgall", "EnchantProtection", "ClothingOuterArmorBasic"),
        new("MagicInkIndothine", "EnchantProtFire", "ClothingOuterArmorBasic"),
        new("MagicInkMarakat", "EnchantSlippery", "CombatKnife", "Slippery", "StepTrigger", "CollisionWake", "FixturesChange"),
        new("MagicInkNillycant", "EnchantThorns", "ClothingOuterArmorBasic"),
        new("MagicInkNocimat", "EnchantUnbreaking", "LightBulb"),
        new("MagicInkOrpimentexultant", "EnchantUnslippable", "ClothingShoesColorBlack", "NoSlip"),
        new("MagicInkPorphyrine", "EnchantElectrified", "CombatKnife", "PointLight", "Electrified", "EmitSoundOnCollide"),
        new("MagicInkPosithane", "EnchantFocus", "ClothingOuterArmorBasic", "UnholyItem", "HereticMagicItem"),
        new("MagicInkPyroendine", "EnchantLaser", "CombatKnife", "Gun", "AmmoCounter", "UseDelayOnShoot", "UseDelay", "RechargeBasicEntityAmmo", "BasicEntityAmmoProvider"),
        new("MagicInkRaggath", "EnchantMagicProtection", "WoodenBuckler", "Reflect"),
        new("MagicInkRubiginosus", "EnchantMagnetized", "ClothingShoesColorBlack", "Magboots"),
        new("MagicInkStargallink", "EnchantPoison", "CombatKnife", "SolutionContainerManager", "SolutionRegeneration", "MeleeChemicalInjector"),
        new("MagicInkTerragall", "EnchantRotten", "CombatKnife", "Perishable", "RotInto"),
        new("MagicInkUnden", "EnchantMagicSentience", "CombatKnife", "LanguageSpeaker", "LanguageKnowledge", "GhostRole", "GhostTakeoverAvailable", "TypingIndicator", "Speech"),
        new("MagicInkPerhibiate", "CurseBurning", "CombatKnife", "DamageOnHolding"),
        new("MagicInkPerinsabate", "CurseClumsy", "ClothingOuterArmorBasic", "ClothingGrantComponent"),
        new("MagicInkPerinculate", "CurseSlowing", "ClothingOuterArmorBasic", "ClothingSpeedModifier", "HeldSpeedModifier"),
        new("MagicInkPurpuraatramentum", "CurseGravity", "CombatKnife", "GravityWell"),
        new("MagicInkPyrathene", "CurseInvisibility", "CombatKnife", "Stealth", "StealthOnMove"),
        new("MagicInkUzult", "CurseUnremovable", "CombatKnife", "Unremoveable"),
        new("MagicInkYewgallink", "CurseVanishing", "CombatKnife", "VanishingCurse"),
    ];

    private static readonly DemonCase[] Demons =
    [
        new("MinorDemonGuy", 50, 0, 1, "WaddleAnimation", "MeleeChemicalInjector"),
        new("MinorDemonIncel", 100, 0, 1),
        new("MinorDemonUrist", 100, 0, 1, "Crawler", "Inventory", "Hands"),
        new("MinorDemonIan", 50, 0, 1),
        new("MinorDemonVox", 100, 0, 1, "Crawler", "Inventory", "Hands"),
        new("MinorDemonVulp", 100, 0, 1, "Crawler", "Inventory", "Hands"),
        new("MinorDemonCentifiend", 35, 1, 2, "MeleeChemicalInjector"),
        new("MinorDemonFlesh", 35, 0, 1, "Bloodstream"),

        new("MediumDemonCentifiend", 150, 1, 3, "MeleeChemicalInjector"),
        new("MediumDemonChad", 200, 1, 3),
        new("MediumDemonHamlet", 25, 1, 2, "VentCrawler", "FaxSlip"),
        new("MediumDemonMindflayer", 75, 2, 3, "MeleeChemicalInjector"),
        new("MediumDemonAbomination", 200, 1, 3, "Gun", "BatteryAmmoProvider"),
        new("MediumDemonImp", 100, 1, 2),
        new("MediumDemonDark", 100, 1, 2),

        new("MajorAngelHuman", 275, 4, 5, "ActionGrant", "MeleeChemicalInjector"),
        new("MajorAngelLizard", 400, 5, 6, "ActionGrant", "MeleeChemicalInjector"),
        new("MajorAngelMoth", 300, 4, 5, "ActionGrant"),
        new("MajorDemonBosche", 1250, 3, 5, "Devourer"),
        new("MajorDemonFeverbird", 300, 3, 5, "MovementIgnoreGravity", "MeleeChemicalInjector"),
        new("MajorDemonHanged", 300, 3, 5, "GrabIntent"),
        new("MajorDemonHiver", 100, 3, 5, "TimedSpawner"),
        new("MajorDemonSteamer", 100, 3, 5, "GunRequiresWield", "Gun", "BatteryAmmoProvider"),
        new("MajorDemonButcher", 450, 4, 6),
        new("MajorDemonGhost", 40, 3, 4),
        new("MajorDemonSaturn", 200, 4, 5, "Devourer"),
    ];

    [Test]
    public async Task CompleteDemonologyFlowWorks()
    {
        await Server.WaitAssertion(AssertConstructionAndPrototypeWiring);
        await AssertCraftingWorks();
        await AssertEveryInkWorks();
        await AssertRunesWork();
        await AssertEveryDemonWorks();
        await AssertFamiliarBindingWorks();
    }

    private void AssertConstructionAndPrototypeWiring()
    {
        var recipes = new Dictionary<string, string>
        {
            ["EnchantScroll"] = "EnchantedScroll",
            ["BloodVial"] = "DemonBlood",
            ["CraftInk"] = "RandomInk",
            ["DefaceBible"] = "DefaceBible",
            ["EnchantingRune"] = "EnchantingRune",
            ["MinorSummoning"] = "MinorSummoning",
            ["MediumSummoning"] = "MediumSummoning",
            ["MajorSummoning"] = "MajorSummoning",
        };

        foreach (var (recipeId, graphId) in recipes)
        {
            var recipe = ProtoMan.Index<ConstructionPrototype>(recipeId);
            Assert.Multiple(() =>
            {
                Assert.That(recipe.Graph.Id, Is.EqualTo(graphId), recipeId);
                Assert.That(recipe.TargetNode, Is.EqualTo("finish"), recipeId);
                Assert.That(ProtoMan.Index<ConstructionGraphPrototype>(graphId).Nodes, Does.ContainKey("finish"), graphId);
            });
        }

        var bibleEdge = ProtoMan.Index<ConstructionGraphPrototype>("DefaceBible").Edge("start", "finish");
        Assert.That(bibleEdge, Is.Not.Null);
        var bibleStep = bibleEdge!.Steps.OfType<ComponentConstructionGraphStep>().Single();
        Assert.That(bibleStep.Component, Is.EqualTo("Bible"));

        var bloodGraph = ProtoMan.Index<ConstructionGraphPrototype>("DemonBlood");
        var bloodEdge = bloodGraph.Edge("unfinished", "finish");
        Assert.That(bloodEdge, Is.Not.Null);
        var bloodCondition = bloodEdge!.Conditions.OfType<MinSolution>().Single();
        Assert.Multiple(() =>
        {
            Assert.That(bloodCondition.Solution, Is.EqualTo("drink"));
            Assert.That(bloodCondition.Reagent.Prototype, Is.EqualTo("Blood"));
            Assert.That(bloodCondition.Quantity, Is.EqualTo(FixedPoint2.New(100)));
        });

        AssertRuneBloodCost("MinorSummoning", 1);
        AssertRuneBloodCost("MediumSummoning", 2);
        AssertRuneBloodCost("MajorSummoning", 3);

        var inkSpawner = ProtoMan.Index<EntityPrototype>("RandomInkSpawner");
        Assert.That(inkSpawner.TryGetComponent<EntityTableSpawnerComponent>(out var tableSpawner, Factory));
        var pool = ((GroupSelector) tableSpawner!.Table).Children
            .Cast<EntSelector>()
            .Select(selector => selector.Id.Id)
            .ToHashSet();
        Assert.That(pool, Is.EquivalentTo(Inks.Select(ink => ink.Ink)));

        foreach (var ink in Inks)
        {
            var inkProto = ProtoMan.Index<EntityPrototype>(ink.Ink);
            Assert.That(inkProto.TryGetComponent<EnchantAdderComponent>(out var adder, Factory), ink.Ink);
            Assert.That(adder!.Enchant.Id, Is.EqualTo(ink.Enchant), ink.Ink);
            Assert.That(ProtoMan.TryIndex<EntityPrototype>(ink.Enchant, out _), ink.Enchant);
        }
    }

    private void AssertRuneBloodCost(string graphId, int expectedVials)
    {
        var edge = ProtoMan.Index<ConstructionGraphPrototype>(graphId).Edge("carved", "finish");
        Assert.That(edge, Is.Not.Null, graphId);
        Assert.That(edge!.Steps.OfType<TagConstructionGraphStep>().Count(), Is.EqualTo(expectedVials), graphId);
    }

    private async Task AssertCraftingWorks()
    {
        var paper = await Spawn("Paper");
        await Pickup(paper);
        await CraftItem("EnchantScroll");
        var scroll = await FindEntity("EnchantingScrollEmpty");
        Assert.That(SEntMan.Deleted(ToServer(paper)));
        await Delete(scroll);

        var bible = await Spawn("Bible");
        var vial = await PlaceInHands("BloodVialFull");
        await CraftItem("DefaceBible");
        var bloodBible = await FindEntity("BloodEnchanter");
        Assert.Multiple(() =>
        {
            Assert.That(SEntMan.Deleted(ToServer(bible)), "Bible was not consumed");
            Assert.That(SEntMan.Deleted(ToServer(vial)), "Blood vial was not consumed");
        });
        await Delete(bloodBible);

        var fragment = await Spawn("DemonSoulFragment");
        vial = await PlaceInHands("BloodVialFull");
        await CraftItem("CraftInk");
        await RunTicks(10);
        Target = null;

        var spawnedInk = (await DoEntityLookup())
            .Single(uid => Inks.Any(ink => PrototypeId(uid) == ink.Ink));
        Assert.Multiple(() =>
        {
            Assert.That(SEntMan.Deleted(ToServer(fragment)), "Soul fragment was not consumed");
            Assert.That(SEntMan.Deleted(ToServer(vial)), "Blood vial was not consumed");
        });
        await Delete(spawnedInk);

        var emptyVial = await Spawn("BloodVial");
        var vialUid = ToServer(emptyVial);
        var solutions = SEntMan.System<SharedSolutionContainerSystem>();
        Assert.That(solutions.TryGetSolution(vialUid, "drink", out var solutionEnt, out _));

        var condition = new MinSolution
        {
            Solution = "drink",
            Reagent = new ReagentId("Blood", [new DnaData { DNA = "demonology-test" }]),
            Quantity = FixedPoint2.New(100),
        };

        await Server.WaitPost(() =>
        {
            Assert.That(solutions.TryAddReagent(
                solutionEnt!.Value,
                new ReagentId("Blood", [new DnaData { DNA = "demonology-test" }]),
                FixedPoint2.New(99),
                out _));
            Assert.That(condition.Condition(vialUid, SEntMan), Is.False);

            Assert.That(solutions.TryAddReagent(
                solutionEnt.Value,
                new ReagentId("Blood", [new DnaData { DNA = "other-sample" }]),
                FixedPoint2.New(1),
                out _));
            Assert.That(condition.Condition(vialUid, SEntMan), Is.True);
        });
        await Delete(emptyVial);
    }

    private async Task AssertEveryInkWorks()
    {
        foreach (var test in Inks)
        {
            var scroll = await SpawnTarget("EnchantingScrollEmpty");
            var scrollUid = ToServer(scroll);
            var ink = await PlaceInHands(test.Ink);

            await Interact();

            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.Deleted(ToServer(ink)), $"{test.Ink} was not consumed");
                Assert.That(SEntMan.HasComponent<EnchanterComponent>(scrollUid), $"{test.Ink} did not prepare the scroll");
                Assert.That(SEntMan.HasComponent<EnchantedComponent>(scrollUid), Is.False,
                    $"{test.Ink} enchanted the scroll instead of preparing it");
            });

            var enchanter = SEntMan.GetComponent<EnchanterComponent>(scrollUid);
            Assert.That(enchanter.Enchants.Select(id => id.Id), Is.EqualTo(new[] { test.Enchant }), test.Ink);

            var rune = await Spawn("EnchantingRune");
            var target = await SpawnTarget(test.Target);
            var targetUid = ToServer(target);
            await PlaceInHands("BloodEnchanter");
            await Interact();
            await RunTicks(3);

            Assert.That(SEntMan.Deleted(scrollUid), $"{test.Enchant}: prepared scroll was not consumed");
            Assert.That(SEntMan.TryGetComponent(targetUid, out EnchantedComponent? enchanted), test.Enchant);
            Assert.That(enchanted!.Enchants, Has.Count.EqualTo(1), test.Enchant);

            var enchantUid = enchanted.Enchants.Single();
            Assert.That(PrototypeId(enchantUid), Is.EqualTo(test.Enchant));
            var enchant = SEntMan.GetComponent<EnchantComponent>(enchantUid);
            Assert.Multiple(() =>
            {
                Assert.That(enchant.Enchanted, Is.EqualTo(targetUid), test.Enchant);
                Assert.That(enchant.Level, Is.InRange(1, enchant.MaxLevel), test.Enchant);
            });

            await Server.WaitAssertion(() =>
            {
                var enchantProto = ProtoMan.Index<EntityPrototype>(test.Enchant);
                if (enchantProto.TryGetComponent<ComponentsEnchantComponent>(out var components, Factory) &&
                    components.Added is { } added)
                {
                    foreach (var componentName in added.Keys)
                        AssertHasComponent(targetUid, componentName, test.Enchant);
                }

                foreach (var componentName in test.AddedComponents)
                    AssertHasComponent(targetUid, componentName, test.Enchant);

                AssertEnchantRuntimeState(enchantUid, targetUid, test.Enchant);
            });

            await DeleteHeldEntity();
            await Delete(target);
            await Delete(rune);
            Target = null;
        }
    }

    private void AssertEnchantRuntimeState(EntityUid enchantUid, EntityUid targetUid, string enchantId)
    {
        switch (enchantId)
        {
            case "EnchantFireAspect":
                Assert.That(SEntMan.GetComponent<IgniteOnMeleeHitComponent>(enchantUid).FireStacks, Is.GreaterThan(0));
                break;
            case "EnchantFortune":
                Assert.That(SEntMan.GetComponent<FortuneEnchantComponent>(enchantUid).Chance, Is.GreaterThan(1f));
                break;
            case "EnchantProtection":
            case "EnchantUnbreaking":
                Assert.That(SEntMan.GetComponent<DamageModifyEnchantComponent>(enchantUid).Modifier, Is.GreaterThan(0f));
                break;
            case "EnchantSlippery":
                Assert.That(SEntMan.GetComponent<SlipperyComponent>(targetUid).SlipData.SuperSlippery);
                break;
        }
    }

    private async Task AssertRunesWork()
    {
        var runeCases = new[]
        {
            (Id: "MinorDemonRune", HostileChance: 0.25f, Demons: Demons.Take(8).Select(d => d.Id).ToHashSet()),
            (Id: "MediumDemonRune", HostileChance: 0.5f, Demons: Demons.Skip(8).Take(7).Select(d => d.Id).ToHashSet()),
            (Id: "MajorDemonRune", HostileChance: 0.5f, Demons: Demons.Skip(15).Take(11).Select(d => d.Id).ToHashSet()),
        };

        foreach (var test in runeCases)
        {
            await Server.WaitAssertion(() =>
            {
                var proto = ProtoMan.Index<EntityPrototype>(test.Id);
                Assert.That(proto.TryGetComponent<RandomDemonSpawnerComponent>(out var spawnerProto, Factory), test.Id);
                Assert.Multiple(() =>
                {
                    Assert.That(spawnerProto!.HostileChance, Is.EqualTo(test.HostileChance), test.Id);
                    Assert.That(spawnerProto.Demons.Select(id => id.Id), Is.EquivalentTo(test.Demons), test.Id);
                    Assert.That(spawnerProto.Demons.Select(id => id.Id), Is.Unique, test.Id);
                });
            });

            var rune = await Spawn(test.Id);
            var runeUid = ToServer(rune);
            var selected = SEntMan.GetComponent<GhostRoleMobSpawnerComponent>(runeUid).Prototype;
            var materialized = SEntMan.GetComponent<SpawnOnDespawnComponent>(runeUid).Prototype;

            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.GetComponent<TimedDespawnComponent>(runeUid).Lifetime, Is.InRange(59f, 60f), test.Id);
                Assert.That(selected, Is.Not.Null, test.Id);
                Assert.That(selected!.Value.Id, Is.AnyOf(test.Demons.ToArray()), test.Id);
                Assert.That(materialized.Id, Is.EqualTo(selected.Value.Id), test.Id);
            });

            await Delete(rune);
        }
    }

    private async Task AssertEveryDemonWorks()
    {
        foreach (var test in Demons)
        {
            await Server.WaitAssertion(() => AssertDemonPrototype(test));

            var demon = await Spawn(test.Id);
            var demonUid = ToServer(demon);
            foreach (var componentName in new[]
                     {
                         "MeleeWeapon", "Destructible", "MobThresholds", "WeakToHoly", "GhostRole",
                         "GhostTakeoverAvailable", "HTN", "PassiveDamage",
                     })
            {
                AssertHasComponent(demonUid, componentName, test.Id);
            }

            foreach (var componentName in test.AbilityComponents)
                AssertHasComponent(demonUid, componentName, test.Id);

            if (SEntMan.TryGetComponent(demonUid, out ActionGrantComponent? grant))
            {
                Assert.That(grant.ActionEntities, Has.Count.EqualTo(grant.Actions.Count), test.Id);
                foreach (var action in grant.ActionEntities)
                {
                    Assert.That(SEntMan.HasComponent<ActionComponent>(action), test.Id);
                    Assert.That(ProtoMan.TryIndex<EntityPrototype>(PrototypeId(action), out _), test.Id);
                }
            }

            if (SEntMan.TryGetComponent(demonUid, out Content.Shared.Devour.Components.DevourerComponent? devourer))
            {
                Assert.That(devourer.DevourActionEntity, Is.Not.Null, test.Id);
                Assert.That(SEntMan.HasComponent<ActionComponent>(devourer.DevourActionEntity!.Value), test.Id);
            }

            if (SEntMan.TryGetComponent(demonUid, out BatteryAmmoProviderComponent? ammo))
                Assert.That(ProtoMan.TryIndex<EntityPrototype>(ammo.Prototype.Id, out _), test.Id);

            if (SEntMan.TryGetComponent(demonUid, out MeleeChemicalInjectorComponent? injector))
            {
                var solutions = SEntMan.System<SharedSolutionContainerSystem>();
                Assert.That(solutions.TryGetSolution(demonUid, injector.Solution, out _, out var solution), test.Id);
                Assert.That(solution!.MaxVolume, Is.GreaterThanOrEqualTo(injector.TransferAmount),
                    $"{test.Id} injects {injector.TransferAmount} from a {solution.MaxVolume} solution");

                foreach (var reagent in solution.Contents)
                    Assert.That(ProtoMan.TryIndex<ReagentPrototype>(reagent.Reagent.Prototype, out _),
                        $"{test.Id}: {reagent.Reagent}");
            }

            AssertDemonSpecificRuntime(test.Id, demonUid);
            await Delete(demon);
        }

        var before = CountPrototype("BeeLaughterDemon");
        var hiver = await Spawn("MajorDemonHiver");
        await RunSeconds(3.2f);
        Assert.That(CountPrototype("BeeLaughterDemon"), Is.InRange(before + 5, before + 10));
        await Delete(hiver);
    }

    private void AssertDemonPrototype(DemonCase test)
    {
        var proto = ProtoMan.Index<EntityPrototype>(test.Id);
        Assert.That(proto.TryGetComponent<DestructibleComponent>(out var destructible, Factory), test.Id);
        var threshold = destructible!.Thresholds.Single(entry => entry.Trigger is DamageTrigger);
        var trigger = (DamageTrigger) threshold.Trigger!;
        var spawn = threshold.Behaviors.OfType<SpawnEntitiesBehavior>().Single();

        Assert.Multiple(() =>
        {
            Assert.That(trigger.Damage, Is.EqualTo(FixedPoint2.New(test.DestructionDamage)), test.Id);
            Assert.That(spawn.Spawn.TryGetValue("DemonSoulFragment", out var soulRange), test.Id);
            Assert.That(soulRange.Min, Is.EqualTo(test.SoulMin), test.Id);
            Assert.That(soulRange.Max, Is.EqualTo(test.SoulMax), test.Id);
        });

        if (proto.TryGetComponent<Content.Shared.Chemistry.Components.SolutionRegenerationComponent>(out var regen, Factory))
        {
            foreach (var reagent in regen!.Generated.Contents)
                Assert.That(ProtoMan.TryIndex<ReagentPrototype>(reagent.Reagent.Prototype, out _), $"{test.Id}: {reagent.Reagent}");
        }

        if (proto.TryGetComponent<ActionGrantComponent>(out var grant, Factory))
        {
            foreach (var action in grant!.Actions)
                Assert.That(ProtoMan.TryIndex<EntityPrototype>(action.Id, out _), $"{test.Id}: {action}");
        }

        if (proto.TryGetComponent<BatteryAmmoProviderComponent>(out var ammo, Factory))
            Assert.That(ProtoMan.TryIndex<EntityPrototype>(ammo!.Prototype.Id, out _), $"{test.Id}: {ammo.Prototype}");
    }

    private void AssertDemonSpecificRuntime(string id, EntityUid uid)
    {
        var melee = SEntMan.GetComponent<MeleeWeaponComponent>(uid);
        var movement = SEntMan.GetComponent<MovementSpeedModifierComponent>(uid);

        switch (id)
        {
            case "MinorDemonIncel":
                Assert.That(melee.Range, Is.EqualTo(3f));
                break;
            case "MinorDemonIan":
                Assert.Multiple(() =>
                {
                    Assert.That(melee.AutoAttack);
                    Assert.That(melee.AttackRate, Is.EqualTo(5f));
                    Assert.That(movement.BaseSprintSpeed, Is.EqualTo(7.5f));
                });
                break;
            case "MediumDemonHamlet":
                Assert.That(movement.BaseSprintSpeed, Is.EqualTo(5.5f));
                break;
            case "MediumDemonMindflayer":
                Assert.That(melee.Range, Is.EqualTo(5f));
                break;
            case "MediumDemonImp":
                Assert.That(movement.BaseSprintSpeed, Is.EqualTo(7f));
                break;
            case "MajorDemonButcher":
                Assert.Multiple(() =>
                {
                    Assert.That(melee.Range, Is.EqualTo(2f));
                    Assert.That(melee.Angle.Degrees, Is.EqualTo(45f));
                });
                break;
            case "MajorDemonFeverbird":
                Assert.That(SEntMan.HasComponent<MovementIgnoreGravityComponent>(uid));
                break;
        }
    }

    private async Task AssertFamiliarBindingWorks()
    {
        var familiar = await Spawn("MinorDemonGuy");
        var copy = await Spawn("MinorDemonFlesh");
        var familiarUid = ToServer(familiar);
        var copyUid = ToServer(copy);
        var system = SEntMan.System<FamiliarSystem>();

        await Server.WaitPost(() =>
        {
            system.SetMaster(familiarUid, SPlayer);
            Assert.That(SEntMan.GetComponent<FamiliarMasterComponent>(familiarUid).Master, Is.EqualTo(SPlayer));
            Assert.That(system.CopyMaster(familiarUid, copyUid));
            Assert.That(SEntMan.GetComponent<FamiliarMasterComponent>(copyUid).Master, Is.EqualTo(SPlayer));
        });

        await Delete(familiar);
        await Delete(copy);
    }

    private void AssertHasComponent(EntityUid uid, string componentName, string context)
    {
        Assert.That(Factory.TryGetRegistration(componentName, out var registration), $"Unknown component {componentName}");
        Assert.That(SEntMan.HasComponent(uid, registration!.Type), $"{context}: missing {componentName}");
    }

    private int CountPrototype(string prototype)
    {
        var count = 0;
        var query = SEntMan.AllEntityQueryEnumerator<MetaDataComponent>();
        while (query.MoveNext(out _, out var meta))
        {
            if (meta.EntityPrototype?.ID == prototype)
                count++;
        }

        return count;
    }

    private string? PrototypeId(EntityUid uid)
        => SEntMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID;
}
