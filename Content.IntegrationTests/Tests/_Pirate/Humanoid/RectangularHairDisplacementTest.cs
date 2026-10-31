using System.Linq;
using Content.Client.Humanoid;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Pirate.Humanoid;

[TestFixture]
public sealed class RectangularHairDisplacementTest
{
    [TestCase("Vox", "WFMeridianHairChunkyQuadBuns", "chunky_quad_buns", 48)]
    [TestCase("NeoVox", "WFMeridianHairChunkyQuadBuns", "chunky_quad_buns", 48)]
    [TestCase("Vox", "WFMeridianHairAfroHuge", "afro_huge", 48)]
    [TestCase("Vox", "WFMeridianHairMohawkTall", "mohawk_tall", 48)]
    [TestCase("Vox", "WFMeridianHairPonytailScythe", "ponytail_scythe", 48)]
    [TestCase("Vox", "WFMeridianHairChunkyPonytail", "chunky_ponytail", 32)]
    public async Task HairKeepsItsDisplacementWhenPreviewIsRefreshed(string species, string marking, string state, int height)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;
        await client.WaitAssertion(() =>
        {
            var em = client.EntMan;
            var prototypes = client.ResolveDependency<IPrototypeManager>();
            var dummy = em.SpawnEntity(prototypes.Index<SpeciesPrototype>(species).DollPrototype, MapCoordinates.Nullspace);
            var appearance = client.System<HumanoidAppearanceSystem>();
            var sprites = client.System<SpriteSystem>();
            var humanoid = em.GetComponent<HumanoidAppearanceComponent>(dummy);
            var sprite = em.GetComponent<SpriteComponent>(dummy);
            var sharedShader = prototypes.Index<ShaderPrototype>("DisplacedDraw").Instance();
            appearance.LoadProfile(dummy, HumanoidCharacterProfile.DefaultWithSpecies(species));
            appearance.AddMarking(dummy, marking, Color.White, sync: false, forced: true);

            int? count = null;
            for (var refresh = 0; refresh < 2; refresh++)
            {
                appearance.UpdateSprite((dummy, humanoid, sprite));
                var key = $"{marking}-{state}";
                Assert.That(sprites.LayerMapTryGet((dummy, sprite), key, out var hairIndex, false), Is.True);
                Assert.That(sprites.LayerMapTryGet((dummy, sprite), $"{key}-displacement", out var mapIndex, false), Is.True);
                Assert.That(sprites.LayerGetEffectiveRsi((dummy, sprite), hairIndex)!.Size, Is.EqualTo(new Vector2i(32, height)));
                Assert.That(sprites.LayerGetEffectiveRsi((dummy, sprite), mapIndex)!.Size, Is.EqualTo(new Vector2i(32, 32)));
                var hair = (SpriteComponent.Layer) sprite[hairIndex];
                Assert.That(hair.ShaderPrototype?.Id, Is.EqualTo("DisplacedDraw"));
                Assert.That(hair.Shader, Is.Not.SameAs(sharedShader), "Frame dimensions must not change another entity's shader.");
                Assert.That(hair.Shader!.Mutable, Is.True);
                Assert.That(sharedShader.Mutable, Is.False);
                count ??= sprite.AllLayers.Count();
                Assert.That(sprite.AllLayers.Count(), Is.EqualTo(count), "Refreshing must replace the displacement layer.");
            }

            em.DeleteEntity(dummy);
        });
        // Pool disposal fails on client warnings, reproducing the former non-square RSI failure deterministically.
        await pair.CleanReturnAsync();
    }
}
