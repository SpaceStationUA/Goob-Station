// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using Content.Server.Lathe;
using Content.Server.Power.Components;
using Content.Shared.Lathe;
using Content.Shared.Lathe.Prototypes;
using Content.Shared.Materials;
using Content.Shared.Research.Prototypes;
using Content.Shared.Whitelist;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Pirate.Knowledge;

[TestFixture]
public sealed class PortedLatheIntegrityTest
{
    private static readonly string[] PortedLatheIds =
    [
        "CharcoalOven",
        "RevBulletPress",
        "RevForge",
        "RevMedFab",
        "RevMicroelectronicsAssembler",
        "RevPlasmaFoundry",
        "RevPrinter",
        "RevPrintingPress",
        "RevRadioBench",
        "RevTechAnalyzer",
        "RevTelesphere",
        "RevUniformPrinter",
    ];

    private static readonly Dictionary<string, Dictionary<string, string[]>> RequiredMachineRecipes = new()
    {
        ["CharcoalOven"] = new()
        {
            ["CharcoalBurnerStatic"] = ["Charcoal", "AshPile"],
        },
        ["RevBulletPress"] = new()
        {
            ["RevBulletPressStatic"] =
            [
                "RevTurretBullet", "RevPistolBullet", "RevRifleBullet",
                "RevBuckShell", "RevSlugShell", "RevBreachShell",
            ],
        },
        ["RevForge"] = new()
        {
            ["RevForgeStatic"] =
            [
                "RevSword", "RevKnife", "RevPitchfork", "RevTorch", "RevMace",
                "RevGear", "RevBolt", "RevNut", "ClothingOuterArmorRev",
                "RevArmorPlate", "RevCuffs", "RevBallBearings",
            ],
        },
        ["RevMedFab"] = new()
        {
            ["RevMedicalStatic"] =
            [
                "MedicatedSuture", "RegenerativeMesh", "RevGauze",
                "RevBrutepack", "RevOintment",
            ],
        },
        ["RevMicroelectronicsAssembler"] = new()
        {
            ["RevMicroElectronicsStatic"] =
            [
                "RevID", "RevDoor", "RevJammer", "RevHeadset", "RevVisor",
                "PinpointerCommand", "FusionCore",
            ],
        },
        ["RevPlasmaFoundry"] = new()
        {
            ["RevFoundryStatic"] =
            [
                "RevRocketFrame", "ClothingOuterArmorRevFlame", "RevEngineParts",
                "RevEmptyFuelTank", "T45Helmet", "T45Chest", "T45ArmL",
                "T45ArmR", "T45LegL", "T45LegR",
            ],
        },
        ["RevPrinter"] = new()
        {
            ["RevPrinterStatic"] =
            [
                "SmilingPal1", "SmilingPal2", "SmilingPal3", "SmilingPal4",
                "SmilingPal5", "SmilingPal6", "RevGunParts", "RevTurretMagazine",
                "RevTurretMagazineHeavy", "RevSMGMagazine",
            ],
        },
        ["RevPrintingPress"] = new()
        {
            ["RevPrintingPress"] =
                ["RevPropaganda", "BookMeleePrintedRev", "BookShootingPrintedRev"],
            ["PrintingPressAdvanced"] = ["BookFabricationPrinted", "BookSurvivalPrinted"],
        },
        ["RevRadioBench"] = new()
        {
            ["RevTelecommsWorkbenchStatic"] =
            [
                "EncryptionKeyRev", "RevDropPodTraps", "RevTurretElectronics",
                "TelecomServerCircuitboard", "RevIFF", "TrackingImplanterRev",
                "SheetifierMachineCircuitboard",
            ],
        },
        ["RevTechAnalyzer"] = new()
        {
            ["RevTechStatic"] =
            [
                "DataChipAIBoard", "DataChipReflectivePlate", "DataChipFakeMindshield",
                "DataChipShieldRipper", "MoldMeleeUpgrades",
            ],
        },
        ["RevTelesphere"] = new()
        {
            ["RevTelesphereStatic"] =
            [
                "RevCrystal", "RevBiomass", "RevBrass", "RevBanana", "RevBones",
                "RevGlass", "RevCardboard", "RevPaper", "RevCloth", "RevSteel",
                "RevMeat", "RevDuraThread", "RevPlasma", "RevDiamonds", "RevSilk",
                "RevPlasteel", "RevUranium", "RevPlastic", "RevWood", "RevGunpowder",
            ],
        },
        ["RevUniformPrinter"] = new()
        {
            ["RevClothingStatic"] =
            [
                "ClothingBackpackRev", "ClothingBeltRevWebbing", "RevHelmetLiner",
                "RevGasFilter", "ClothingHeadBandRev", "RevEmptyVest",
                "ClothingUniformJumpsuitMilitaryTurtleneckRev", "ClothingShoesBootsRev",
                "ClothingHandsGlovesCombat",
            ],
        },
    };

