// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using Content.Client._Pirate.CustomMarkings;
using Content.Client.Clickable;
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
using SixLabors.ImageSharp.PixelFormats;

namespace Content.IntegrationTests.Tests._Pirate.CustomMarkings;

/// <summary>
/// A marking only shows on the body and the margin around it, and a body part that is hidden takes its section of
/// the marking along, margin and all.
/// </summary>
[TestFixture]
[TestOf(typeof(CustomMarkingSystem))]
public sealed class CustomMarkingLimbsTest
{
    private const int Frame = CustomMarkingRules.FrameSize;

    private static readonly string Hash = new('c', CustomMarkingRules.HashLength);
    private static readonly string FittingHash = new('e', CustomMarkingRules.HashLength);
    private static readonly Rgba32 Red = new(255, 0, 0, 255);

    [Test]
    public async Task ArtStaysOnBodyAndLeavesWithLimbTest()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;

        await client.WaitAssertion(() =>
        {
            var entMan = client.EntMan;
            var sprites = entMan.System<SpriteSystem>();
            var humanoids = entMan.System<HumanoidAppearanceSystem>();
            var system = entMan.System<CustomMarkingSystem>();
            var clickMap = client.ResolveDependency<IClickMapManager>();
            var resources = CustomMarkingResources.For(client.ResolveDependency<IResourceCache>());

            // A drawing that covers every pixel of every facing, so whatever is cut out shows.
            var art = new CustomMarkingArt();
            for (var facing = 0; facing < CustomMarkingRules.Facings; facing++)
            {
                for (var y = 0; y < Frame; y++)
                {
                    for (var x = 0; x < Frame; x++)
                    {
                        art.SetPixel(0, facing, x, y, Red);
                    }
                }
            }

            system.Remember(Hash, art);

            // And one that sits on the middle of the torso.
            var fitting = new CustomMarkingArt();
            fitting.SetPixel(0, CustomMarkingArt.South, 15, 15, Red);
            system.Remember(FittingHash, fitting);

            var profile = HumanoidCharacterProfile.DefaultWithSpecies("Human");
            var species = client.ResolveDependency<IPrototypeManager>().Index<SpeciesPrototype>(profile.Species);
            var doll = entMan.SpawnEntity(species.DollPrototype, MapCoordinates.Nullspace);
            humanoids.LoadProfile(doll, profile.WithCustomMarkings(new List<CustomMarking> { new(Hash, CustomMarkingPlacement.Skin) }));

            var humanoid = entMan.GetComponent<HumanoidAppearanceComponent>(doll);
            var sprite = new Entity<SpriteComponent>(doll, entMan.GetComponent<SpriteComponent>(doll));

            Assert.That(system.TryGetArt(Hash, out var whole), Is.True);
            var sections = system.GetSections((sprite.Owner, sprite.Comp), CustomMarkingPlacement.Skin);
            Assert.That(sections, Is.Not.Null);

            // The mirror for drawing stands on the body's middle column, front and back.
            Assert.That(system.MirrorAxis((sprite.Owner, sprite.Comp), CustomMarkingArt.South), Is.EqualTo(CustomMarkingSketch.DefaultMirrorAxis));
            Assert.That(system.MirrorAxis((sprite.Owner, sprite.Comp), CustomMarkingArt.North), Is.EqualTo(CustomMarkingSketch.DefaultMirrorAxis));

            // On a whole body the drawing is cut to the body and the margin around it.
            var fittedRsi = Shown().ActualRsi;
            Assert.That(fittedRsi, Is.Not.SameAs(whole), "art past the body's reach is cut off");
            var fitted = Read(fittedRsi);
            var onBody = 0;
            var inReach = 0;
            for (var facing = 0; facing < CustomMarkingRules.Facings; facing++)
            {
                for (var y = 0; y < Frame; y++)
                {
                    for (var x = 0; x < Frame; x++)
                    {
                        var reach = Section(facing, x, y) != CustomMarkingSections.None;
                        var body = OnAnyPart(facing, x, y);
                        inReach += reach ? 1 : 0;
                        onBody += body ? 1 : 0;
                        Assert.That(fitted.GetPixel(0, facing, x, y).A != 0, Is.EqualTo(reach), $"facing {facing}, pixel {x},{y}");
                        if (body)
                            Assert.That(reach, Is.True, $"facing {facing}, pixel {x},{y} is on the body");
                    }
                }
            }

            Assert.Multiple(() =>
            {
                Assert.That(fitted.GetPixel(0, CustomMarkingArt.South, 15, 15), Is.EqualTo(Red), "the middle of the torso");
                Assert.That(fitted.GetPixel(0, CustomMarkingArt.South, 0, 0).A, Is.Zero, "a corner far from the body");
                Assert.That(inReach, Is.GreaterThan(onBody), "there is a margin around the body");
                Assert.That(inReach, Is.LessThan(CustomMarkingRules.Facings * Frame * Frame * 3 / 4), "and most of the canvas is out of it");
            });

            // The left arm's section: the number at a pixel where the arm is the top part under the art.
            byte arm = CustomMarkingSections.None;
            for (var y = 0; y < Frame && arm == CustomMarkingSections.None; y++)
            {
                for (var x = 0; x < Frame; x++)
                {
                    if (!Opaque(HumanoidVisualLayers.LArm, CustomMarkingArt.South, x, y)
                        || Opaque(HumanoidVisualLayers.RLeg, CustomMarkingArt.South, x, y)
                        || Opaque(HumanoidVisualLayers.LLeg, CustomMarkingArt.South, x, y))
                        continue;

                    arm = Section(CustomMarkingArt.South, x, y);
                    break;
                }
            }

            Assert.That(arm, Is.Not.EqualTo(CustomMarkingSections.None), "the arm has a section");

            // The arm goes, and its section with it, margin and all.
            humanoids.SetLayerVisibility((doll, humanoid), HumanoidVisualLayers.LArm, false);
            var cutRsi = Shown().ActualRsi;
            Assert.That(cutRsi, Is.Not.SameAs(fittedRsi));
            var cut = Read(cutRsi);
            var gone = 0;
            var marginGone = 0;
            for (var facing = 0; facing < CustomMarkingRules.Facings; facing++)
            {
                for (var y = 0; y < Frame; y++)
                {
                    for (var x = 0; x < Frame; x++)
                    {
                        var section = Section(facing, x, y);
                        var stays = section != CustomMarkingSections.None && section != arm;
                        Assert.That(cut.GetPixel(0, facing, x, y).A != 0, Is.EqualTo(stays), $"facing {facing}, pixel {x},{y}");
                        if (section != arm)
                            continue;

                        gone++;
                        if (!Opaque(HumanoidVisualLayers.LArm, facing, x, y))
                            marginGone++;
                    }
                }
            }

            Assert.That(gone, Is.GreaterThan(0), "the arm had art on it");
            Assert.That(marginGone, Is.GreaterThan(0), "and around it");

            // The same body again reuses the copy rather than cutting a new one.
            system.Refresh((doll, humanoid));
            Assert.That(Shown().ActualRsi, Is.SameAs(cutRsi));

            // The arm comes back, and its section with it.
            humanoids.SetLayerVisibility((doll, humanoid), HumanoidVisualLayers.LArm, true);
            Assert.That(Shown().ActualRsi, Is.SameAs(fittedRsi));

            // A drawing that already fits the body is drawn as it is, with no copy made.
            humanoids.LoadProfile(doll, profile.WithCustomMarkings(new List<CustomMarking> { new(FittingHash, CustomMarkingPlacement.Skin) }));
            Assert.That(system.TryGetArt(FittingHash, out var fits), Is.True);
            Assert.That(Shown().ActualRsi, Is.SameAs(fits));

            // Every placement is held to the body, the one behind it too.
            humanoids.LoadProfile(doll, profile.WithCustomMarkings(new List<CustomMarking> { new(Hash, CustomMarkingPlacement.Behind) }));
            var behind = Read(Shown().ActualRsi);
            Assert.That(behind.GetPixel(0, CustomMarkingArt.South, 0, 0).A, Is.Zero);
            Assert.That(behind.GetPixel(0, CustomMarkingArt.South, 15, 15), Is.EqualTo(Red));

            entMan.DeleteEntity(doll);
            return;

            SpriteComponent.Layer Shown()
            {
                Assert.That(sprites.LayerMapTryGet((sprite.Owner, sprite.Comp), CustomMarkingSystem.LayerKey(0), out var index, false), Is.True);
                return (SpriteComponent.Layer) sprite.Comp[index];
            }

            CustomMarkingArt Read(RSI rsi)
            {
                Assert.That(resources.TryGetPng(rsi.Path.FilenameWithoutExtension, out var png), Is.True);
                var read = CustomMarkingPng.Read(png);
                Assert.That(read, Is.Not.Null);
                return read;
            }

            byte Section(int facing, int x, int y)
            {
                return sections[CustomMarkingSections.Index(facing, x, y)];
            }

            bool Opaque(HumanoidVisualLayers part, int facing, int x, int y)
            {
                if (!sprites.LayerMapTryGet((sprite.Owner, sprite.Comp), part, out var index, false)
                    || sprite.Comp[index] is not SpriteComponent.Layer { ActualRsi: { } rsi } layer
                    || !rsi.TryGetState(layer.State, out var state))
                    return false;

                var direction = state.RsiDirections == RsiDirectionType.Dir1 ? RsiDirection.South : (RsiDirection) facing;
                return clickMap.IsOpaque(rsi, layer.State, direction, 0, new Vector2i(x, y));
            }

            bool OnAnyPart(int facing, int x, int y)
            {
                return Opaque(HumanoidVisualLayers.Chest, facing, x, y)
                       || Opaque(HumanoidVisualLayers.Head, facing, x, y)
                       || Opaque(HumanoidVisualLayers.RArm, facing, x, y)
                       || Opaque(HumanoidVisualLayers.LArm, facing, x, y)
                       || Opaque(HumanoidVisualLayers.RHand, facing, x, y)
                       || Opaque(HumanoidVisualLayers.LHand, facing, x, y)
                       || Opaque(HumanoidVisualLayers.RLeg, facing, x, y)
                       || Opaque(HumanoidVisualLayers.LLeg, facing, x, y)
                       || Opaque(HumanoidVisualLayers.RFoot, facing, x, y)
                       || Opaque(HumanoidVisualLayers.LFoot, facing, x, y);
            }
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>A tail is in a marking's reach, past the body, for as long as it shows.</summary>
    [Test]
    public async Task TailIsInReachTest()
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
            var clickMap = client.ResolveDependency<IClickMapManager>();
            var resources = CustomMarkingResources.For(client.ResolveDependency<IResourceCache>());
            var tailSprites = client.ResolveDependency<MarkingManager>().Markings[tail].Sprites;

            var art = new CustomMarkingArt();
            for (var facing = 0; facing < CustomMarkingRules.Facings; facing++)
            {
                for (var y = 0; y < Frame; y++)
                {
                    for (var x = 0; x < Frame; x++)
                    {
                        art.SetPixel(0, facing, x, y, Red);
                    }
                }
            }

            var hash = new string('f', CustomMarkingRules.HashLength);
            system.Remember(hash, art);

            var profile = HumanoidCharacterProfile.DefaultWithSpecies("Reptilian");
            profile = profile
                .WithCharacterAppearance(profile.Appearance.WithMarkings(new List<Marking> { new(tail, tailSprites.Count) }))
                .WithCustomMarkings(new List<CustomMarking> { new(hash, CustomMarkingPlacement.Front) });
            var species = client.ResolveDependency<IPrototypeManager>().Index<SpeciesPrototype>(profile.Species);
            var doll = entMan.SpawnEntity(species.DollPrototype, MapCoordinates.Nullspace);
            humanoids.LoadProfile(doll, profile);

            var humanoid = entMan.GetComponent<HumanoidAppearanceComponent>(doll);
            var sprite = new Entity<SpriteComponent>(doll, entMan.GetComponent<SpriteComponent>(doll));

            // A pixel the tail covers that is further from the body than the body's own margin reaches.
            (int Facing, int X, int Y)? found = null;
            for (var facing = 0; facing < CustomMarkingRules.Facings && found == null; facing++)
            {
                for (var y = 0; y < Frame && found == null; y++)
                {
                    for (var x = 0; x < Frame; x++)
                    {
                        if (!OnTail(facing, x, y) || NearBody(facing, x, y))
                            continue;

                        found = (facing, x, y);
                        break;
                    }
                }
            }

            Assert.That(found, Is.Not.Null, "the tail sticks out past the body");
            var at = CustomMarkingSections.Index(found.Value.Facing, found.Value.X, found.Value.Y);

            var sections = system.GetSections((sprite.Owner, sprite.Comp), CustomMarkingPlacement.Front);
            Assert.That(sections[at], Is.Not.EqualTo(CustomMarkingSections.None), "the tail is in reach");
            Assert.That(Shown().GetPixel(0, found.Value.Facing, found.Value.X, found.Value.Y), Is.EqualTo(Red), "and art on it shows");

            // Hidden, as under a hardsuit, the tail is out of reach and the art there with it.
            humanoids.SetLayerVisibility((doll, humanoid), HumanoidVisualLayers.Tail, false);
            sections = system.GetSections((sprite.Owner, sprite.Comp), CustomMarkingPlacement.Front);
            Assert.That(sections[at], Is.EqualTo(CustomMarkingSections.None));
            Assert.That(Shown().GetPixel(0, found.Value.Facing, found.Value.X, found.Value.Y).A, Is.Zero);
            Assert.That(Shown().GetPixel(0, CustomMarkingArt.South, 15, 15), Is.EqualTo(Red), "art on the body stays");

            entMan.DeleteEntity(doll);
            return;

            CustomMarkingArt Shown()
            {
                Assert.That(sprites.LayerMapTryGet((sprite.Owner, sprite.Comp), CustomMarkingSystem.LayerKey(0), out var index, false), Is.True);
                var rsi = ((SpriteComponent.Layer) sprite.Comp[index]).ActualRsi;
                Assert.That(resources.TryGetPng(rsi.Path.FilenameWithoutExtension, out var png), Is.True);
                return CustomMarkingPng.Read(png);
            }

            bool Opaque(SpriteComponent.Layer layer, int facing, int x, int y)
            {
                if (layer.ActualRsi is not { } rsi || !rsi.TryGetState(layer.State, out var state))
                    return false;

                var direction = state.RsiDirections == RsiDirectionType.Dir1 ? RsiDirection.South : (RsiDirection) facing;
                return clickMap.IsOpaque(rsi, layer.State, direction, 0, new Vector2i(x, y));
            }

            bool OnTail(int facing, int x, int y)
            {
                foreach (var specifier in tailSprites)
                {
                    if (specifier is Robust.Shared.Utility.SpriteSpecifier.Rsi drawn
                        && sprites.LayerMapTryGet((sprite.Owner, sprite.Comp), $"{tail}-{drawn.RsiState}", out var index, false)
                        && sprite.Comp[index] is SpriteComponent.Layer { Visible: true } layer
                        && Opaque(layer, facing, x, y))
                        return true;
                }

                return false;
            }

            bool NearBody(int facing, int x, int y)
            {
                for (var dy = -CustomMarkingSections.Margin; dy <= CustomMarkingSections.Margin; dy++)
                {
                    for (var dx = -CustomMarkingSections.Margin; dx <= CustomMarkingSections.Margin; dx++)
                    {
                        foreach (var part in BodyParts)
                        {
                            if (sprites.LayerMapTryGet((sprite.Owner, sprite.Comp), part, out var index, false)
                                && sprite.Comp[index] is SpriteComponent.Layer layer
                                && Opaque(layer, facing, x + dx, y + dy))
                                return true;
                        }
                    }
                }

                return false;
            }
        });

        await pair.CleanReturnAsync();
    }

    private static readonly HumanoidVisualLayers[] BodyParts =
    {
        HumanoidVisualLayers.Chest,
        HumanoidVisualLayers.Head,
        HumanoidVisualLayers.RArm,
        HumanoidVisualLayers.LArm,
        HumanoidVisualLayers.RHand,
        HumanoidVisualLayers.LHand,
        HumanoidVisualLayers.RLeg,
        HumanoidVisualLayers.LLeg,
        HumanoidVisualLayers.RFoot,
        HumanoidVisualLayers.LFoot,
    };
}
