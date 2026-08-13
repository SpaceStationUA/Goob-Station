// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Lathe;
using Content.Server.Power.Components;
using Content.Shared.Lathe;
using Content.Shared.Materials;
using Content.Shared.Research.Prototypes;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Pirate.Lathe;

/// <summary>
/// Regression test for the ore processor crashing the server ("Nullable object must have a value")
/// when a batch of instant, storage-output recipes was queued.
/// </summary>
[TestFixture]
public sealed class LatheMassProductionTest
{
    private const string LatheProto = "OreProcessor";
    private const string RecipeProto = "IngotGold1";
    private const int Quantity = 10;

    [Test]
    public async Task InstantStorageRecipeBatchProducesEveryItem()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var mapData = await pair.CreateTestMap();

        var entMan = server.EntMan;
        var protoMan = server.ProtoMan;
        var compFactory = server.ResolveDependency<IComponentFactory>();
        var latheSystem = server.System<LatheSystem>();
        var materialStorage = server.System<SharedMaterialStorageSystem>();

        await server.WaitAssertion(() =>
        {
            var lathe = entMan.SpawnEntity(LatheProto, mapData.GridCoords);
            entMan.RemoveComponent<ApcPowerReceiverComponent>(lathe); // Test maps have no APC.
            var latheComp = entMan.GetComponent<LatheComponent>(lathe);

            var recipe = protoMan.Index<LatheRecipePrototype>(RecipeProto);

            Assert.Multiple(() =>
            {
                Assert.That(latheComp.OutputToStorage, $"{LatheProto} no longer outputs to storage, pick another lathe for this test");
                Assert.That(recipe.CompleteTime, Is.EqualTo(TimeSpan.Zero), $"{RecipeProto} is no longer instant, pick another recipe for this test");
                Assert.That(recipe.Result, Is.Not.Null);
            });

            var result = protoMan.Index<EntityPrototype>(recipe.Result!.Value);
            Assert.That(result.TryGetComponent<PhysicalCompositionComponent>(out var composition, compFactory),
                $"{result.ID} has no composition, so it wouldn't go into the lathe's storage");

            foreach (var (material, amount) in recipe.Materials)
            {
                Assert.That(materialStorage.TryChangeMaterialAmount(lathe, material, amount * Quantity),
                    $"Failed to seed the lathe with {amount * Quantity} {material}");
            }

            Assert.That(latheSystem.TryAddToQueue(lathe, recipe, Quantity, latheComp), "Failed to queue the batch");
            Assert.That(latheSystem.TryStartProducing(lathe, latheComp), "The lathe refused to start printing");

            Assert.Multiple(() =>
            {
                Assert.That(latheComp.Queue, Is.Empty, "The lathe stopped printing before the batch was finished");
                Assert.That(latheComp.CurrentRecipe, Is.Null);

                foreach (var (material, amount) in composition!.MaterialComposition)
                {
                    Assert.That(materialStorage.GetMaterialAmount(lathe, material),
                        Is.EqualTo(amount * Quantity),
                        $"Lathe produced the wrong amount of {material}");
                }
            });

            entMan.DeleteEntity(lathe);
        });

        await pair.CleanReturnAsync();
    }
}
