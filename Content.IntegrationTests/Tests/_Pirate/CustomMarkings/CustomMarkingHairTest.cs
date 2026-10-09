// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using Content.Client._Pirate.CustomMarkings;
using Content.Client.Humanoid;
using Content.Client.Inventory;
using Content.Shared._Pirate.CustomMarkings;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.IntegrationTests.Tests._Pirate.CustomMarkings;

/// <summary>
/// A marking over the hair is there to change the hair, so it is hidden, all of it, whenever the hair is. Markings
/// placed elsewhere stay. And the editor can tell which layers are hair, to leave them out of its picture.
/// </summary>
[TestFixture]
[TestOf(typeof(CustomMarkingSystem))]
public sealed class CustomMarkingHairTest
{
    private const string Hair = "HumanHairAfro";
    private const string Beard = "HumanFacialHairFullbeard";

    private static readonly string Hash = new('5', CustomMarkingRules.HashLength);

    [TestCase(true)]
    [TestCase(false)]
    public async Task HairArtStaysUnderGasMaskTest(bool preview)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;
        await client.WaitAssertion(() =>
        {
            var entMan = client.EntMan;
            var sprites = entMan.System<SpriteSystem>();
            var inventory = entMan.System<ClientInventorySystem>();
            var system = entMan.System<CustomMarkingSystem>();
            var art = new CustomMarkingArt();
            art.SetPixel(0, CustomMarkingArt.South, 16, 12, new Rgba32(255, 0, 0, 255));
            system.Remember(Hash, art);
            var profile = HumanoidCharacterProfile.DefaultWithSpecies("Human");
            profile = profile
                .WithCharacterAppearance(profile.Appearance.WithHairStyleName(Hair))
                .WithCustomMarkings(new List<CustomMarking> { new(Hash, CustomMarkingPlacement.Hair) });
            var species = client.ResolveDependency<IPrototypeManager>().Index<SpeciesPrototype>(profile.Species);
            var doll = entMan.SpawnEntity(preview ? species.DollPrototype : "MobHuman", MapCoordinates.Nullspace);
            entMan.System<HumanoidAppearanceSystem>().LoadProfile(doll, profile);
            var sprite = entMan.GetComponent<SpriteComponent>(doll);
            Assert.That(sprites.LayerMapTryGet((doll, sprite), HumanoidVisualLayers.Hair, out var hair, false), Is.True);
            Assert.That(Marking(), Is.GreaterThan(hair), "empty mask anchors do not bury the artwork under the hair");

            var mask = entMan.SpawnEntity("ClothingMaskGas", MapCoordinates.Nullspace);
            Assert.That(inventory.TryEquip(doll, mask, "mask", force: true), Is.True);
            var layers = entMan.GetComponent<InventorySlotsComponent>(doll).VisualLayerKeys["mask"];
            Assert.That(layers, Is.Not.Empty, "the mask has rendered equipment layers");
            foreach (var key in layers)
            {
                Assert.That(sprites.LayerMapTryGet((doll, sprite), key, out var maskLayer, false), Is.True);
                Assert.That(Marking(), Is.LessThan(maskLayer), "visible gas-mask layers cover hair-placement art");
            }

            Assert.That(sprite[Marking()].Visible, Is.True, "a gas mask leaves hair-placement artwork visible");
            Assert.That(inventory.TryUnequip(doll, "mask", force: true, reparent: false), Is.True);
            Assert.That(sprites.LayerMapTryGet((doll, sprite), HumanoidVisualLayers.Hair, out hair, false), Is.True);
            Assert.That(Marking(), Is.GreaterThan(hair), "taking off the mask restores the normal depth automatically");
            entMan.DeleteEntity(mask);
            entMan.DeleteEntity(doll);
            return;

            int Marking()
            {
                Assert.That(sprites.LayerMapTryGet((doll, sprite), CustomMarkingSystem.LayerKey(0), out var index, false), Is.True);
                return index;
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HairMarkingFollowsHairTest()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;

        await client.WaitAssertion(() =>
        {
            var entMan = client.EntMan;
            var sprites = entMan.System<SpriteSystem>();
            var humanoids = entMan.System<HumanoidAppearanceSystem>();
            var system = entMan.System<CustomMarkingSystem>();
            var markings = client.ResolveDependency<MarkingManager>();

            // A drawing that covers every facing, so some of it lies on the body whatever the hair does.
            var art = new CustomMarkingArt();
            for (var facing = 0; facing < CustomMarkingRules.Facings; facing++)
            {
                for (var y = 0; y < CustomMarkingRules.FrameSize; y++)
                {
                    for (var x = 0; x < CustomMarkingRules.FrameSize; x++)
                    {
                        art.SetPixel(0, facing, x, y, new Rgba32(255, 0, 0, 255));
                    }
                }
            }

            system.Remember(Hash, art);

            var profile = HumanoidCharacterProfile.DefaultWithSpecies("Human");
            profile = profile
                .WithCharacterAppearance(profile.Appearance.WithHairStyleName(Hair).WithFacialHairStyleName(Beard))
                .WithCustomMarkings(new List<CustomMarking>
                {
                    new(Hash, CustomMarkingPlacement.Hair),
                    new(Hash, CustomMarkingPlacement.Skin),
                    new(Hash, CustomMarkingPlacement.Front),
                });
            var species = client.ResolveDependency<IPrototypeManager>().Index<SpeciesPrototype>(profile.Species);
            var doll = entMan.SpawnEntity(species.DollPrototype, MapCoordinates.Nullspace);
            humanoids.LoadProfile(doll, profile);
            var humanoid = entMan.GetComponent<HumanoidAppearanceComponent>(doll);
            var sprite = entMan.GetComponent<SpriteComponent>(doll);

            // The layers that are hair: every sprite of the hair and of the beard, and nothing of the body.
            var expected = new List<int>();
            foreach (var id in new[] { Hair, Beard })
            {
                foreach (var drawn in markings.Markings[id].Sprites.OfType<SpriteSpecifier.Rsi>())
                {
                    Assert.That(sprites.LayerMapTryGet((doll, sprite), $"{id}-{drawn.RsiState}", out var index, false), Is.True, $"the body shows {id}");
                    expected.Add(index);
                }
            }

            var hair = new HashSet<int>();
            system.GetHairLayers((doll, sprite), hair);
            Assert.That(expected, Is.Not.Empty);
            Assert.That(hair, Is.EquivalentTo(expected));

            Assert.Multiple(() =>
            {
                Assert.That(Shown(0), Is.True);
                Assert.That(Shown(1), Is.True);
                Assert.That(Shown(2), Is.True);
            });

            // A helmet hides the hair: the marking on the hair goes with it, whole, and the others stay.
            humanoids.SetLayerVisibility((doll, humanoid), HumanoidVisualLayers.Hair, false);
            Assert.Multiple(() =>
            {
                Assert.That(Shown(0), Is.False, "the marking over the hair is hidden like the hair");
                Assert.That(Shown(1), Is.True, "the one on the skin stays");
                Assert.That(Shown(2), Is.True, "and the one over clothing");
            });

            humanoids.SetLayerVisibility((doll, humanoid), HumanoidVisualLayers.Hair, true);
            Assert.That(Shown(0), Is.True, "and comes back with it");

            entMan.DeleteEntity(doll);
            return;

            bool Shown(int marking)
            {
                Assert.That(sprites.LayerMapTryGet((doll, sprite), CustomMarkingSystem.LayerKey(marking), out var index, false), Is.True);
                return sprite[index].Visible;
            }
        });

        await pair.CleanReturnAsync();
    }
}
