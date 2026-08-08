// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Humanoid;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Pirate.Humanoid;

[TestFixture]
public sealed class HumanoidAppearanceVisibilityTest
{
    [Test]
    public async Task LayerRemainsHiddenUntilAllSourcesAreRemoved()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;
        var appearance = client.System<HumanoidAppearanceSystem>();
        var spriteSystem = client.System<SpriteSystem>();

        await client.WaitAssertion(() =>
        {
            var uid = client.EntMan.Spawn("MobHumanDummy");
            var humanoid = client.EntMan.GetComponent<HumanoidAppearanceComponent>(uid);
            var sprite = client.EntMan.GetComponent<SpriteComponent>(uid);

            appearance.AddMarking(uid,
                "HumanNoseNubby",
                Color.White,
                sync: false,
                forced: true,
                humanoid: humanoid);
            appearance.UpdateSprite((uid, humanoid, sprite));

            Assert.That(spriteSystem.LayerMapTryGet((uid, sprite), HumanoidVisualLayers.Snout, out var snoutLayer, false));
            Assert.That(spriteSystem.LayerMapTryGet((uid, sprite), "HumanNoseNubby-nubby", out var noseLayer, false));
            Assert.That(sprite[snoutLayer].Visible, Is.True);
            Assert.That(sprite[noseLayer].Visible, Is.True);

            appearance.SetLayerVisibility(uid, HumanoidVisualLayers.Snout, false, SlotFlags.HEAD);
            appearance.SetLayerVisibility(uid, HumanoidVisualLayers.Snout, false, SlotFlags.MASK);
            appearance.SetLayerVisibility(uid, HumanoidVisualLayers.Snout, true, SlotFlags.HEAD);

            Assert.That(sprite[snoutLayer].Visible, Is.False);
            Assert.That(sprite[noseLayer].Visible, Is.False);

            appearance.SetLayerVisibility(uid, HumanoidVisualLayers.Snout, true, SlotFlags.MASK);

            Assert.That(sprite[snoutLayer].Visible, Is.True);
            Assert.That(sprite[noseLayer].Visible, Is.True);

            client.EntMan.DeleteEntity(uid);
        });

        await pair.CleanReturnAsync();
    }
}
