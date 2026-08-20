// SPDX-License-Identifier: AGPL-3.0-or-later

#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Goobstation.Common.Devour;
using Content.Goobstation.Common.Grab;
using Content.Goobstation.Maths.FixedPoint;
using Content.Goobstation.Shared.Clothing.Components;
using Content.Goobstation.Shared.Enchanting.Components;
using Content.Goobstation.Shared.Enchanting.Systems;
using Content.Goobstation.Shared.GrabIntent;
using Content.Goobstation.Shared.Religion;
using Content.Goobstation.Shared.Religion.Nullrod;
using Content.IntegrationTests.Tests.Interaction;
using Content.Pirate.Shared.Enchanting;
using Content.Pirate.Shared.Demonology;
using Content.Pirate.Shared.Familiar;
using Content.Pirate.Shared.Spawners;
using Content.Server.Atmos.Components;
using Content.Server.Chemistry.Components;
using Content.Server.Construction.Conditions;
using Content.Server.Destructible;
using Content.Server.Destructible.Thresholds.Behaviors;
using Content.Server.Electrocution;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Server.NPC.HTN;
using Content.Server.Singularity.Components;
using Content.Server.Singularity.EntitySystems;
using Content.Server.Spawners.Components;
using Content.Server.Weapons.Melee;
using Content.Shared._EinsteinEngines.Language.Systems;
using Content.Shared._Goobstation.Wizard;
using Content.Shared._Goobstation.Wizard.FadingTimedDespawn;
using Content.Shared._Starlight.VentCrawling;
using Content.Shared._Starlight.VentCrawling.Components;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Clothing;
using Content.Shared.Construction.Prototypes;
using Content.Shared.Construction.Steps;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Destructible.Thresholds.Triggers;
using Content.Shared.Devour;
using Content.Shared.Devour.Components;
using Content.Shared.Electrocution;
using Content.Shared.EntityTable.EntitySelectors;
using Content.Shared.Gravity;
using Content.Shared.Ghost.Roles.Components;
using Content.Shared.Heretic;
using Content.Shared.Inventory;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Item;
using Content.Shared.Interaction.Components;
using Content.Shared.Magic.Events;
using Content.Shared.Mining.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Physics;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Projectiles;
using Content.Shared.Slippery;
using Content.Shared.Speech.Components;
using Content.Shared.Stealth.Components;
using Content.Shared.StepTrigger.Components;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.Stunnable;
using Content.Shared.Temperature;
using Content.Shared.Throwing;
using Content.Shared.Verbs;
using Content.Shared.VentCrawler.Tube.Components;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Components;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Reflect;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
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
  parent: [ InteractionTestMob, MobBloodstream ]
  components:
  - type: CanEnchant
  - type: Damageable
  - type: MobState
  - type: MovementSpeedModifier
  - type: Sprite

- type: entity
  id: DemonologyTestOre
  components:
  - type: OreVein

- type: entity
  id: DemonologyTestItem
  parent: BaseItem

- type: entity
  id: DemonologyTestMobItem
  parent: BaseItem
  components:
  - type: MobState

- type: entity
  id: DemonologyTestVictim
  parent: [ InteractionTestMob, MobBloodstream ]
  components:
  - type: Damageable
  - type: MobState
  - type: MovementSpeedModifier
  - type: Sprite
  - type: Physics
    bodyType: KinematicController
  - type: Pullable
  - type: Grabbable
  - type: StandingState
  - type: Crawler
  - type: Stamina
    baseCritThreshold: 2000
  - type: Fixtures
    fixtures:
      fix1:
        shape:
          !type:PhysShapeCircle
          radius: 0.35
        density: 50
        mask:
        - MobMask
        layer:
        - MobLayer

- type: entity
  id: DemonologyTestStructure
  parent: BaseStructure
  components:
  - type: Damageable

- type: entity
  id: DemonologyTestPoisonBlocked
  parent: CombatKnife
  components:
  - type: SolutionContainerManager

- type: entity
  id: DemonologyTestClothingOnly
  parent: BaseItem
  components:
  - type: Clothing
    slots:
    - outerClothing

- type: entity
  id: DemonologyTestArmorOnly
  parent: BaseItem
  components:
  - type: Armor
    modifiers:
      coefficients: {}

- type: entity
  id: DemonologyTestBosche
  parent: MajorDemonBosche
  components:
  - type: Devourer
    devourTime: 0
    structureDevourTime: 0

