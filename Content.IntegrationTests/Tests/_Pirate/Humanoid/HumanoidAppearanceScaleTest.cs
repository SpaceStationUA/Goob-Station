// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Client.Humanoid;
using Content.IntegrationTests.Pair;
using Content.Shared.Humanoid;
using Content.Shared.Preferences;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Pirate.Humanoid;

[TestFixture]
public sealed class HumanoidAppearanceScaleTest
{
    [TestCase(0.9f, 1.05f)]
    [TestCase(1.2f, 0.95f)]
    public async Task LobbyPreviewUsesProfileDimensionsAndCanReset(float height, float width)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;

        await client.WaitAssertion(() =>
        {
            var dummy = client.EntMan.Spawn("MobHumanDummy");
            var appearance = client.System<HumanoidAppearanceSystem>();
            var profile = HumanoidCharacterProfile.DefaultWithSpecies("Human").WithHeight(height).WithWidth(width);
            var humanoid = client.EntMan.GetComponent<HumanoidAppearanceComponent>(dummy);
            var sprite = client.EntMan.GetComponent<SpriteComponent>(dummy);

            appearance.LoadProfile(dummy, profile);
            Assert.That(humanoid.Height, Is.EqualTo(height));
            Assert.That(humanoid.Width, Is.EqualTo(width));
            Assert.That(sprite.Scale, Is.EqualTo(Vector2.One));

            appearance.UpdateSprite((dummy, humanoid, sprite));
            Assert.That(sprite.Scale, Is.EqualTo(Vector2.One), "Refreshing appearance keeps the sprite from shimmering during movement.");

            appearance.LoadProfile(dummy, profile.WithHeight(1f).WithWidth(1f));
            Assert.That(sprite.Scale, Is.EqualTo(Vector2.One), "Resetting the profile restores the default size.");

            client.EntMan.DeleteEntity(dummy);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SpawnedCharacterUsesReplicatedProfileDimensions()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var client = pair.Client;
        var appearance = server.System<SharedHumanoidAppearanceSystem>();
        EntityUid body = default;

        await server.WaitPost(() =>
        {
            body = server.EntMan.SpawnEntity("MobHuman", map.GridCoords);
            var profile = HumanoidCharacterProfile.DefaultWithSpecies("Human").WithHeight(1.15f).WithWidth(0.95f);
            appearance.LoadProfile(body, profile);
            server.PlayerMan.SetAttachedEntity(pair.Player!, body);
        });
        await pair.RunTicksSync(10);

        var clientBody = pair.ToClientUid(body);
        await client.WaitAssertion(() =>
        {
            var humanoid = client.EntMan.GetComponent<HumanoidAppearanceComponent>(clientBody);
            var sprite = client.EntMan.GetComponent<SpriteComponent>(clientBody);
            Assert.That(humanoid.Height, Is.EqualTo(1.15f));
            Assert.That(humanoid.Width, Is.EqualTo(0.95f));
            Assert.That(sprite.Scale, Is.EqualTo(Vector2.One));
        });

        await server.WaitPost(() => appearance.SetHeight(body, 0.92f));
        await pair.RunTicksSync(10);

        await client.WaitAssertion(() =>
        {
            var humanoid = client.EntMan.GetComponent<HumanoidAppearanceComponent>(clientBody);
            var sprite = client.EntMan.GetComponent<SpriteComponent>(clientBody);
            Assert.That(humanoid.Height, Is.EqualTo(0.92f));
            Assert.That(sprite.Scale, Is.EqualTo(Vector2.One), "Later appearance updates keep the sprite from shimmering during movement.");
        });

        await pair.CleanReturnAsync();
    }
}
