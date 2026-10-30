// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared.Polymorph.Components;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using ServerProjectorSystem = Content.Server.Polymorph.Systems.ChameleonProjectorSystem;

namespace Content.IntegrationTests.Tests._Pirate.Chameleon;

[TestFixture]
public sealed class ChameleonProjectorTest
{
    [TestCase("Wrench", false)]
    [TestCase("DrinkOniGourd", false)]
    [TestCase("CrateGenericSteel", false)]
    [TestCase("Wrench", true)]
    public async Task DisguiseCopiesVisibleSpriteAndReveals(string prototype, bool usePrototypeFallback)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var client = pair.Client;
        var projectorSystem = server.System<ServerProjectorSystem>();
        EntityUid user = default;
        EntityUid source = default;
        EntityUid projector = default;
        EntityUid disguise = default;

        await server.WaitPost(() =>
        {
            user = server.EntMan.SpawnEntity("MobHuman", map.GridCoords);
            source = server.EntMan.SpawnEntity(prototype, map.GridCoords);
            projector = server.EntMan.SpawnEntity("ChameleonProjectorNoBattery", map.GridCoords);
        });
        await pair.RunTicksSync(10);

        await client.WaitAssertion(() =>
        {
            var cSource = pair.ToClientUid(source);
            if (usePrototypeFallback)
            {
                // The original sprite may be unavailable when the disguise enters the client's view.
                client.EntMan.RemoveComponent<SpriteComponent>(cSource);
                return;
            }

            // Use a runtime change so copying just the prototype would not pass.
            client.System<SpriteSystem>().SetColor(cSource, Color.Red);
        });

        await server.WaitAssertion(() =>
        {
            var component = server.EntMan.GetComponent<ChameleonProjectorComponent>(projector);
            Assert.That(projectorSystem.TryDisguise((projector, component), user, source), Is.True);
            disguise = server.EntMan.GetComponent<ChameleonDisguisedComponent>(user).Disguise;
        });
        await pair.RunTicksSync(10);

        await client.WaitAssertion(() =>
        {
            var sprite = client.EntMan.GetComponent<SpriteComponent>(pair.ToClientUid(disguise));
            Assert.Multiple(() =>
            {
                Assert.That(client.EntMan.GetComponent<SpriteComponent>(pair.ToClientUid(user)).Visible, Is.False);
                Assert.That(sprite.Visible, Is.True, "The disguise must replace the hidden player with a visible object.");
                Assert.That(sprite.AddToTree, Is.True, "The disguise must be eligible for world rendering.");
                Assert.That(sprite.AllLayers.Any(layer => layer.Visible && layer.RsiState.IsValid), Is.True,
                    "The disguise must have loaded sprite layers rather than being invisible.");
                Assert.That(sprite.Color, Is.EqualTo(usePrototypeFallback ? Color.White : Color.Red));
            });

            foreach (var layer in sprite.AllLayers.Where(layer => layer.Visible && layer.RsiState.IsValid))
            {
                var rsi = layer.Rsi ?? sprite.BaseRSI;
                Assert.That(rsi, Is.Not.Null);
                Assert.That(rsi!.TryGetState(layer.RsiState, out _), Is.True,
                    "Visible layers must resolve to actual RSI states.");
            }

            if (usePrototypeFallback)
                return;

            var original = client.EntMan.GetComponent<SpriteComponent>(pair.ToClientUid(source));
            Assert.That(sprite.BaseRSI, Is.SameAs(original.BaseRSI));
            Assert.That(sprite.AllLayers.Select(layer => layer.RsiState),
                Is.EqualTo(original.AllLayers.Select(layer => layer.RsiState)));
            Assert.That(sprite.AllLayers.Select(layer => layer.Visible),
                Is.EqualTo(original.AllLayers.Select(layer => layer.Visible)));

            // The disguise is a snapshot, and must not share mutable layers with the original.
            var visibleLayer = sprite.AllLayers.ToList().FindIndex(layer => layer.Visible);
            client.System<SpriteSystem>().LayerSetVisible(pair.ToClientUid(source), visibleLayer, false);
            Assert.That(sprite[visibleLayer].Visible, Is.True);
        });

        await server.WaitAssertion(() => Assert.That(projectorSystem.TryReveal(user), Is.True));
        await pair.RunTicksSync(10);

        await client.WaitAssertion(() =>
        {
            var cUser = pair.ToClientUid(user);
            Assert.That(client.EntMan.HasComponent<ChameleonDisguisedComponent>(cUser), Is.False);
            Assert.That(client.EntMan.GetComponent<SpriteComponent>(cUser).Visible, Is.True,
                "Revealing must restore the player's sprite.");
        });

        await server.WaitPost(() => server.System<SharedMapSystem>().DeleteMap(map.MapId));
        await pair.CleanReturnAsync();
    }
}
