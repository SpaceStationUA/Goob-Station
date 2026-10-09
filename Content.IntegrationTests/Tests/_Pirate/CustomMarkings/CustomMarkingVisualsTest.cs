// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using Content.Client._Pirate.CustomMarkings;
using Content.Client.Humanoid;
using Content.Shared._Pirate.CustomMarkings;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.GameObjects;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.IntegrationTests.Tests._Pirate.CustomMarkings;

/// <summary>Art held in memory loads as a four-facing RSI and lands on a body at the depth its placement names.</summary>
[TestFixture]
[TestOf(typeof(CustomMarkingSystem))]
public sealed class CustomMarkingVisualsTest
{
    private static readonly string Hash = new('a', CustomMarkingRules.HashLength);
    private static readonly string OtherHash = new('b', CustomMarkingRules.HashLength);

    [Test]
    public async Task ArtLoadsFromMemoryTest()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;

        await client.WaitAssertion(() =>
        {
            var system = client.EntMan.System<CustomMarkingSystem>();
            Store(client.ResolveDependency<IResourceCache>(), Hash);

            Assert.That(system.TryGetArt(Hash, out var rsi), Is.True, "art in the root loads without asking the server");
            Assert.That(rsi!.Size, Is.EqualTo(new Vector2i(CustomMarkingRules.FrameSize, CustomMarkingRules.FrameSize)));
            Assert.That(rsi.TryGetState(CustomMarkingResources.State, out var state), Is.True);
            Assert.That(state!.RsiDirections, Is.EqualTo(RsiDirectionType.Dir4), "one frame for each facing");
            Assert.That(system.TryGetPng(Hash, out var png), Is.True);
            Assert.That(CustomMarkingPng.Read(png!)!.GetPixel(0, CustomMarkingArt.West, 3, 4), Is.EqualTo(new Rgba32(1, 2, 3, 255)));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DollWearsProfileMarkingsTest()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;

        await client.WaitAssertion(() =>
        {
            var entMan = client.EntMan;
            var sprites = entMan.System<SpriteSystem>();
            var resCache = client.ResolveDependency<IResourceCache>();
            Store(resCache, Hash);
            Store(resCache, OtherHash);

            var profile = HumanoidCharacterProfile.DefaultWithSpecies().WithCustomMarkings(new List<CustomMarking>
            {
                new(Hash, CustomMarkingPlacement.Behind),
                new(Hash, CustomMarkingPlacement.Skin),
                new(OtherHash, CustomMarkingPlacement.Skin),
                new(Hash, CustomMarkingPlacement.Hair),
            });
            var species = client.ResolveDependency<IPrototypeManager>().Index<SpeciesPrototype>(profile.Species);
            var doll = entMan.SpawnEntity(species.DollPrototype, MapCoordinates.Nullspace);
            entMan.System<HumanoidAppearanceSystem>().LoadProfile(doll, profile);

            var sprite = new Entity<SpriteComponent>(doll, entMan.GetComponent<SpriteComponent>(doll));
            var at = Layers(4);
            Assert.Multiple(() =>
            {
                Assert.That(at[0], Is.LessThan(Index(HumanoidVisualLayers.Chest)), "behind the body");
                Assert.That(at[1], Is.GreaterThan(Index(HumanoidVisualLayers.LLeg)), "over every body part");
                Assert.That(at[2], Is.EqualTo(at[1] + 1), "a later marking draws over an earlier one");
                Assert.That(at[2] + 1, Is.EqualTo(Math.Min(Index(HumanoidVisualLayers.UndergarmentTop), Index(HumanoidVisualLayers.UndergarmentBottom))), "under underwear and clothing");
                Assert.That(at[3], Is.GreaterThan(Index(HumanoidVisualLayers.Hair)), "over the hair");
                Assert.That(at[3], Is.LessThan(Index("head")), "under hats");
            });

            // A rebuild replaces the layers rather than stacking more.
            var count = sprite.Comp.AllLayers.Count();
            entMan.System<HumanoidAppearanceSystem>().LoadProfile(doll, profile.WithCustomMarkings(new List<CustomMarking>
            {
                new(Hash, CustomMarkingPlacement.Hands),
                new(OtherHash, CustomMarkingPlacement.Front),
            }));
            at = Layers(2);
            Assert.Multiple(() =>
            {
                Assert.That(sprites.LayerMapTryGet(sprite, CustomMarkingSystem.LayerKey(2), out _, false), Is.False, "old layers are gone");
                Assert.That(sprite.Comp.AllLayers.Count(), Is.EqualTo(count - 2));
                Assert.That(at[0], Is.GreaterThan(Index(HumanoidVisualLayers.RHand)), "over the hands");
                Assert.That(at[0], Is.LessThan(Index("gloves")), "under gloves");
                Assert.That(at[1], Is.GreaterThan(Index("outerClothing")), "over clothing");
            });

            entMan.DeleteEntity(doll);
            return;

            int[] Layers(int worn)
            {
                var found = new int[worn];
                for (var i = 0; i < worn; i++)
                {
                    Assert.That(sprites.LayerMapTryGet(sprite, CustomMarkingSystem.LayerKey(i), out found[i], false), Is.True, $"marking {i} has a layer");
                    Assert.That(sprite.Comp[found[i]].RsiState.Name, Is.EqualTo(CustomMarkingResources.State));
                    Assert.That(sprite.Comp[found[i]].Visible, Is.True);
                }

                return found;
            }

            int Index(object key)
            {
                var found = key is string name
                    ? sprites.LayerMapTryGet(sprite, name, out var index, false)
                    : sprites.LayerMapTryGet(sprite, (Enum) key, out index, false);
                Assert.That(found, Is.True, $"the body has a {key} layer");
                return index;
            }
        });

        await pair.CleanReturnAsync();
    }

    private static void Store(IResourceCache resCache, string hash)
    {
        // One pixel on the body, which is what shows, and one far off it.
        var art = new CustomMarkingArt();
        art.SetPixel(0, CustomMarkingArt.South, 15, 15, new Rgba32(200, 30, 30, 255));
        art.SetPixel(0, CustomMarkingArt.West, 3, 4, new Rgba32(1, 2, 3, 255));
        CustomMarkingResources.For(resCache).Store(hash, art.ToPng());
    }
}
