// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using Content.Client._Pirate.CustomMarkings;
using Content.Client.Humanoid;
using Content.Shared._Pirate.CustomMarkings;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
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
using Robust.Shared.Utility;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.IntegrationTests.Tests._Pirate.CustomMarkings;

/// <summary>
/// A marking can erase the body under it: the layers its mask touches are drawn through the erase shader, fed by a
/// layer that holds the mask; nothing else is touched; and a body is never left with too little in sight.
/// </summary>
[TestFixture]
[TestOf(typeof(CustomMarkingSystem))]
public sealed class CustomMarkingBodyEraseTest
{
    private const int Frame = CustomMarkingRules.FrameSize;

    /// <summary>More mask layers than a body can have.</summary>
    private const int MaskKeys = 64;

    private static readonly string PatchHash = new('1', CustomMarkingRules.HashLength);
    private static readonly string AllHash = new('2', CustomMarkingRules.HashLength);
    private static readonly string RedrawnHash = new('3', CustomMarkingRules.HashLength);
    private static readonly string TailHash = new('4', CustomMarkingRules.HashLength);
    private static readonly Rgba32 Red = new(255, 0, 0, 255);

    /// <summary>Art that erases the whole of every facing, and with <paramref name="drawn"/> also draws over all of it.</summary>
    private static CustomMarkingArt Everything(bool drawn)
    {
        var art = new CustomMarkingArt();
        for (var facing = 0; facing < CustomMarkingRules.Facings; facing++)
        {
            for (var y = 0; y < Frame; y++)
            {
                for (var x = 0; x < Frame; x++)
                {
                    art.SetErased(facing, x, y, true);
                    if (drawn)
                        art.SetPixel(0, facing, x, y, Red);
                }
            }
        }

        return art;
    }

    /// <summary>What each of a sprite's mask layers masks, with a check that each sits right under it.</summary>
    private static List<object> Masked(SpriteSystem sprites, EntityUid uid, SpriteComponent sprite)
    {
        var targets = new List<object>();
        for (var i = 0; i < MaskKeys; i++)
        {
            if (!sprites.LayerMapTryGet((uid, sprite), CustomMarkingSystem.EraseKey(i), out var index, false))
                continue;

            var copy = ((SpriteComponent.Layer) sprite[index]).CopyToShaderParameters;
            Assert.That(copy, Is.Not.Null, "a mask layer hands its texture to another layer's shader");

            var target = -1;
            var found = copy.LayerKey switch
            {
                Enum key => sprites.LayerMapTryGet((uid, sprite), key, out target, false),
                string key => sprites.LayerMapTryGet((uid, sprite), key, out target, false),
                _ => false,
            };
            Assert.That(found, Is.True, "which the sprite still has");
            Assert.That(target, Is.EqualTo(index + 1), "and which is drawn right after it");
            targets.Add(copy.LayerKey);
        }

        return targets;
    }

    [Test]
    public async Task BodyIsErasedUnderMarkingTest()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;

        var entMan = client.EntMan;
        var sprites = client.System<SpriteSystem>();
        var system = client.System<CustomMarkingSystem>();
        EntityUid doll = default;
        SpriteComponent sprite = default!;
        HumanoidAppearanceComponent humanoid = default!;