- type: entity
  id: DemonologyTestSaturn
  parent: MajorDemonSaturn
  components:
  - type: Devourer
    devourTime: 0
    structureDevourTime: 0
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

    private static readonly IReadOnlyDictionary<string, int> InkMaxLevels =
        new Dictionary<string, int>
        {
            ["EnchantSharpness"] = 5,
            ["EnchantFireAspect"] = 2,
            ["EnchantFortune"] = 3,
            ["EnchantInsulated"] = 1,
            ["EnchantKnockback"] = 2,
            ["EnchantLavaforged"] = 1,
            ["EnchantProtection"] = 4,
            ["EnchantProtFire"] = 4,
            ["EnchantSlippery"] = 4,
            ["EnchantThorns"] = 3,
            ["EnchantUnbreaking"] = 3,
            ["EnchantUnslippable"] = 1,
            ["EnchantElectrified"] = 1,
            ["EnchantFocus"] = 1,
            ["EnchantLaser"] = 1,
            ["EnchantMagicProtection"] = 1,
            ["EnchantMagnetized"] = 1,
            ["EnchantPoison"] = 1,
            ["EnchantRotten"] = 1,
            ["EnchantMagicSentience"] = 1,
            ["CurseBurning"] = 1,
            ["CurseClumsy"] = 1,
            ["CurseSlowing"] = 1,
            ["CurseGravity"] = 1,
            ["CurseInvisibility"] = 1,
            ["CurseUnremovable"] = 1,
            ["CurseVanishing"] = 1,
        };

    private static readonly HashSet<string> GenericItemEnchants =
    [
        "EnchantSlippery",
        "EnchantElectrified",
        "EnchantLaser",
        "EnchantRotten",
        "EnchantMagicSentience",
        "CurseBurning",
        "CurseSlowing",
        "CurseGravity",
        "CurseInvisibility",
        "CurseUnremovable",
        "CurseVanishing",
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
        new("MediumDemonHamlet", 25, 1, 2, "VentCrawler", "FaxSlip", "DemonTripOnHit"),
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

    private static readonly IReadOnlyDictionary<string, (string Type, float Amount)[]> DemonMeleeDamage =
        new Dictionary<string, (string Type, float Amount)[]>
        {
            ["MinorDemonGuy"] = [("Slash", 5f)],
            ["MinorDemonIncel"] = [("Blunt", 5f), ("Slash", 5f)],
            ["MinorDemonUrist"] = [("Blunt", 5f)],
            ["MinorDemonIan"] = [("Slash", 2f)],
            ["MinorDemonVox"] = [("Blunt", 5f)],
            ["MinorDemonVulp"] = [("Slash", 5f)],
            ["MinorDemonCentifiend"] = [("Piercing", 1f)],
            ["MinorDemonFlesh"] = [("Blunt", 3f)],
            ["MediumDemonCentifiend"] = [("Piercing", 1f)],
            ["MediumDemonChad"] = [("Blunt", 20f), ("Structural", 50f)],
            ["MediumDemonHamlet"] = [("Slash", 10f)],
            ["MediumDemonMindflayer"] = [("Asphyxiation", 15f), ("Bloodloss", 5f), ("Ion", 20f)],
            ["MediumDemonAbomination"] = [("Blunt", 13f)],
            ["MediumDemonImp"] = [("Piercing", 12f)],
            ["MediumDemonDark"] = [("Slash", 10f), ("Asphyxiation", 5f)],
            ["MajorAngelHuman"] = [("Caustic", 20f), ("Blunt", 10f), ("Structural", 20f)],
            ["MajorAngelLizard"] = [("Slash", 20f), ("Piercing", 5f)],
            ["MajorAngelMoth"] = [("Piercing", 10f)],
            ["MajorDemonBosche"] = [("Blunt", 6f)],
            ["MajorDemonFeverbird"] = [("Slash", 5f)],
            ["MajorDemonHanged"] = [("Blunt", 5f)],
            ["MajorDemonHiver"] = [("Blunt", 15f)],
            ["MajorDemonSteamer"] = [("Blunt", 15f)],
            ["MajorDemonButcher"] = [("Slash", 30f), ("Structural", 50f)],
            ["MajorDemonGhost"] = [("Cold", 15f)],
            ["MajorDemonSaturn"] = [("Blunt", 20f), ("Slash", 15f), ("Structural", 30f)],
        };

    private static readonly IReadOnlyDictionary<string, (string Reagent, float Amount)[]> DemonInjectedReagents =
        new Dictionary<string, (string Reagent, float Amount)[]>
        {
            ["MinorDemonGuy"] = [("Honk", 7f)],
            ["MinorDemonCentifiend"] = [("UnstableMutagen", 1f)],
            ["MediumDemonCentifiend"] = [("UnstableMutagen", 3f), ("Mold", 5f)],
            ["MediumDemonMindflayer"] = [("ChloralHydrate", 7f)],
            ["MajorAngelHuman"] = [("Gold", 50f), ("Hemorrhinol", 7f)],
            ["MajorAngelLizard"] = [("Impedrezene", 6f)],
            ["MajorDemonFeverbird"] =
            [
                ("MindbreakerToxin", 1f),
                ("Tazinide", 5f),
                ("Fentanyl", 1f),
                ("Happiness", 10f),
            ],
        };

    private static readonly IReadOnlyDictionary<string, string[]> DemonActions =
        new Dictionary<string, string[]>
        {
            ["MajorAngelHuman"] = ["ActionDemonRift", "ActionDemonRepulse"],
            ["MajorAngelLizard"] = ["ActionDemonRift"],
            ["MajorAngelMoth"] = ["ActionDemonRepulse"],
        };

    [Test]
    public async Task CompleteDemonologyFlowWorks()
    {
        await Server.WaitAssertion(AssertConstructionAndPrototypeWiring);
        await AssertCraftingWorks();
        await AssertEveryInkWorks();
        await AssertInkRestrictionsAndLimits();
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
            Assert.That(ProtoMan.TryIndex<EntityPrototype>(ink.Enchant, out var enchantProto), ink.Enchant);
            Assert.That(enchantProto!.TryGetComponent<EnchantComponent>(out var enchant, Factory), ink.Enchant);
            Assert.That(enchant!.MaxLevel, Is.EqualTo(InkMaxLevels[ink.Enchant]), ink.Enchant);
        }

        Assert.That(InkMaxLevels.Keys, Is.EquivalentTo(Inks.Select(ink => ink.Enchant)));
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
            await RunSeconds(0.6f);

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

            await AssertEnchantBehavior(enchantUid, targetUid, test.Enchant);

            await DeleteHeldEntity();
            await Delete(target);
            await Delete(rune);
            Target = null;
        }
    }

    private async Task AssertInkRestrictionsAndLimits()
    {
        var enchanting = SEntMan.System<EnchantingSystem>();

        foreach (var test in Inks)
        {
            var valid = await Spawn(test.Target);
            var validUid = ToServer(valid);
            var generic = await Spawn("DemonologyTestItem");
            var genericUid = ToServer(generic);

            await Server.WaitAssertion(() =>
            {
                Assert.That(enchanting.CanEnchant(validUid, test.Enchant), Is.True,
                    $"{test.Enchant}: wiki-valid target was rejected");
                Assert.That(enchanting.CanEnchant(genericUid, test.Enchant),
                    Is.EqualTo(GenericItemEnchants.Contains(test.Enchant)),
                    $"{test.Enchant}: generic item restriction differs from the wiki");

                Assert.That(enchanting.Enchant(validUid, test.Enchant, 999), Is.True, test.Enchant);
                var enchanted = SEntMan.GetComponent<EnchantedComponent>(validUid);
                var applied = enchanting.FindEnchant(enchanted, test.Enchant);
                Assert.That(applied, Is.Not.Null, test.Enchant);
                Assert.Multiple(() =>
                {
                    Assert.That(applied!.Value.Comp.Level, Is.EqualTo(InkMaxLevels[test.Enchant]), test.Enchant);
                    Assert.That(applied.Value.Comp.IsMaxed, Is.True, test.Enchant);
                    Assert.That(enchanting.CanEnchant(validUid, test.Enchant), Is.False,
                        $"{test.Enchant}: max-level enchant could be upgraded again");
                });
            });

            await Delete(valid);
            await Delete(generic);
        }

        await AssertIncompatibleEnchants(enchanting,
            "ClothingHandsGlovesColorYellow",
            "EnchantBudgetInsulated",
            "EnchantInsulated");
        await AssertIncompatibleEnchants(enchanting,
            "ClothingOuterArmorBasic",
            "EnchantProtection",
            "EnchantProtFire");
        await AssertIncompatibleEnchants(enchanting,
            "DemonologyTestMobItem",
            "EnchantSlippery",
            "EnchantUnslippable");

        await AssertEnchantRejected(enchanting, "EnchantSlippery", "ClothingShoesColorBlack");
        await AssertEnchantRejected(enchanting, "EnchantUnslippable", "ClothingShoesBootsMag");
        await AssertEnchantRejected(enchanting, "EnchantPoison", "DemonologyTestPoisonBlocked");

        foreach (var enchant in new[] { "EnchantRotten", "CurseUnremovable", "CurseVanishing" })
            await AssertEnchantRejected(enchanting, enchant, "ClothingShoesBootsMagAdv");

        foreach (var enchant in new[] { "EnchantProtection", "EnchantProtFire", "EnchantFocus", "CurseClumsy" })
        {
            await AssertEnchantRejected(enchanting, enchant, "DemonologyTestClothingOnly");
            await AssertEnchantRejected(enchanting, enchant, "DemonologyTestArmorOnly");
        }
    }

    private async Task AssertIncompatibleEnchants(
        EnchantingSystem enchanting,
        string targetPrototype,
        string first,
        string second)
    {
        foreach (var (applied, rejected) in new[] { (first, second), (second, first) })
        {
            var target = await Spawn(targetPrototype);
            var targetUid = ToServer(target);
            await Server.WaitAssertion(() =>
            {
                Assert.That(enchanting.Enchant(targetUid, applied), Is.True, applied);
                Assert.That(enchanting.CanEnchant(targetUid, rejected), Is.False,
                    $"{applied} must be incompatible with {rejected}");
                Assert.That(enchanting.Enchant(targetUid, rejected), Is.False,
                    $"{rejected} was applied despite {applied}");
            });
            await Delete(target);
        }
    }

    private async Task AssertEnchantRejected(EnchantingSystem enchanting, string enchant, string targetPrototype)
    {
        var target = await Spawn(targetPrototype);
        var targetUid = ToServer(target);
        await Server.WaitAssertion(() =>
        {
            Assert.That(enchanting.CanEnchant(targetUid, enchant), Is.False,
                $"{enchant} accepted forbidden target {targetPrototype}");
            Assert.That(enchanting.Enchant(targetUid, enchant), Is.False,
                $"{enchant} was applied to forbidden target {targetPrototype}");
        });
        await Delete(target);
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

        AssertEnchantExactRuntimeState(enchantUid, targetUid, enchantId);
    }

    private void AssertEnchantExactRuntimeState(EntityUid enchantUid, EntityUid targetUid, string enchantId)
    {
        var level = SEntMan.GetComponent<EnchantComponent>(enchantUid).Level;
        switch (enchantId)
        {
            case "EnchantSharpness":
            {
                var damage = SEntMan.GetComponent<BonusDamageEnchantComponent>(enchantUid).Damage;
                Assert.That(damage.DamageDict["Slash"], Is.EqualTo(FixedPoint2.New(4 * level)));
                break;
            }
            case "EnchantFireAspect":
                Assert.That(SEntMan.GetComponent<IgniteOnMeleeHitComponent>(enchantUid).FireStacks,
                    Is.EqualTo(0.5f * level).Within(0.001f));
                break;
            case "EnchantFortune":
            {
                var fortune = SEntMan.GetComponent<FortuneEnchantComponent>(enchantUid);
                Assert.That(fortune.Chance, Is.EqualTo(1f + fortune.BaseChance * level).Within(0.001f));
                break;
            }
            case "EnchantInsulated":
                Assert.That(SEntMan.GetComponent<InsulatedComponent>(targetUid).Coefficient, Is.EqualTo(0f));
                break;
            case "EnchantKnockback":
            {
                var knockback = SEntMan.GetComponent<MeleeThrowOnHitComponent>(enchantUid);
                Assert.Multiple(() =>
                {
                    Assert.That(knockback.Distance, Is.EqualTo(level).Within(0.001f));
                    Assert.That(knockback.Speed, Is.EqualTo(1.5f * level).Within(0.001f));
                });
                break;
            }
            case "EnchantLavaforged":
                Assert.That(SEntMan.GetComponent<LavaImmunityEnchantComponent>(enchantUid).Group.Types!.Select(x => x.Id),
                    Does.Contain("Lava"));
                break;
            case "EnchantProtection":
            case "EnchantUnbreaking":
            {
                var damage = SEntMan.GetComponent<DamageModifyEnchantComponent>(enchantUid);
                Assert.That(damage.Modifier, Is.EqualTo(MathF.Pow(damage.Factor, level)).Within(0.001f));
                Assert.That(damage.ProtectWearer, Is.EqualTo(enchantId == "EnchantProtection"));
                break;
            }
            case "EnchantProtFire":
            {
                var fire = SEntMan.GetComponent<FireProtEnchantComponent>(enchantUid);
                Assert.Multiple(() =>
                {
                    Assert.That(fire.Reduction, Is.EqualTo(0.1f * level).Within(0.001f));
                    Assert.That(fire.TempModifier, Is.EqualTo(MathF.Pow(0.1f, level)).Within(0.001f));
                });
                break;
            }
            case "EnchantSlippery":
            {
                var slippery = SEntMan.GetComponent<SlipperyComponent>(targetUid);
                var factor = 1.25f * level;
                Assert.Multiple(() =>
                {
                    Assert.That(slippery.SlipData.SuperSlippery);
                    Assert.That(slippery.SlipData.StunTime.TotalSeconds, Is.EqualTo(1.5f * factor).Within(0.001f));
                    Assert.That(slippery.SlipData.LaunchForwardsMultiplier, Is.EqualTo(1.5f * factor).Within(0.001f));
                });
                break;
            }
            case "EnchantThorns":
            {
                var thorns = SEntMan.GetComponent<DamageOnAttackedComponent>(enchantUid).Damage;
                Assert.That(thorns.DamageDict["Slash"], Is.EqualTo(FixedPoint2.New(4 * level)));
                break;
            }
            case "EnchantUnslippable":
                Assert.That(SEntMan.HasComponent<NoSlipComponent>(targetUid));
                break;
            case "EnchantElectrified":
            {
                var electrified = SEntMan.GetComponent<ElectrifiedComponent>(targetUid);
                Assert.Multiple(() =>
                {
                    Assert.That(electrified.ShockDamage, Is.EqualTo(2f).Within(0.001f));
                    Assert.That(electrified.ShockTime, Is.EqualTo(0.8f).Within(0.001f));
                    Assert.That(electrified.RequirePower, Is.False);
                    Assert.That(electrified.OnBump);
                });
                break;
            }
            case "EnchantFocus":
                Assert.Multiple(() =>
                {
                    Assert.That(SEntMan.HasComponent<UnholyItemComponent>(targetUid));
                    Assert.That(SEntMan.HasComponent<HereticMagicItemComponent>(targetUid));
                });
                break;
            case "EnchantLaser":
            {
                var gun = SEntMan.GetComponent<GunComponent>(targetUid);
                var ammo = SEntMan.GetComponent<BasicEntityAmmoProviderComponent>(targetUid);
                var recharge = SEntMan.GetComponent<RechargeBasicEntityAmmoComponent>(targetUid);
                var delay = SEntMan.GetComponent<Content.Shared.Timing.UseDelayComponent>(targetUid);
                Assert.Multiple(() =>
                {
                    Assert.That(gun.UseKey, Is.False);
                    Assert.That(gun.FireRate, Is.EqualTo(0.5f).Within(0.001f));
                    Assert.That(ammo.Capacity, Is.EqualTo(1));
                    Assert.That(ammo.Count, Is.EqualTo(1));
                    Assert.That(ammo.Proto, Is.EqualTo("BulletLaser"));
                    Assert.That(recharge.RechargeCooldown, Is.EqualTo(1.4f).Within(0.001f));
                    Assert.That(delay.Delay.TotalSeconds, Is.EqualTo(1.5f).Within(0.001f));
                });
                break;
            }
            case "EnchantMagicProtection":
            {
                var reflect = SEntMan.GetComponent<ReflectComponent>(targetUid);
                Assert.Multiple(() =>
                {
                    Assert.That(reflect.Reflects, Is.EqualTo(ReflectType.Magic));
                    Assert.That(reflect.ReflectProb, Is.EqualTo(1f).Within(0.001f));
                });
                break;
            }
            case "EnchantMagnetized":
                Assert.That(SEntMan.GetComponent<MagbootsComponent>(targetUid).Slot, Is.EqualTo("shoes"));
                break;
            case "EnchantPoison":
            {
                var solutions = SEntMan.System<SharedSolutionContainerSystem>();
                var injector = SEntMan.GetComponent<MeleeChemicalInjectorComponent>(targetUid);
                var regeneration = SEntMan.GetComponent<SolutionRegenerationComponent>(targetUid);
                Assert.That(injector.TransferAmount, Is.EqualTo(FixedPoint2.New(1)));
                Assert.That(solutions.TryGetSolution(targetUid, "melee", out _, out var meleeSolution));
                Assert.Multiple(() =>
                {
                    Assert.That(meleeSolution!.MaxVolume, Is.EqualTo(FixedPoint2.New(1)));
                    Assert.That(regeneration.SolutionName, Is.EqualTo("melee"));
                    Assert.That(regeneration.Generated.Contents.Single().Reagent.Prototype, Is.EqualTo("Amatoxin"));
                    Assert.That(regeneration.Generated.Contents.Single().Quantity, Is.EqualTo(FixedPoint2.New(0.1)));
                });
                break;
            }
            case "EnchantRotten":
            {
                var perishable = SEntMan.GetComponent<PerishableComponent>(targetUid);
                var rotInto = SEntMan.GetComponent<RotIntoComponent>(targetUid);
                Assert.Multiple(() =>
                {
                    Assert.That(perishable.RotAfter, Is.EqualTo(TimeSpan.FromSeconds(300)));
                    Assert.That(perishable.MolsPerSecondPerUnitMass, Is.EqualTo(0f));
                    Assert.That(rotInto.Entity, Is.EqualTo("MobLivingCurse"));
                    Assert.That(rotInto.Stage, Is.EqualTo(0));
                });
                break;
            }
            case "EnchantMagicSentience":
            {
                var knowledge = SEntMan.GetComponent<Content.Server._EinsteinEngines.Language.LanguageKnowledgeComponent>(targetUid);
                var ghostRole = SEntMan.GetComponent<GhostRoleComponent>(targetUid);
                Assert.Multiple(() =>
                {
                    Assert.That(knowledge.SpokenLanguages.Select(x => x.Id), Does.Contain("TauCetiBasic"));
                    Assert.That(knowledge.UnderstoodLanguages.Select(x => x.Id), Does.Contain("TauCetiBasic"));
                    Assert.That(ghostRole.MindRoles.Select(x => x.Id), Does.Contain("MindRoleGhostRoleFamiliar"));
                });
                break;
            }
            case "CurseBurning":
            {
                var holderDamage = SEntMan.GetComponent<DamageOnHoldingComponent>(targetUid);
                Assert.Multiple(() =>
                {
                    Assert.That(holderDamage.Enabled);
                    Assert.That(holderDamage.Damage.DamageDict["Heat"], Is.EqualTo(FixedPoint2.New(5)));
                });
                break;
            }
            case "CurseClumsy":
            {
                var grants = SEntMan.GetComponent<ClothingGrantComponentComponent>(targetUid).Components;
                Assert.That(grants.Keys, Is.EquivalentTo(new[] { "Clumsy", "SleepEmitSound", "SpeakFontOverride" }));
                var clumsy = (Content.Shared.Clumsy.ClumsyComponent) grants["Clumsy"].Component;
                var font = (Content.Goobstation.Shared.SpeakFontOverride.SpeakFontOverrideComponent) grants["SpeakFontOverride"].Component;
                Assert.Multiple(() =>
                {
                    Assert.That(clumsy.GunShootFailDamage!.DamageDict["Blunt"], Is.EqualTo(FixedPoint2.New(5)));
                    Assert.That(clumsy.GunShootFailDamage.DamageDict["Piercing"], Is.EqualTo(FixedPoint2.New(4)));
                    Assert.That(clumsy.GunShootFailDamage.DamageDict["Heat"], Is.EqualTo(FixedPoint2.New(3)));
                    Assert.That(font.Enabled);
                    Assert.That(font.FontId, Is.EqualTo("LDFComicSans"));
                });
                break;
            }
            case "CurseSlowing":
            {
                var clothing = SEntMan.GetComponent<ClothingSpeedModifierComponent>(targetUid);
                var held = SEntMan.GetComponent<HeldSpeedModifierComponent>(targetUid);
                Assert.Multiple(() =>
                {
                    Assert.That(clothing.WalkModifier, Is.EqualTo(0.9f).Within(0.001f));
                    Assert.That(clothing.SprintModifier, Is.EqualTo(0.9f).Within(0.001f));
                    Assert.That(held.MirrorClothingModifier);
                });
                break;
            }
            case "CurseGravity":
            {
                var gravity = SEntMan.GetComponent<GravityWellComponent>(targetUid);
                Assert.Multiple(() =>
                {
                    Assert.That(gravity.MaxRange, Is.EqualTo(1.5f).Within(0.001f));
                    Assert.That(gravity.BaseRadialAcceleration, Is.EqualTo(3f).Within(0.001f));
                });
                break;
            }
            case "CurseInvisibility":
            {
                var stealth = SEntMan.GetComponent<StealthComponent>(targetUid);
                var move = SEntMan.GetComponent<StealthOnMoveComponent>(targetUid);
                Assert.Multiple(() =>
                {
                    Assert.That(stealth.HadOutline);
                    Assert.That(move.PassiveVisibilityRate, Is.EqualTo(-0.1f).Within(0.001f));
                    Assert.That(move.MovementVisibilityRate, Is.EqualTo(0.1f).Within(0.001f));
                });
                break;
            }
            case "CurseUnremovable":
                Assert.That(SEntMan.GetComponent<UnremoveableComponent>(targetUid).DeleteOnDrop);
                break;
            case "CurseVanishing":
            {
                var curse = SEntMan.GetComponent<VanishingCurseComponent>(targetUid);
                Assert.Multiple(() =>
                {
                    Assert.That(curse.Lifetime, Is.EqualTo(1f).Within(0.001f));
                    Assert.That(curse.FadeOutTime, Is.EqualTo(180f).Within(0.001f));
                });
                break;
            }
            default:
                Assert.Fail($"{enchantId}: missing exact runtime-state assertion");
                break;
        }
    }

    private async Task AssertEnchantBehavior(EntityUid enchantUid, EntityUid targetUid, string enchantId)
    {
        switch (enchantId)
        {
            case "EnchantSharpness":
                await Server.WaitAssertion(() =>
                {
                    var ev = CreateMeleeHit(targetUid, SPlayer);
                    SEntMan.EventBus.RaiseLocalEvent(targetUid, ev);
                    Assert.That(ev.BonusDamage.DamageDict["Slash"],
                        Is.EqualTo(SEntMan.GetComponent<BonusDamageEnchantComponent>(enchantUid).Damage.DamageDict["Slash"]));
                });
                break;
            case "EnchantFireAspect":
            {
                var victim = await Spawn("MobHuman");
                var victimUid = ToServer(victim);
                await Server.WaitAssertion(() =>
                {
                    var flammable = SEntMan.GetComponent<FlammableComponent>(victimUid);
                    var before = flammable.FireStacks;
                    var ev = CreateMeleeHit(targetUid, victimUid);
                    SEntMan.EventBus.RaiseLocalEvent(targetUid, ev);
                    Assert.Multiple(() =>
                    {
                        Assert.That(flammable.FireStacks, Is.GreaterThan(before), enchantId);
                        Assert.That(flammable.OnFire, enchantId);
                    });
                });
                await Delete(victim);
                break;
            }
            case "EnchantFortune":
            {
                var ore = await Spawn("DemonologyTestOre");
                var oreUid = ToServer(ore);
                await Server.WaitAssertion(() =>
                {
                    var ev = CreateMeleeHit(targetUid, oreUid);
                    SEntMan.EventBus.RaiseLocalEvent(targetUid, ev);
                    Assert.That(SEntMan.GetComponent<OreVeinComponent>(oreUid).Modifier,
                        Is.EqualTo(SEntMan.GetComponent<FortuneEnchantComponent>(enchantUid).Chance).Within(0.001f));
                });
                await Delete(ore);
                break;
            }
            case "EnchantInsulated":
                await Server.WaitAssertion(() =>
                {
                    var ev = new ElectrocutionAttemptEvent(targetUid, targetUid, 1f, SlotFlags.GLOVES);
                    SEntMan.EventBus.RaiseLocalEvent(targetUid, ev);
                    Assert.That(ev.SiemensCoefficient, Is.Zero, enchantId);
                });
                break;
            case "EnchantKnockback":
            {
                var victim = await Spawn("MobHuman");
                var victimUid = ToServer(victim);
                await Server.WaitAssertion(() =>
                {
                    var ev = CreateMeleeHit(targetUid, victimUid);
                    SEntMan.EventBus.RaiseLocalEvent(targetUid, ev);
                    Assert.Multiple(() =>
                    {
                        Assert.That(SEntMan.HasComponent<ThrownItemComponent>(victimUid), enchantId);
                        Assert.That(SEntMan.GetComponent<PhysicsComponent>(victimUid).LinearVelocity.X,
                            Is.GreaterThan(0f), enchantId);
                    });
                });
                await Delete(victim);
                break;
            }
            case "EnchantLavaforged":
            {
                var lava = await Spawn("FloorLavaEntity");
                var lavaUid = ToServer(lava);
                await Server.WaitAssertion(() =>
                {
                    var ev = new StepTriggerAttemptEvent
                    {
                        Source = lavaUid,
                        Tripper = SPlayer,
                    };
                    SEntMan.EventBus.RaiseLocalEvent(targetUid, ref ev);
                    Assert.That(ev.Cancelled, enchantId);
                });
                await Delete(lava);
                break;
            }
            case "EnchantProtection":
            case "EnchantUnbreaking":
                await Server.WaitAssertion(() =>
                {
                    const float incoming = 100f;
                    var ev = new DamageModifyEvent(
                        enchantId == "EnchantProtection" ? SPlayer : targetUid,
                        CreateDamage("Blunt", incoming));
                    SEntMan.EventBus.RaiseLocalEvent(targetUid, ev);
                    var expected = incoming * SEntMan.GetComponent<DamageModifyEnchantComponent>(enchantUid).Modifier;
                    Assert.That(ev.Damage.DamageDict["Blunt"].Float(), Is.EqualTo(expected).Within(0.01f), enchantId);
                });
                break;
            case "EnchantProtFire":
                await Server.WaitAssertion(() =>
                {
                    var component = SEntMan.GetComponent<FireProtEnchantComponent>(enchantUid);
                    var fire = new GetFireProtectionEvent(SPlayer);
                    SEntMan.EventBus.RaiseLocalEvent(targetUid, ref fire);
                    var temperature = new ModifyChangedTemperatureEvent(100f, SPlayer);
                    SEntMan.EventBus.RaiseLocalEvent(targetUid, temperature);
                    Assert.Multiple(() =>
                    {
                        Assert.That(fire.Multiplier, Is.EqualTo(1f - component.Reduction).Within(0.001f), enchantId);
                        Assert.That(temperature.TemperatureDelta,
                            Is.EqualTo(100f * component.TempModifier).Within(0.001f), enchantId);
                    });
                });
                break;
            case "EnchantSlippery":
            {
                await AddGravity();
                var victim = await Spawn("MobHuman");
                var victimUid = ToServer(victim);
                await Server.WaitAssertion(() =>
                {
                    var attempt = new StepTriggerAttemptEvent
                    {
                        Source = targetUid,
                        Tripper = victimUid,
                    };
                    SEntMan.EventBus.RaiseLocalEvent(targetUid, ref attempt);
                    Assert.That(attempt.Continue, enchantId);

                    Assert.That(SEntMan.GetComponent<GravityAffectedComponent>(victimUid).Weightless, Is.False,
                        $"{enchantId}: test victim must be under gravity");
                    var triggered = new StepTriggeredOffEvent(targetUid, victimUid);
                    SEntMan.EventBus.RaiseLocalEvent(targetUid, ref triggered);
                    AssertHasComponent(victimUid, "KnockedDown", enchantId);
                });
                await Delete(victim);
                break;
            }
            case "EnchantThorns":
            {
                var attacker = await Spawn("MobHuman");
                var attackerUid = ToServer(attacker);
                await Server.WaitAssertion(() =>
                {
                    var damageable = SEntMan.GetComponent<DamageableComponent>(attackerUid);
                    var before = damageable.TotalDamage;
                    var ev = new AttackedEvent(targetUid, attackerUid, Coordinates(targetUid));
                    SEntMan.EventBus.RaiseLocalEvent(targetUid, ev);
                    Assert.That(damageable.TotalDamage, Is.GreaterThan(before), enchantId);
                });
                await Delete(attacker);
                break;
            }
            case "EnchantUnslippable":
                await Server.WaitAssertion(() =>
                {
                    var ev = new SlipAttemptEvent(targetUid);
                    SEntMan.EventBus.RaiseLocalEvent(targetUid, ev);
                    Assert.That(ev.NoSlip, enchantId);
                });
                break;
            case "EnchantElectrified":
            {
                var victim = await Spawn("MobHuman");
                var victimUid = ToServer(victim);
                await Server.WaitAssertion(() =>
                {
                    var damageable = SEntMan.GetComponent<DamageableComponent>(victimUid);
                    var before = damageable.TotalDamage;
                    Assert.That(SEntMan.System<ElectrocutionSystem>().TryDoElectrifiedAct(targetUid, victimUid),
                        Is.True, enchantId);
                    Assert.That(damageable.TotalDamage, Is.GreaterThan(before), enchantId);
                });
                await Delete(victim);
                break;
            }
            case "EnchantFocus":
                await Server.WaitAssertion(() =>
                {
                    var ev = new CheckMagicItemEvent();
                    SEntMan.EventBus.RaiseLocalEvent(targetUid, ev);
                    Assert.That(ev.Handled, enchantId);
                });
                break;
            case "EnchantLaser":
            {
                EntityUid? projectile = null;
                await Server.WaitAssertion(() =>
                {
                    var ammunition = new List<(EntityUid? Entity, IShootable Shootable)>();
                    var ev = new TakeAmmoEvent(1, ammunition, Coordinates(targetUid), SPlayer);
                    SEntMan.EventBus.RaiseLocalEvent(targetUid, ev);
                    Assert.That(ammunition, Has.Count.EqualTo(1), enchantId);
                    projectile = ammunition.Single().Entity;
                    Assert.Multiple(() =>
                    {
                        Assert.That(projectile, Is.Not.Null, enchantId);
                        Assert.That(PrototypeId(projectile!.Value), Is.EqualTo("BulletLaser"), enchantId);
                        Assert.That(SEntMan.GetComponent<BasicEntityAmmoProviderComponent>(targetUid).Count,
                            Is.Zero, enchantId);
                    });
                });

                await RunSeconds(1.5f);
                Assert.That(SEntMan.GetComponent<BasicEntityAmmoProviderComponent>(targetUid).Count,
                    Is.EqualTo(1), $"{enchantId}: shot did not recharge");
                if (projectile is { } projectileUid)
                    await Delete(projectileUid);
                break;
            }
            case "EnchantMagicProtection":
                await Server.WaitAssertion(() =>
                {
                    var reflect = SEntMan.GetComponent<ReflectComponent>(targetUid);
                    Assert.That(SEntMan.System<ReflectSystem>().TryReflectHitscan(
                            (targetUid, reflect),
                            targetUid,
                            SPlayer,
                            targetUid,
                            Vector2.UnitX,
                            ReflectType.Magic,
                            null,
                            out var direction),
                        Is.True,
                        enchantId);
                    Assert.That(direction, Is.Not.Null, enchantId);
                });
                break;
            case "EnchantMagnetized":
                await Server.WaitAssertion(() =>
                {
                    var ev = new IsWeightlessEvent(true);
                    SEntMan.EventBus.RaiseLocalEvent(targetUid, ref ev);
                    Assert.Multiple(() =>
                    {
                        Assert.That(ev.Handled, enchantId);
                        Assert.That(ev.IsWeightless, Is.False, enchantId);
                    });
                });
                break;
            case "EnchantPoison":
            {
                await RunSeconds(1.1f);
                var victim = await Spawn("MobHuman");
                var victimUid = ToServer(victim);
                await Server.WaitAssertion(() =>
                {
                    var solutions = SEntMan.System<SharedSolutionContainerSystem>();
                    Assert.That(solutions.TryGetSolution(
                        victimUid,
                        BloodstreamComponent.DefaultBloodSolutionName,
                        out _,
                        out var bloodstream));
                    var before = bloodstream!.GetTotalPrototypeQuantity("Amatoxin");
                    var ev = CreateMeleeHit(targetUid, victimUid);
                    SEntMan.EventBus.RaiseLocalEvent(targetUid, ev);
                    Assert.That(bloodstream.GetTotalPrototypeQuantity("Amatoxin"), Is.GreaterThan(before), enchantId);
                });
                await Delete(victim);
                break;
            }
            case "EnchantRotten":
            {
                var before = CountPrototype("MobLivingCurse");
                await Server.WaitPost(() => SEntMan.AddComponent<RottingComponent>(targetUid));
                await RunTicks(1);
                Assert.Multiple(() =>
                {
                    Assert.That(SEntMan.Deleted(targetUid), enchantId);
                    Assert.That(CountPrototype("MobLivingCurse"), Is.EqualTo(before + 1), enchantId);
                });
                break;
            }
            case "EnchantMagicSentience":
                await Server.WaitAssertion(() =>
                {
                    var language = SEntMan.System<Content.Server._EinsteinEngines.Language.LanguageSystem>();
                    Assert.Multiple(() =>
                    {
                        Assert.That(language.CanSpeak(targetUid, SharedLanguageSystem.FallbackLanguagePrototype),
                            enchantId);
                        Assert.That(language.CanUnderstand(targetUid, SharedLanguageSystem.FallbackLanguagePrototype),
                            enchantId);
                        Assert.That(SEntMan.System<GhostRoleSystem>().GhostRoles.Any(role => role.Owner == targetUid),
                            enchantId);
                    });
                });
                break;
            case "CurseBurning":
            {
                var before = SEntMan.GetComponent<DamageableComponent>(SPlayer).Damage.DamageDict
                    .GetValueOrDefault("Heat");
                await DeleteHeldEntity();
                await Pickup(SEntMan.GetNetEntity(targetUid), deleteHeld: false);
                await Server.WaitPost(() =>
                {
                    var component = SEntMan.GetComponent<DamageOnHoldingComponent>(targetUid);
                    var system = SEntMan.System<DamageOnHoldingSystem>();
                    system.SetEnabled(targetUid, false, component);
                    system.SetEnabled(targetUid, true, component);
                });
                await RunTicks(1);
                Assert.That(SEntMan.GetComponent<DamageableComponent>(SPlayer).Damage.DamageDict
                        .GetValueOrDefault("Heat"),
                    Is.GreaterThanOrEqualTo(before + FixedPoint2.New(5)), enchantId);
                await Drop();
                break;
            }
            case "CurseClumsy":
            {
                var wearer = await Spawn("MobHuman");
                var wearerUid = ToServer(wearer);
                var inventory = SEntMan.System<InventorySystem>();
                await Server.WaitPost(() =>
                    Assert.That(inventory.TryEquip(wearerUid, targetUid, "outerClothing", force: true), enchantId));
                await RunTicks(1);
                await Server.WaitAssertion(() =>
                {
                    AssertHasComponent(wearerUid, "Clumsy", enchantId);
                    AssertHasComponent(wearerUid, "SleepEmitSound", enchantId);
                    AssertHasComponent(wearerUid, "SpeakFontOverride", enchantId);
                });
                await Server.WaitPost(() =>
                    Assert.That(inventory.TryUnequip(wearerUid, "outerClothing", force: true), enchantId));
                await Delete(wearer);
                break;
            }
            case "CurseSlowing":
                await DeleteHeldEntity();
                await Pickup(SEntMan.GetNetEntity(targetUid), deleteHeld: false);
                Assert.Multiple(() =>
                {
                    var movement = SEntMan.GetComponent<MovementSpeedModifierComponent>(SPlayer);
                    Assert.That(movement.WalkSpeedModifier, Is.EqualTo(0.9f).Within(0.001f), enchantId);
                    Assert.That(movement.SprintSpeedModifier, Is.EqualTo(0.9f).Within(0.001f), enchantId);
                });
                await Drop();
                Assert.Multiple(() =>
                {
                    var movement = SEntMan.GetComponent<MovementSpeedModifierComponent>(SPlayer);
                    Assert.That(movement.WalkSpeedModifier, Is.EqualTo(1f).Within(0.001f), enchantId);
                    Assert.That(movement.SprintSpeedModifier, Is.EqualTo(1f).Within(0.001f), enchantId);
                });
                break;
            case "CurseGravity":
            {
                var victim = await Spawn("MobHuman", PlayerCoords);
                var victimUid = ToServer(victim);
                await Server.WaitAssertion(() =>
                {
                    var physics = SEntMan.GetComponent<PhysicsComponent>(victimUid);
                    var before = physics.LinearVelocity;
                    var gravity = SEntMan.GetComponent<GravityWellComponent>(targetUid);
                    SEntMan.System<GravityWellSystem>().GravPulse(
                        targetUid,
                        gravity.MaxRange,
                        gravity.MinRange,
                        gravity.BaseRadialAcceleration);
                    var towardsCurse = Transform.GetWorldPosition(targetUid) - Transform.GetWorldPosition(victimUid);
                    Assert.That(Vector2.Dot(physics.LinearVelocity - before, towardsCurse), Is.GreaterThan(0f), enchantId);
                });
                await Delete(victim);
                break;
            }
            case "CurseInvisibility":
            {
                var stealth = SEntMan.System<Content.Server.Stealth.StealthSystem>();
                var before = stealth.GetVisibility(targetUid);
                await RunSeconds(1f);
                Assert.That(stealth.GetVisibility(targetUid), Is.LessThan(before), enchantId);
                break;
            }
            case "CurseUnremovable":
                await DeleteHeldEntity();
                await Pickup(SEntMan.GetNetEntity(targetUid), deleteHeld: false);
                await Server.WaitAssertion(() =>
                    Assert.That(HandSys.TryDrop((SPlayer, Hands)), Is.False, enchantId));
                await Server.WaitPost(() => SEntMan.RemoveComponent<UnremoveableComponent>(targetUid));
                await Drop();
                break;
            case "CurseVanishing":
                await DeleteHeldEntity();
                await Pickup(SEntMan.GetNetEntity(targetUid), deleteHeld: false);
                await Server.WaitAssertion(() =>
                {
                    var mobState = SEntMan.GetComponent<MobStateComponent>(SPlayer);
                    var ev = new MobStateChangedEvent(SPlayer, mobState, MobState.Alive, MobState.Dead);
                    SEntMan.EventBus.RaiseLocalEvent(SPlayer, ev, true);
                    Assert.That(SEntMan.TryGetComponent(targetUid, out FadingTimedDespawnComponent? fading), enchantId);
                    var curse = SEntMan.GetComponent<VanishingCurseComponent>(targetUid);
                    Assert.Multiple(() =>
                    {
                        Assert.That(fading!.Lifetime, Is.EqualTo(curse.Lifetime).Within(0.001f), enchantId);
                        Assert.That(fading.FadeOutTime, Is.EqualTo(curse.FadeOutTime).Within(0.001f), enchantId);
                    });
                });
                await Drop();
                break;
            default:
                Assert.Fail($"{enchantId}: missing functional behavior assertion");
                break;
        }
    }

    private MeleeHitEvent CreateMeleeHit(EntityUid weapon, EntityUid victim)
        => new([victim], SPlayer, weapon, new DamageSpecifier(), Vector2.UnitX, Coordinates(victim));

    private EntityCoordinates Coordinates(EntityUid uid)
        => SEntMan.GetComponent<TransformComponent>(uid).Coordinates;

    private DamageSpecifier CreateDamage(string type, float amount)
        => new(ProtoMan.Index<DamageTypePrototype>(type), FixedPoint2.New(amount));

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

            // Disable the hostile AI while the deterministic interaction checks run.
            await Server.WaitPost(() => SEntMan.RemoveComponent<HTNComponent>(demonUid));

            await AssertDemonMeleeAndHolyDamage(test, demonUid);

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

            await AssertDemonDestruction(test, demonUid);

            if (!SEntMan.Deleted(demonUid))
                await Delete(demon);
        }

        var before = CountPrototype("BeeLaughterDemon");
        var hiver = await Spawn("MajorDemonHiver");
        await Server.WaitPost(() => SEntMan.RemoveComponent<HTNComponent>(ToServer(hiver)));
        await RunSeconds(3.2f);
        Assert.That(CountPrototype("BeeLaughterDemon"), Is.InRange(before + 5, before + 10));
        await Delete(hiver);

        await AssertDemonAbilitiesWork();
    }

    private async Task AssertDemonAbilitiesWork()
    {
        await AssertDemonRiftWorks();
        await AssertDemonRepulseWorks();
        await AssertHamletTripWorks();
        await AssertHamletVentCrawlWorks();
        await AssertAbominationAcidWorks();
        await AssertHangedGrabWorks();
        await AssertFeverbirdGravityWorks();
        await AssertSteamerBurstWorks();
        await AssertGhostFixtureWorks();
        await AssertDevourerWorks("DemonologyTestBosche", includeWalls: true);
        await AssertDevourerWorks("DemonologyTestSaturn", includeWalls: false);
    }

    private async Task AssertDemonRiftWorks()
    {
        var demon = await Spawn("MajorAngelLizard", AbilityCoordinates(5, 0));
        var demonUid = ToServer(demon);
        await Server.WaitPost(() => SEntMan.RemoveComponent<HTNComponent>(demonUid));

        var action = GetGrantedAction(demonUid, "ActionDemonRift");
        var target = SEntMan.GetCoordinates(AbilityCoordinates(7, 0));
        var ev = new TeleportSpellEvent { Target = target };

        await Server.WaitPost(() =>
        {
            SEntMan.System<SharedActionsSystem>().PerformAction(demonUid, action, ev, predicted: false);
            Assert.That(ev.Handled, Is.True, "ActionDemonRift was not handled");
        });

        await RunTicks(1);
        Assert.That(Vector2.Distance(
                Transform.ToMapCoordinates(Coordinates(demonUid)).Position,
                Transform.ToMapCoordinates(target).Position),
            Is.LessThan(0.01f),
            "Demon rift did not teleport the demon to the selected coordinates");
        await Delete(demon);
    }

    private async Task AssertDemonRepulseWorks()
    {
        var demon = await Spawn("MajorAngelMoth", AbilityCoordinates(10, 0));
        var victim = await Spawn("DemonologyTestVictim", AbilityCoordinates(12, 0));
        var demonUid = ToServer(demon);
        var victimUid = ToServer(victim);
        await Server.WaitPost(() => SEntMan.RemoveComponent<HTNComponent>(demonUid));

        var action = GetGrantedAction(demonUid, "ActionDemonRepulse");
        var beforeVelocity = SEntMan.GetComponent<PhysicsComponent>(victimUid).LinearVelocity;
        var beforePosition = Transform.GetWorldPosition(victimUid);
        var away = beforePosition - Transform.GetWorldPosition(demonUid);
        var ev = new RepulseEvent();

        await Server.WaitPost(() =>
        {
            SEntMan.System<SharedActionsSystem>().PerformAction(demonUid, action, ev, predicted: false);
            Assert.That(ev.Handled, Is.True, "ActionDemonRepulse was not handled");
        });

        await RunTicks(1);
        var victimPhysics = SEntMan.GetComponent<PhysicsComponent>(victimUid);
        Assert.That(Vector2.Dot(victimPhysics.LinearVelocity - beforeVelocity, away), Is.GreaterThan(0f),
            "Demon repulse did not push the target away");
        Assert.That(SEntMan.HasComponent<StunnedComponent>(victimUid),
            "Demon repulse did not paralyze the target");

        await Delete(victim);
        await Delete(demon);
    }

    private async Task AssertHamletTripWorks()
    {
        var demon = await Spawn("MediumDemonHamlet", AbilityCoordinates(15, 0));
        var victim = await Spawn("DemonologyTestVictim", AbilityCoordinates(16, 0));
        var demonUid = ToServer(demon);
        var victimUid = ToServer(victim);
        await Server.WaitPost(() =>
        {
            SEntMan.RemoveComponent<HTNComponent>(demonUid);
            SEntMan.RemoveComponent<GravityAffectedComponent>(victimUid);
            SEntMan.GetComponent<DemonTripOnHitComponent>(demonUid).Chance = 1f;

            var melee = SEntMan.GetComponent<MeleeWeaponComponent>(demonUid);
            var attack = new LightAttackEvent(
                SEntMan.GetNetEntity(victimUid),
                SEntMan.GetNetEntity(demonUid),
                SEntMan.GetNetCoordinates(Coordinates(victimUid)));
            SEntMan.System<MeleeWeaponSystem>().DoLightAttack(demonUid, attack, demonUid, melee, null);
        });

        await RunTicks(1);
        Assert.That(SEntMan.TryGetComponent(victimUid, out KnockedDownComponent? knocked),
            "Hellmet melee did not knock the target down");
        Assert.That(knocked!.NextUpdate - STiming.CurTime, Is.GreaterThanOrEqualTo(TimeSpan.FromSeconds(2.9)),
            "Hellmet knockdown duration was shorter than the configured 3 seconds");

        await Delete(victim);
        await Delete(demon);
    }

    private async Task AssertHamletVentCrawlWorks()
    {
        var demon = await Spawn("MediumDemonHamlet", AbilityCoordinates(20, 0));
        var holder = await Spawn("VentCrawlerHolder", AbilityCoordinates(20, 0));
        var vent = await Spawn("GasVentPump", AbilityCoordinates(21, 0));
        var demonUid = ToServer(demon);
        var holderUid = ToServer(holder);
        var ventUid = ToServer(vent);

        await Server.WaitAssertion(() =>
        {
            var ventSystem = SEntMan.System<SharedVentCrawableSystem>();
            Assert.That(ventSystem.TryInsert(holderUid, demonUid), Is.True,
                "Hellmet could not be inserted into a vent holder");
            Assert.That(ventSystem.EnterTube(holderUid, ventUid), Is.True,
                "Hellmet holder could not enter the vent tube");

            var holderComp = SEntMan.GetComponent<VentCrawlerHolderComponent>(holderUid);
            var tubeComp = SEntMan.GetComponent<VentCrawlerTubeComponent>(ventUid);
            Assert.Multiple(() =>
            {
                Assert.That(holderComp.CurrentTube, Is.EqualTo(ventUid));
                Assert.That(holderComp.Container.Contains(demonUid));
                Assert.That(tubeComp.Contents.Contains(holderUid));
                Assert.That(SEntMan.HasComponent<BeingVentCrawlerComponent>(demonUid));
                Assert.That(SEntMan.GetComponent<BeingVentCrawlerComponent>(demonUid).Holder, Is.EqualTo(holderUid));
                Assert.That(SEntMan.GetComponent<PhysicsComponent>(demonUid).CanCollide, Is.False);
            });
        });

        await Delete(demon);
        await Delete(holder);
        await Delete(vent);
    }

    private async Task AssertAbominationAcidWorks()
    {
        await Server.WaitAssertion(() =>
        {
            var projectile = ProtoMan.Index<EntityPrototype>("DemonAcid");
            Assert.That(projectile.TryGetComponent<ProjectileComponent>(out var projectileComp, Factory));
            Assert.Multiple(() =>
            {
                Assert.That(projectileComp!.Damage.DamageDict.Keys, Is.EquivalentTo(new[] { "Caustic", "Structural" }));
                Assert.That(projectileComp.Damage.DamageDict["Caustic"], Is.EqualTo(FixedPoint2.New(15)));
                Assert.That(projectileComp.Damage.DamageDict["Structural"], Is.EqualTo(FixedPoint2.New(20)));
            });
        });

        var demon = await Spawn("MediumDemonAbomination", AbilityCoordinates(25, 0));
        var victim = await Spawn("DemonologyTestVictim", AbilityCoordinates(27, 0));
        var demonUid = ToServer(demon);
        var victimUid = ToServer(victim);

        await Server.WaitPost(() =>
        {
            SEntMan.RemoveComponent<HTNComponent>(demonUid);
            SEntMan.RemoveComponent<BatterySelfRechargerComponent>(demonUid);
            var gun = SEntMan.GetComponent<GunComponent>(demonUid);
            Assert.That(SGun.AttemptShoot(demonUid, (demonUid, gun), Coordinates(victimUid), victimUid), Is.True,
                "Abomination could not fire its acid projectile");
        });

        await RunSeconds(0.5f);
        var damage = SEntMan.GetComponent<DamageableComponent>(victimUid).Damage.DamageDict;
        Assert.Multiple(() =>
        {
            Assert.That(damage.GetValueOrDefault("Caustic"), Is.EqualTo(FixedPoint2.New(15)),
                "Abomination acid did not deal caustic damage");
            Assert.That(damage.GetValueOrDefault("Structural"), Is.EqualTo(FixedPoint2.New(20)),
                "Abomination acid did not deal structural damage");
        });

        await Delete(victim);
        await Delete(demon);
    }

    private async Task AssertHangedGrabWorks()
    {
        var demon = await Spawn("MajorDemonHanged", AbilityCoordinates(30, 0));
        var victim = await Spawn("DemonologyTestVictim", AbilityCoordinates(31, 0));
        var item = await Spawn("DemonologyTestItem", AbilityCoordinates(30, 1));
        var demonUid = ToServer(demon);
        var victimUid = ToServer(victim);
        var itemUid = ToServer(item);
        var pulling = SEntMan.System<PullingSystem>();
        var grab = SEntMan.System<GrabIntentSystem>();
        var stamina = SEntMan.System<SharedStaminaSystem>();

        await Server.WaitPost(() =>
        {
            var hangedGrab = SEntMan.GetComponent<GrabIntentComponent>(demonUid);
            Assert.That(hangedGrab.SuffocateGrabStaminaDamage, Is.EqualTo(1000f));
            Assert.That(HandSys.TryPickupAnyHand(demonUid, itemUid), Is.False,
                "Hanged could pick up an ordinary item");
            Assert.That(pulling.TryStartPull(demonUid, victimUid, force: true), Is.True,
                "Hanged could not start a grab");
            Assert.That(grab.TryGrab(victimUid, demonUid,
                ignoreCombatMode: true, grabStageOverride: GrabStage.Suffocate), Is.True,
                "Hanged could not escalate its grab to suffocating");
            Assert.That(SEntMan.GetComponent<GrabIntentComponent>(demonUid).GrabStage, Is.EqualTo(GrabStage.Suffocate));
            Assert.That(SEntMan.GetComponent<GrabbableComponent>(victimUid).GrabStage, Is.EqualTo(GrabStage.Suffocate));
            var held = HandSys.EnumerateHeld(demonUid).ToArray();
            Assert.That(held, Has.Length.EqualTo(2), "Hanged grab did not occupy both virtual hands");
            Assert.That(held.All(SEntMan.HasComponent<VirtualItemComponent>), Is.True,
                "Hanged grab used a non-virtual hand item");
        });

        await RunSeconds(0.2f);
        var before = stamina.GetStaminaDamage(victimUid);
        await Server.WaitPost(() =>
        {
            Assert.That(grab.TryGrab(victimUid, demonUid, ignoreCombatMode: true), Is.True,
                "Hanged suffocating grab could not apply its damage");
        });
        Assert.That(stamina.GetStaminaDamage(victimUid) - before, Is.EqualTo(1000f),
            "Hanged suffocating grab did not deal 1000 stamina damage");

        await Server.WaitPost(() => pulling.TryStopPull(victimUid, SEntMan.GetComponent<PullableComponent>(victimUid), ignoreGrab: true));
        await Delete(item);
        await Delete(victim);
        await Delete(demon);
    }

    private async Task AssertFeverbirdGravityWorks()
    {
        var demon = await Spawn("MajorDemonFeverbird", AbilityCoordinates(35, 0));
        var demonUid = ToServer(demon);
        await Server.WaitAssertion(() =>
        {
            var ev = new IsWeightlessEvent();
            SEntMan.EventBus.RaiseLocalEvent(demonUid, ref ev);
            Assert.That(ev.Handled, Is.True, "Feverbird did not handle weightlessness checks");
            Assert.That(ev.IsWeightless, Is.True, "Feverbird is affected by gravity");
        });
        await Delete(demon);
    }

    private async Task AssertSteamerBurstWorks()
    {
        var demon = await Spawn("MajorDemonSteamer", AbilityCoordinates(40, 0));
        var victim = await Spawn("DemonologyTestVictim", AbilityCoordinates(42, 0));
        var demonUid = ToServer(demon);
        var victimUid = ToServer(victim);

        await Server.WaitPost(() =>
        {
            SEntMan.RemoveComponent<HTNComponent>(demonUid);
            SEntMan.RemoveComponent<BatterySelfRechargerComponent>(demonUid);
            var gun = SEntMan.GetComponent<GunComponent>(demonUid);
            var provider = SEntMan.GetComponent<BatteryAmmoProviderComponent>(demonUid);
            Assert.That(gun.ShotsPerBurstModified, Is.EqualTo(5));
            Assert.That(provider.FireCost, Is.EqualTo(1));
            Assert.That(SGun.GetShots((demonUid, provider)).Item1, Is.EqualTo(150));
            Assert.That(SGun.AttemptShoot(demonUid, (demonUid, gun), Coordinates(victimUid), victimUid), Is.True,
                "Steamer could not start its flamethrower burst");
        });

        await RunSeconds(1.2f);
        Assert.That(SGun.GetShots((demonUid, SEntMan.GetComponent<BatteryAmmoProviderComponent>(demonUid))).Item1,
            Is.EqualTo(145), "Steamer did not consume exactly five burst charges");

        await Delete(victim);
        await Delete(demon);
    }

    private async Task AssertGhostFixtureWorks()
    {
        var demon = await Spawn("MajorDemonGhost", AbilityCoordinates(45, 0));
        var demonUid = ToServer(demon);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.TryGetComponent(demonUid, out FixturesComponent? fixtures));
            Assert.That(fixtures!.Fixtures.TryGetValue("projectile", out var projectile), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(projectile!.Hard, Is.False, "Ghost projectile fixture blocks movement");
                Assert.That(projectile.CollisionMask, Is.EqualTo((int) CollisionGroup.Opaque),
                    "Ghost projectile fixture has the wrong collision mask");
            });
        });
        await Delete(demon);
    }

    private async Task AssertDevourerWorks(string prototype, bool includeWalls)
    {
        var demon = await Spawn(prototype, AbilityCoordinates(includeWalls ? 50 : 60, 0));
        var demonUid = ToServer(demon);
        var devourer = SEntMan.GetComponent<DevourerComponent>(demonUid);
        var actions = SEntMan.System<SharedActionsSystem>();

        await Server.WaitPost(() =>
        {
            SEntMan.RemoveComponent<HTNComponent>(demonUid);
        });

        var victim = await Spawn("MobHuman", AbilityCoordinates(includeWalls ? 51 : 61, 0));
        var victimUid = ToServer(victim);
        await Server.WaitPost(() =>
            SEntMan.System<MobStateSystem>().ChangeMobState(victimUid, MobState.Dead));

        var bloodstream = SEntMan.System<SharedSolutionContainerSystem>();
        FixedPoint2 beforeIchor = FixedPoint2.Zero;
        await Server.WaitAssertion(() =>
        {
            Assert.That(bloodstream.TryGetSolution(demonUid, BloodstreamComponent.DefaultBloodSolutionName,
                out _, out var solution), Is.True);
            beforeIchor = solution!.GetTotalPrototypeQuantity("Ichor");
            var action = SEntMan.GetComponent<ActionComponent>(devourer.DevourActionEntity!.Value);
            var ev = new DevourActionEvent { Target = victimUid };
            actions.PerformAction(demonUid, (devourer.DevourActionEntity.Value, action), ev, predicted: false);
            Assert.That(ev.Handled, Is.True, $"{prototype} did not handle its devour action");
        });
        await RunTicks(3);

        Assert.That(devourer.Stomach.ContainedEntities, Does.Contain(victimUid), $"{prototype} did not store a humanoid");
        Assert.That(SEntMan.HasComponent<PreventSelfRevivalComponent>(victimUid));
        Assert.That(GetBloodstreamReagent(bloodstream, demonUid, "Ichor") - beforeIchor,
            Is.EqualTo(FixedPoint2.New(7.5f)), $"{prototype} did not receive Ichor from humanoid food");

        var targetCoordinates = AbilityCoordinates(includeWalls ? 51 : 61, 0);
        var item = await Spawn("DemonologyTestItem", targetCoordinates);
        var itemUid = ToServer(item);
        var beforeItemIchor = GetBloodstreamReagent(bloodstream, demonUid, "Ichor");
        await PerformDevourAction(demonUid, devourer, itemUid, actions);
        Assert.That(devourer.Stomach.ContainedEntities, Does.Contain(itemUid), $"{prototype} did not store an item");
        Assert.That(GetBloodstreamReagent(bloodstream, demonUid, "Ichor"), Is.EqualTo(beforeItemIchor),
            $"{prototype} incorrectly rewarded an item as preferred food");

        var door = await Spawn("Airlock", targetCoordinates);
        var doorUid = ToServer(door);
        await PerformDevourAction(demonUid, devourer, doorUid, actions);
        Assert.That(SEntMan.Deleted(doorUid), $"{prototype} did not destroy a door");

        if (includeWalls)
        {
            var wall = await Spawn("WallSolid", targetCoordinates);
            var wallUid = ToServer(wall);
            await PerformDevourAction(demonUid, devourer, wallUid, actions);
            Assert.That(SEntMan.Deleted(wallUid), "Bosche did not destroy a wall");
        }

        await Delete(demon);
    }

    private async Task PerformDevourAction(EntityUid demonUid, DevourerComponent devourer, EntityUid target,
        SharedActionsSystem actions)
    {
        await Server.WaitAssertion(() =>
        {
            var actionUid = devourer.DevourActionEntity!.Value;
            var action = SEntMan.GetComponent<ActionComponent>(actionUid);
            var ev = new DevourActionEvent { Target = target };
            actions.PerformAction(demonUid, (actionUid, action), ev, predicted: false);
            Assert.That(ev.Handled, Is.True, $"{PrototypeId(demonUid)} did not handle devour target");
        });
        await RunTicks(3);
    }

    private FixedPoint2 GetBloodstreamReagent(SharedSolutionContainerSystem solutions, EntityUid uid, string reagent)
    {
        Assert.That(solutions.TryGetSolution(uid, BloodstreamComponent.DefaultBloodSolutionName,
            out _, out var solution), Is.True);
        return solution!.GetTotalPrototypeQuantity(reagent);
    }

    private Entity<ActionComponent> GetGrantedAction(EntityUid owner, string prototype)
    {
        Assert.That(SEntMan.TryGetComponent(owner, out ActionGrantComponent? grant),
            $"{PrototypeId(owner)} has no action grant");
        foreach (var action in grant!.ActionEntities)
        {
            if (PrototypeId(action) == prototype)
                return (action, SEntMan.GetComponent<ActionComponent>(action));
        }

        Assert.Fail($"{PrototypeId(owner)} is missing granted action {prototype}");
        return default;
    }

    private NetCoordinates AbilityCoordinates(float x, float y)
        => SEntMan.GetNetCoordinates(MapData.GridCoords.Offset(new Vector2(x, y)));

    private void AssertDemonPrototype(DemonCase test)
    {
        var proto = ProtoMan.Index<EntityPrototype>(test.Id);
        Assert.That(proto.TryGetComponent<MeleeWeaponComponent>(out var melee, Factory), test.Id);
        Assert.That(proto.TryGetComponent<DestructibleComponent>(out var destructible, Factory), test.Id);
        var threshold = destructible!.Thresholds.Single(entry => entry.Trigger is DamageTrigger);
        var trigger = (DamageTrigger) threshold.Trigger!;
        var spawn = threshold.Behaviors.OfType<SpawnEntitiesBehavior>().Single();

        var expectedDamage = DemonMeleeDamage[test.Id]
            .ToDictionary(damage => damage.Type, damage => FixedPoint2.New(damage.Amount));
        Assert.That(melee!.Damage.DamageDict.Keys, Is.EquivalentTo(expectedDamage.Keys), test.Id);
        foreach (var (type, amount) in expectedDamage)
            Assert.That(melee.Damage.DamageDict[type], Is.EqualTo(amount), $"{test.Id}: {type}");

        Assert.Multiple(() =>
        {
            Assert.That(trigger.Damage, Is.EqualTo(FixedPoint2.New(test.DestructionDamage)), test.Id);
            Assert.That(spawn.Spawn.TryGetValue("DemonSoulFragment", out var soulRange), test.Id);
            Assert.That(soulRange.Min, Is.EqualTo(test.SoulMin), test.Id);
            Assert.That(soulRange.Max, Is.EqualTo(test.SoulMax), test.Id);
        });

        if (proto.TryGetComponent<Content.Shared.Chemistry.Components.SolutionRegenerationComponent>(out var regen, Factory))
        {
            Assert.That(DemonInjectedReagents.ContainsKey(test.Id), test.Id);
            foreach (var reagent in regen!.Generated.Contents)
                Assert.That(ProtoMan.TryIndex<ReagentPrototype>(reagent.Reagent.Prototype, out _), $"{test.Id}: {reagent.Reagent}");

            var expectedReagents = DemonInjectedReagents[test.Id]
                .ToDictionary(reagent => reagent.Reagent, reagent => FixedPoint2.New(reagent.Amount));
            Assert.That(regen.Generated.Contents.Select(reagent => reagent.Reagent.Prototype),
                Is.EquivalentTo(expectedReagents.Keys), test.Id);
            foreach (var reagent in regen.Generated.Contents)
                Assert.That(reagent.Quantity, Is.EqualTo(expectedReagents[reagent.Reagent.Prototype]),
                    $"{test.Id}: {reagent.Reagent.Prototype}");

            Assert.That(proto.TryGetComponent<MeleeChemicalInjectorComponent>(out var injector, Factory), test.Id);
            Assert.That(injector!.TransferAmount, Is.EqualTo(regen.Generated.Volume), test.Id);
        }
        else
            Assert.That(DemonInjectedReagents.ContainsKey(test.Id), Is.False, test.Id);

        if (proto.TryGetComponent<ActionGrantComponent>(out var grant, Factory))
        {
            Assert.That(DemonActions.TryGetValue(test.Id, out var expectedActions), test.Id);
            Assert.That(grant!.Actions.Select(action => action.Id), Is.EquivalentTo(expectedActions!), test.Id);
            foreach (var action in grant!.Actions)
                Assert.That(ProtoMan.TryIndex<EntityPrototype>(action.Id, out _), $"{test.Id}: {action}");
        }
        else
            Assert.That(DemonActions.ContainsKey(test.Id), Is.False, test.Id);

        if (proto.TryGetComponent<BatteryAmmoProviderComponent>(out var ammo, Factory))
            Assert.That(ProtoMan.TryIndex<EntityPrototype>(ammo!.Prototype.Id, out _), $"{test.Id}: {ammo.Prototype}");
    }

    private async Task AssertDemonMeleeAndHolyDamage(DemonCase test, EntityUid demonUid)
    {
        var victim = await Spawn("DemonologyTestVictim");
        var victimUid = ToServer(victim);

        if (DemonInjectedReagents.ContainsKey(test.Id))
            await RunSeconds(1.1f);

        await Server.WaitAssertion(() =>
        {
            var damageable = SEntMan.GetComponent<DamageableComponent>(victimUid);
            var beforeDamage = damageable.Damage.DamageDict.ToDictionary(pair => pair.Key, pair => pair.Value);
            var beforeReagents = new Dictionary<string, FixedPoint2>();
            if (SEntMan.TryGetComponent<BloodstreamComponent>(victimUid, out _))
            {
                var solutions = SEntMan.System<SharedSolutionContainerSystem>();
                Assert.That(solutions.TryGetSolution(
                    victimUid,
                    BloodstreamComponent.DefaultBloodSolutionName,
                    out _,
                    out var bloodstream), test.Id);
                foreach (var reagent in DemonInjectedReagents.GetValueOrDefault(test.Id, []))
                    beforeReagents[reagent.Reagent] = bloodstream!.GetTotalPrototypeQuantity(reagent.Reagent);
            }

            var melee = SEntMan.GetComponent<MeleeWeaponComponent>(demonUid);
            var attack = new LightAttackEvent(
                SEntMan.GetNetEntity(victimUid),
                SEntMan.GetNetEntity(demonUid),
                SEntMan.GetNetCoordinates(Coordinates(victimUid)));
            SEntMan.System<MeleeWeaponSystem>().DoLightAttack(demonUid, attack, demonUid, melee, null);

            var expectedDamage = DemonMeleeDamage[test.Id]
                .ToDictionary(damage => damage.Type, damage => FixedPoint2.New(damage.Amount));
            foreach (var (type, before) in beforeDamage)
            {
                var delta = damageable.Damage.DamageDict[type] - before;
                Assert.That(delta,
                    Is.EqualTo(expectedDamage.GetValueOrDefault(type, FixedPoint2.Zero)),
                    $"{test.Id}: unexpected {type} melee delta");
            }

            if (DemonInjectedReagents.ContainsKey(test.Id))
            {
                var solutions = SEntMan.System<SharedSolutionContainerSystem>();
                Assert.That(solutions.TryGetSolution(
                    victimUid,
                    BloodstreamComponent.DefaultBloodSolutionName,
                    out _,
                    out var bloodstream), test.Id);
                foreach (var (reagent, amount) in DemonInjectedReagents[test.Id])
                {
                    var delta = bloodstream!.GetTotalPrototypeQuantity(reagent) - beforeReagents[reagent];
                    Assert.That(delta.Float(), Is.EqualTo(amount).Within(0.1f), $"{test.Id}: {reagent} injection");
                }
            }
        });

        await Delete(victim);

        await Server.WaitAssertion(() =>
        {
            var damageable = SEntMan.GetComponent<DamageableComponent>(demonUid);
            var before = damageable.Damage.DamageDict.GetValueOrDefault("Holy");
            var changed = SEntMan.System<DamageableSystem>().TryChangeDamage(
                demonUid,
                CreateDamage("Holy", 1),
                damageable: damageable);
            Assert.That(changed, Is.Not.Null, $"{test.Id}: Holy damage was ignored");
            Assert.That(damageable.Damage.DamageDict["Holy"] - before,
                Is.EqualTo(FixedPoint2.New(1)), $"{test.Id}: Holy damage delta");
            SEntMan.System<DamageableSystem>().SetAllDamage(demonUid, damageable, FixedPoint2.Zero);
        });
    }

    private async Task AssertDemonDestruction(DemonCase test, EntityUid demonUid)
    {
        var beforeFragments = CountPrototype("DemonSoulFragment");
        var beforeHides = CountPrototype("MaterialHideCorgi");
        var damageable = SEntMan.GetComponent<DamageableComponent>(demonUid);

        await Server.WaitPost(() =>
        {
            Assert.That(SEntMan.System<DamageableSystem>().TryChangeDamage(
                demonUid,
                CreateDamage("Blunt", test.DestructionDamage),
                damageable: damageable), Is.Not.Null, test.Id);
        });
        await RunTicks(3);

        Assert.That(SEntMan.Deleted(demonUid), $"{test.Id}: threshold did not destroy demon");
        Assert.That(CountPrototype("DemonSoulFragment"),
            Is.InRange(beforeFragments + test.SoulMin, beforeFragments + test.SoulMax),
            $"{test.Id}: soul fragment drop range");

        if (test.Id == "MinorDemonIan")
            Assert.That(CountPrototype("MaterialHideCorgi"), Is.EqualTo(beforeHides + 1), test.Id);
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