    [Test]
    public async Task PortedLatheMachinePacksRecipesResultsAndMaterialSourcesAreValid()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.EntMan;
        var protoMan = server.ProtoMan;
        var factory = server.ResolveDependency<IComponentFactory>();
        var whitelist = server.System<EntityWhitelistSystem>();

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                foreach (var machineId in PortedLatheIds)
                {
                    if (!protoMan.TryIndex<EntityPrototype>(machineId, out var machinePrototype))
                    {
                        Assert.Fail($"Ported lathe entity '{machineId}' does not exist.");
                        continue;
                    }

                    if (!machinePrototype.TryGetComponent<LatheComponent>(out var lathe, factory))
                    {
                        Assert.Fail($"Ported lathe entity '{machineId}' has no Lathe component.");
                        continue;
                    }

                    if (!machinePrototype.TryGetComponent<MaterialStorageComponent>(out var storage, factory))
                    {
                        Assert.Fail($"Ported lathe entity '{machineId}' has no MaterialStorage component.");
                        continue;
                    }

                    var packIds = lathe.StaticPacks.Concat(lathe.DynamicPacks).Distinct().ToArray();
                    Assert.That(packIds, Is.Not.Empty, $"Ported lathe '{machineId}' has no recipe packs.");

                    foreach (var (requiredPackId, requiredRecipes) in RequiredMachineRecipes[machineId])
                    {
                        Assert.That(packIds, Does.Contain(requiredPackId),
                            $"Ported lathe '{machineId}' is missing required pack '{requiredPackId}'.");

                        if (!protoMan.TryIndex<LatheRecipePackPrototype>(requiredPackId, out var requiredPack))
                        {
                            Assert.Fail($"Required lathe pack '{requiredPackId}' does not exist.");
                            continue;
                        }

                        foreach (var requiredRecipe in requiredRecipes)
                        {
                            Assert.That(requiredPack.Recipes, Does.Contain(requiredRecipe),
                                $"Ported lathe pack '{requiredPackId}' is missing source recipe '{requiredRecipe}'.");
                        }
                    }

                    foreach (var packId in packIds)
                    {
                        if (!protoMan.TryIndex<LatheRecipePackPrototype>(packId, out var pack))
                        {
                            Assert.Fail($"Ported lathe '{machineId}' references missing pack '{packId}'.");
                            continue;
                        }

                        Assert.That(pack.Recipes, Is.Not.Empty,
                            $"Ported lathe pack '{packId}' on '{machineId}' contains no recipes.");

                        foreach (var recipeId in pack.Recipes)
                        {
                            if (!protoMan.TryIndex<LatheRecipePrototype>(recipeId, out var recipe))
                            {
                                Assert.Fail($"Ported lathe pack '{packId}' references missing recipe '{recipeId}'.");
                                continue;
                            }

                            Assert.That(recipe.Abstract, Is.False,
                                $"Ported lathe pack '{packId}' exposes abstract recipe '{recipeId}'.");
                            Assert.That(recipe.CompleteTime, Is.GreaterThanOrEqualTo(TimeSpan.Zero),
                                $"Ported lathe recipe '{recipeId}' has a negative completion time.");
                            Assert.That(recipe.Materials, Is.Not.Empty,
                                $"Ported lathe recipe '{recipeId}' can print without any material input.");

                            if (recipe.Result is not { } resultId ||
                                !protoMan.TryIndex<EntityPrototype>(resultId, out var resultPrototype))
                            {
                                Assert.Fail($"Ported lathe recipe '{recipeId}' has missing entity result '{recipe.Result}'.");
                            }
                            else if (resultPrototype.Abstract)
                            {
                                Assert.Fail($"Ported lathe recipe '{recipeId}' produces abstract entity '{resultId}'.");
                            }
                            else
                            {
                                var result = entMan.SpawnEntity(resultId, map.GridCoords);
                                Assert.That(entMan.Deleted(result), Is.False,
                                    $"Ported lathe result '{resultId}' did not survive initialization.");
                                entMan.DeleteEntity(result);
                            }

                            var totalMaterial = 0;
                            foreach (var (materialId, amount) in recipe.Materials)
                            {
                                Assert.That(amount, Is.GreaterThan(0),
                                    $"Ported lathe recipe '{recipeId}' has a non-positive '{materialId}' cost.");
                                totalMaterial += amount;

                                if (!protoMan.TryIndex<MaterialPrototype>(materialId, out var material))
                                {
                                    Assert.Fail($"Ported lathe recipe '{recipeId}' references missing material '{materialId}'.");
                                    continue;
                                }

                                if (material.StackEntity is not { } sourceId ||
                                    !protoMan.TryIndex<EntityPrototype>(sourceId, out var sourcePrototype) ||
                                    sourcePrototype.Abstract)
                                {
                                    Assert.Fail($"Material '{materialId}' for recipe '{recipeId}' has no concrete stack source.");
                                    continue;
                                }

                                var source = entMan.SpawnEntity(sourceId, map.GridCoords);
                                Assert.That(whitelist.IsWhitelistFail(storage.Whitelist, source), Is.False,
                                    $"Ported lathe '{machineId}' cannot accept '{sourceId}' for recipe '{recipeId}' material '{materialId}'.");
                                entMan.DeleteEntity(source);
                            }

                            if (storage.StorageLimit is { } limit)
                            {
                                Assert.That(totalMaterial, Is.LessThanOrEqualTo(limit),
                                    $"Ported lathe '{machineId}' cannot hold the {totalMaterial} material units needed for '{recipeId}' (limit {limit}).");
                            }
                        }
                    }
                }
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EveryPortedLatheRecipeConsumesMaterialsAndProducesItsResult()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.EntMan;
        var protoMan = server.ProtoMan;
        var latheSystem = server.System<LatheSystem>();
        var materialStorage = server.System<SharedMaterialStorageSystem>();
        var lookup = server.System<EntityLookupSystem>();