        await client.WaitAssertion(() =>
        {
            var humanoids = entMan.System<HumanoidAppearanceSystem>();
            var resources = CustomMarkingResources.For(client.ResolveDependency<IResourceCache>());

            // A marking that draws nothing and erases a patch in the middle of the torso, seen from the front.
            var patch = new CustomMarkingArt();
            for (var y = 15; y <= 18; y++)
            {
                for (var x = 14; x <= 17; x++)
                {
                    patch.SetErased(CustomMarkingArt.South, x, y, true);
                }
            }

            system.Remember(PatchHash, patch);
            system.Remember(AllHash, Everything(false));
            system.Remember(RedrawnHash, Everything(true));
            var hairPatch = patch.Clone();
            hairPatch.SetPixel(0, CustomMarkingArt.South, 14, 15, Red);
            var hairHash = Content.Server._Pirate.CustomMarkings.CustomMarkingSystem.Hash(hairPatch);
            system.Remember(hairHash, hairPatch);

            var profile = HumanoidCharacterProfile.DefaultWithSpecies("Human");
            var species = client.ResolveDependency<IPrototypeManager>().Index<SpeciesPrototype>(profile.Species);
            doll = entMan.SpawnEntity(species.DollPrototype, MapCoordinates.Nullspace);
            humanoids.LoadProfile(doll, profile.WithCustomMarkings(new List<CustomMarking> { new(PatchHash, CustomMarkingPlacement.Skin) }));
            humanoid = entMan.GetComponent<HumanoidAppearanceComponent>(doll);
            sprite = entMan.GetComponent<SpriteComponent>(doll);

            var masked = Masked(sprites, doll, sprite);
            Assert.Multiple(() =>
            {
                Assert.That(system.EraseEnabled, Is.True);
                Assert.That(masked, Does.Contain(HumanoidVisualLayers.Chest), "the torso is drawn through the mask");
                Assert.That(masked, Does.Not.Contain(HumanoidVisualLayers.Head), "a part the mask doesn't touch is left alone");
                Assert.That(masked, Does.Not.Contain(HumanoidVisualLayers.LLeg));
                Assert.That(Layer(HumanoidVisualLayers.Chest).ShaderPrototype, Is.EqualTo(CustomMarkingSystem.EraseShader));
                Assert.That(Layer(HumanoidVisualLayers.Chest).Shader, Is.Not.Null);
                Assert.That(Layer(HumanoidVisualLayers.Head).ShaderPrototype, Is.Null);
                Assert.That(Layer(HumanoidVisualLayers.Head).Shader, Is.Null);
                Assert.That(sprites.LayerMapTryGet((doll, sprite), CustomMarkingSystem.LayerKey(0), out _, false), Is.False,
                    "a marking that only erases has nothing to draw");
            });

            // The mask layer: never drawn, it only holds the mask for the shader of the layer above it.
            Assert.That(sprites.LayerMapTryGet((doll, sprite), CustomMarkingSystem.EraseKey(0), out var maskIndex, false), Is.True);
            var mask = (SpriteComponent.Layer) sprite[maskIndex];
            Assert.Multiple(() =>
            {
                Assert.That(mask.CopyToShaderParameters!.ParameterTexture, Is.EqualTo(CustomMarkingSystem.EraseMaskParameter));
                Assert.That(mask.CopyToShaderParameters.ParameterUV, Is.EqualTo(CustomMarkingSystem.EraseUVParameter));
                Assert.That(mask.Visible, Is.True, "it has to count as showing for its texture to be handed over");
                Assert.That(sprites.IsVisible(mask), Is.False, "but it is not drawn, and not clicked on");
                Assert.That(mask.ActualRsi!.Size, Is.EqualTo(new Vector2i(Frame, Frame)));
                Assert.That(mask.ActualRsi.TryGetState(CustomMarkingResources.State, out var state) && state.RsiDirections == RsiDirectionType.Dir4,
                    Is.True, "it turns with the body");
            });

            // Its sheet holds the mask: the patch, in the south facing's cell, and nothing else.
            Assert.That(resources.TryGetPng(mask.ActualRsi!.Path.FilenameWithoutExtension, out var png), Is.True);
            using (var sheet = Image.Load<Rgba32>(png))
            {
                var opaque = 0;
                for (var y = 0; y < sheet.Height; y++)
                {
                    for (var x = 0; x < sheet.Width; x++)
                    {
                        if (sheet[x, y].A != 0)
                            opaque++;
                    }
                }

                Assert.Multiple(() =>
                {
                    Assert.That((sheet.Width, sheet.Height), Is.EqualTo((Frame * 2, Frame * 2)));
                    Assert.That(sheet[14, 15].A, Is.EqualTo(255));
                    Assert.That(sheet[17, 18].A, Is.EqualTo(255));
                    Assert.That(opaque, Is.EqualTo(16));
                });
            }

            // What erasing is measured against, and what it can apply to.
            var body = system.GetBodyMask((doll, sprite));
            var erasable = new HashSet<int>();
            system.GetErasable((doll, sprite), erasable);
            Assert.That(body, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(CustomMarkingErase.Get(body, CustomMarkingArt.South, 15, 16), Is.True, "the torso is body");
                Assert.That(CustomMarkingErase.Get(body, CustomMarkingArt.South, 0, 0), Is.False);
                Assert.That(erasable, Does.Contain(Index(HumanoidVisualLayers.Chest)));
                Assert.That(erasable, Does.Contain(Index(HumanoidVisualLayers.Head)));
                Assert.That(erasable, Does.Contain(Index(HumanoidVisualLayers.Eyes)), "eyes go with the head they are on");
                Assert.That(sprites.LayerMapTryGet((doll, sprite), "jumpsuit", out var jumpsuit, false) && !erasable.Contains(jumpsuit),
                    Is.True, "clothing is never erased");
            });

            // Hidden hair-placement art must not erase exposed body parts, including erase-only art.
            foreach (var hash in new[] { PatchHash, hairHash })
            {
                Wear(new CustomMarking(hash, CustomMarkingPlacement.Hair));
                Assert.That(Masked(sprites, doll, sprite), Does.Contain(HumanoidVisualLayers.Chest));
                humanoids.SetLayerVisibility(doll, HumanoidVisualLayers.Hair, false);
                Assert.That(Masked(sprites, doll, sprite), Is.Empty, "headgear hides the erase mask with its artwork");
                humanoids.SetLayerVisibility(doll, HumanoidVisualLayers.Hair, true);
                Assert.That(Masked(sprites, doll, sprite), Does.Contain(HumanoidVisualLayers.Chest));
                humanoid.PermanentlyHidden.Add(HumanoidVisualLayers.Hair);
                system.Refresh((doll, humanoid));
                Assert.That(Masked(sprites, doll, sprite), Is.Empty, "permanently hidden hair cannot contribute an erase mask");
                humanoid.PermanentlyHidden.Remove(HumanoidVisualLayers.Hair);
                system.Refresh((doll, humanoid));
            }

            // Taken off, the body is drawn as it was.
            Wear();
            Assert.Multiple(() =>
            {
                Assert.That(Masked(sprites, doll, sprite), Is.Empty);
                Assert.That(Layer(HumanoidVisualLayers.Chest).ShaderPrototype, Is.Null);
                Assert.That(Layer(HumanoidVisualLayers.Chest).Shader, Is.Null);
            });

            // Erasing the whole body and drawing nothing would make it invisible: nothing is erased at all.
            Wear(new CustomMarking(AllHash, CustomMarkingPlacement.Skin));
            Assert.Multiple(() =>
            {
                Assert.That(Masked(sprites, doll, sprite), Is.Empty);
                Assert.That(Layer(HumanoidVisualLayers.Chest).ShaderPrototype, Is.Null);
            });

            // Drawn over, the body is still all there, so all of it may go.
            Wear(new CustomMarking(RedrawnHash, CustomMarkingPlacement.Skin));
            masked = Masked(sprites, doll, sprite);
            Assert.Multiple(() =>
            {
                Assert.That(sprites.LayerMapTryGet((doll, sprite), CustomMarkingSystem.LayerKey(0), out _, false), Is.True);
                foreach (var part in new[]
                         {
                             HumanoidVisualLayers.Chest, HumanoidVisualLayers.Head, HumanoidVisualLayers.RArm, HumanoidVisualLayers.LArm,
                             HumanoidVisualLayers.RLeg, HumanoidVisualLayers.LLeg, HumanoidVisualLayers.Eyes,
                         })
                {
                    Assert.That(masked, Does.Contain(part), $"{part} is erased");
                    Assert.That(Layer(part).ShaderPrototype, Is.EqualTo(CustomMarkingSystem.EraseShader));
                }

                Assert.That(masked, Has.Count.EqualTo(masked.Distinct().Count()), "each layer has one mask");
            });

            // The whole of that body with one more marking, which erases the same and draws nothing: still enough.
            Wear(new CustomMarking(RedrawnHash, CustomMarkingPlacement.Skin), new CustomMarking(AllHash, CustomMarkingPlacement.Front));
            Assert.That(Masked(sprites, doll, sprite), Does.Contain(HumanoidVisualLayers.Chest));

            // A part that is drawn unshaded stays so under the mask, and goes back to it after.
            Wear();
            sprite.LayerSetShader(Index(HumanoidVisualLayers.Head), SpriteSystem.UnshadedId.Id);
            // And a part with a shader of its own is left out: erasing would take the shader's place.
            sprite.LayerSetShader(Index(HumanoidVisualLayers.LLeg), "DisplacedStencilDraw");

            humanoid.CustomMarkings.Add(new CustomMarking(RedrawnHash, CustomMarkingPlacement.Skin));
            system.Refresh((doll, humanoid));
            masked = Masked(sprites, doll, sprite);
            Assert.Multiple(() =>
            {
                Assert.That(Layer(HumanoidVisualLayers.Head).ShaderPrototype, Is.EqualTo(CustomMarkingSystem.EraseShaderUnshaded));
                Assert.That(masked, Does.Contain(HumanoidVisualLayers.Head));
                Assert.That(Layer(HumanoidVisualLayers.LLeg).ShaderPrototype?.Id, Is.EqualTo("DisplacedStencilDraw"));
                Assert.That(masked, Does.Not.Contain(HumanoidVisualLayers.LLeg));
                Assert.That(masked, Does.Contain(HumanoidVisualLayers.RLeg));
            });

            humanoid.CustomMarkings.Clear();
            system.Refresh((doll, humanoid));
            Assert.Multiple(() =>
            {
                Assert.That(Layer(HumanoidVisualLayers.Head).ShaderPrototype, Is.EqualTo(SpriteSystem.UnshadedId));
                Assert.That(Layer(HumanoidVisualLayers.Head).Shader, Is.Null);
                Assert.That(Layer(HumanoidVisualLayers.LLeg).ShaderPrototype?.Id, Is.EqualTo("DisplacedStencilDraw"));
                Assert.That(Layer(HumanoidVisualLayers.RLeg).ShaderPrototype, Is.Null);
            });

            sprite.LayerSetShader(Index(HumanoidVisualLayers.Head), (ShaderInstance) null);
            sprite.LayerSetShader(Index(HumanoidVisualLayers.LLeg), (ShaderInstance) null);
            Wear(new CustomMarking(RedrawnHash, CustomMarkingPlacement.Skin));
            Assert.That(Masked(sprites, doll, sprite), Does.Contain(HumanoidVisualLayers.LLeg));
            return;

            void Wear(params CustomMarking[] worn)
            {
                humanoids.LoadProfile(doll, profile.WithCustomMarkings(worn.ToList()));
            }
        });

        // The server's setting turns erasing off for bodies already drawn, and back on.
        await server.WaitPost(() => server.CfgMan.SetCVar(CustomMarkingCVars.EraseBody, false));
        try
        {
            await pair.RunTicksSync(10);
            await client.WaitAssertion(() =>
            {
                Assert.That(system.EraseEnabled, Is.False);
                Assert.That(Masked(sprites, doll, sprite), Is.Empty);
                Assert.That(Layer(HumanoidVisualLayers.Chest).ShaderPrototype, Is.Null);
                Assert.That(sprites.LayerMapTryGet((doll, sprite), CustomMarkingSystem.LayerKey(0), out _, false), Is.True,
                    "the marking itself is still drawn");
            });
        }
        finally
        {
            await server.WaitPost(() => server.CfgMan.SetCVar(CustomMarkingCVars.EraseBody, true));
            await pair.RunTicksSync(10);
        }

        await client.WaitAssertion(() =>
        {
            Assert.That(Masked(sprites, doll, sprite), Does.Contain(HumanoidVisualLayers.Chest));
            entMan.DeleteEntity(doll);
        });

        await pair.CleanReturnAsync();
        return;

        int Index(HumanoidVisualLayers part)
        {
            Assert.That(sprites.LayerMapTryGet((doll, sprite), part, out var index, false), Is.True, $"the body has a {part} layer");
            return index;
        }

        SpriteComponent.Layer Layer(HumanoidVisualLayers part)
        {
            return (SpriteComponent.Layer) sprite[Index(part)];
        }
    }

    /// <summary>The body's own markings are erased like its parts: a tail, for as long as it shows.</summary>
    [Test]
    public async Task TailIsErasedTest()
    {
        const string tail = "LizardTailSmooth";

        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;

        await client.WaitAssertion(() =>
        {
            var entMan = client.EntMan;
            var sprites = entMan.System<SpriteSystem>();
            var humanoids = entMan.System<HumanoidAppearanceSystem>();
            var system = entMan.System<CustomMarkingSystem>();
            var tailSprites = client.ResolveDependency<MarkingManager>().Markings[tail].Sprites;

            var art = Everything(true);
            system.Remember(TailHash, art);

            var profile = HumanoidCharacterProfile.DefaultWithSpecies("Reptilian");
            profile = profile
                .WithCharacterAppearance(profile.Appearance.WithMarkings(new List<Marking> { new(tail, tailSprites.Count) }))
                // On the skin, so it stays shown, and the body covered, when the tail is hidden further down.
                .WithCustomMarkings(new List<CustomMarking> { new(TailHash, CustomMarkingPlacement.Skin) });
            var species = client.ResolveDependency<IPrototypeManager>().Index<SpeciesPrototype>(profile.Species);
            var doll = entMan.SpawnEntity(species.DollPrototype, MapCoordinates.Nullspace);
            humanoids.LoadProfile(doll, profile);

            var humanoid = entMan.GetComponent<HumanoidAppearanceComponent>(doll);
            var sprite = entMan.GetComponent<SpriteComponent>(doll);
            var keys = tailSprites.OfType<SpriteSpecifier.Rsi>().Select(drawn => $"{tail}-{drawn.RsiState}").ToList();
            Assert.That(keys, Is.Not.Empty);

            var masked = Masked(sprites, doll, sprite);
            Assert.That(masked, Does.Contain(HumanoidVisualLayers.Chest));
            foreach (var key in keys)
            {
                Assert.That(sprites.LayerMapTryGet((doll, sprite), key, out var index, false), Is.True, $"the body shows {key}");
                Assert.That(masked, Does.Contain(key), "a tail sprite is drawn through the mask");
                Assert.That(((SpriteComponent.Layer) sprite[index]).ShaderPrototype, Is.EqualTo(CustomMarkingSystem.EraseShader));
            }

            // Hidden, as under a hardsuit, the tail has nothing to erase, and is left as it was.
            humanoids.SetLayerVisibility((doll, humanoid), HumanoidVisualLayers.Tail, false);
            masked = Masked(sprites, doll, sprite);
            foreach (var key in keys)
            {
                Assert.That(masked, Does.Not.Contain(key));
                Assert.That(sprites.LayerMapTryGet((doll, sprite), key, out var index, false), Is.True);
                Assert.That(((SpriteComponent.Layer) sprite[index]).ShaderPrototype, Is.Null);
            }

            Assert.That(masked, Does.Contain(HumanoidVisualLayers.Chest), "the body under it still is");
            entMan.DeleteEntity(doll);
        });

        await pair.CleanReturnAsync();
    }
}
