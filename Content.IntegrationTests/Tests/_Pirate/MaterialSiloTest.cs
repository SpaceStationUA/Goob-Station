// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Materials;

namespace Content.IntegrationTests.Tests._Pirate;

[TestFixture]
[TestOf(typeof(SharedMaterialStorageSystem))]
public sealed class MaterialSiloTest
{
    [Test]
    public async Task AcceptsStandardMaterials()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entityManager = server.EntMan;
        var materialStorage = server.System<SharedMaterialStorageSystem>();
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var silo = entityManager.SpawnEntity("MachineMaterialSilo", map.GridCoords);
            var materials = new[]
            {
                "SheetSteel1",
                "SheetPlasma1",
                "IngotGold1",
                "MaterialDiamond1",
                "MaterialCloth1",
            };

            Assert.Multiple(() =>
            {
                foreach (var prototype in materials)
                {
                    var material = entityManager.SpawnEntity(prototype, map.GridCoords);
                    Assert.That(materialStorage.CanInsertMaterialEntity(material, silo),
                        Is.True,
                        $"Material silo does not accept {prototype}");
                }
            });
        });

        await pair.CleanReturnAsync();
    }
}