        await server.WaitAssertion(() =>
        {
            foreach (var (machineId, requiredPacks) in RequiredMachineRecipes)
            {
                foreach (var recipeId in requiredPacks.Values.SelectMany(recipes => recipes).Distinct())
                {
                    var lathe = entMan.SpawnEntity(machineId, map.GridCoords);
                    entMan.RemoveComponent<ApcPowerReceiverComponent>(lathe); // Test maps have no APC.
                    var latheComp = entMan.GetComponent<LatheComponent>(lathe);
                    var recipe = protoMan.Index<LatheRecipePrototype>(recipeId);

                    Assert.That(recipe.Result, Is.Not.Null,
                        $"Ported lathe recipe '{recipeId}' has no entity result to print.");

                    foreach (var (material, amount) in recipe.Materials)
                    {
                        var adjusted = SharedLatheSystem.AdjustMaterial(
                            amount,
                            recipe.ApplyMaterialDiscount,
                            latheComp.MaterialUseMultiplier);
                        Assert.That(materialStorage.TryChangeMaterialAmount(lathe, material, adjusted), Is.True,
                            $"Could not seed '{machineId}' with {adjusted} units of '{material}' for '{recipeId}'.");
                    }

                    Assert.That(latheSystem.CanProduce(lathe, recipe, component: latheComp), Is.True,
                        $"'{machineId}' cannot actually produce its exposed recipe '{recipeId}'.");
                    Assert.That(latheSystem.TryAddToQueue(lathe, recipe, 1, latheComp), Is.True,
                        $"'{machineId}' refused to queue '{recipeId}'.");

                    foreach (var material in recipe.Materials.Keys)
                    {
                        Assert.That(materialStorage.GetMaterialAmount(lathe, material), Is.Zero,
                            $"'{machineId}' did not consume the exact '{material}' cost for '{recipeId}'.");
                    }

                    Assert.That(latheSystem.TryStartProducing(lathe, latheComp), Is.True,
                        $"'{machineId}' refused to start '{recipeId}'.");
                    if (latheComp.CurrentRecipe != null)
                        latheSystem.FinishProducing(lathe, latheComp);

                    Assert.Multiple(() =>
                    {
                        Assert.That(latheComp.CurrentRecipe, Is.Null,
                            $"'{machineId}' remained stuck producing '{recipeId}'.");
                        Assert.That(latheComp.Queue, Is.Empty,
                            $"'{machineId}' left '{recipeId}' in its queue after completion.");
                    });

                    var expectedResult = recipe.Result!.Value.Id;
                    var nearby = lookup.GetEntitiesInRange(
                        entMan.GetComponent<TransformComponent>(lathe).Coordinates,
                        0.25f,
                        LookupFlags.All);
                    var result = nearby.FirstOrDefault(uid =>
                        uid != lathe &&
                        !entMan.Deleted(uid) &&
                        entMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID == expectedResult);

                    Assert.That(result, Is.Not.EqualTo(EntityUid.Invalid),
                        $"'{machineId}' completed '{recipeId}' but did not spawn '{expectedResult}'.");

                    if (result != EntityUid.Invalid)
                        entMan.DeleteEntity(result);
                    entMan.DeleteEntity(lathe);
                }
            }
        });

        await pair.CleanReturnAsync();
    }
}
