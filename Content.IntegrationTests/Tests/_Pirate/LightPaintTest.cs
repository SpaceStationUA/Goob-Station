// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Server.LightPaint;
using Content.Pirate.Shared.LightPaint;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using PointLightComponent = Robust.Server.GameObjects.PointLightComponent;

namespace Content.IntegrationTests.Tests._Pirate;

[TestFixture]
public sealed class LightPaintTest
{
    /// <summary>
    ///     Painting a loose bulb has to change the bulb's own colour, and painting a bulb that is
    ///     installed in a fixture has to reach the fixture's point light, since that is what both
    ///     the emitted light and the fixture's glow layer are driven from.
    /// </summary>
    [Test]
    public async Task PaintingBulbRecoloursBulbAndFixtureLight()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var lightPaint = server.System<LightPaintSystem>();
        var poweredLight = server.System<SharedPoweredLightSystem>();
        var map = await pair.CreateTestMap();

        var paint = Color.FromHex("#FF00FF");

        await server.WaitAssertion(() =>
        {
            // A loose bulb, painted directly.
            var looseBulb = entMan.SpawnEntity("LightTube", map.GridCoords);
            var originalColor = entMan.GetComponent<LightBulbComponent>(looseBulb).Color;

            lightPaint.PaintBulb(looseBulb, paint, remember: true);

            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<LightBulbComponent>(looseBulb).Color, Is.EqualTo(paint),
                    "Painting a loose bulb did not change its colour.");
                Assert.That(entMan.GetComponent<PaintedLightBulbComponent>(looseBulb).OriginalColor,
                    Is.EqualTo(originalColor),
                    "The bulb's original colour was not recorded for cleaning.");
            });

            // A fixture that spawns with a tube already installed.
            var fixture = entMan.SpawnEntity("Poweredlight", map.GridCoords);
            var installed = poweredLight.GetBulb(fixture);

            Assert.That(installed, Is.Not.Null, "Test fixture spawned without a bulb.");

            lightPaint.PaintBulb(installed!.Value, paint, remember: true);

            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<LightBulbComponent>(installed.Value).Color, Is.EqualTo(paint),
                    "Painting an installed bulb did not change the bulb's colour.");
                Assert.That(entMan.GetComponent<PointLightComponent>(fixture).Color, Is.EqualTo(paint),
                    "Painting an installed bulb did not reach the fixture's point light.");
            });

            // Cleaning restores the colour the bulb had before it was painted.
            var bulbComp = entMan.GetComponent<LightBulbComponent>(looseBulb);
            var remembered = entMan.GetComponent<PaintedLightBulbComponent>(looseBulb).OriginalColor;
            lightPaint.PaintBulb(looseBulb, remembered, remember: false);

            Assert.That(bulbComp.Color, Is.EqualTo(originalColor),
                "Restoring the remembered colour did not undo the paint.");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     An empty fixture has no bulb to paint, so it must be left alone entirely.
    /// </summary>
    [Test]
    public async Task EmptyFixtureHasNoBulbToPaint()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var poweredLight = server.System<SharedPoweredLightSystem>();
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var empty = entMan.SpawnEntity("PoweredlightEmpty", map.GridCoords);

            Assert.That(poweredLight.GetBulb(empty), Is.Null,
                "An empty fixture reported a bulb, so painting it would recolour the housing.");
        });

        await pair.CleanReturnAsync();
    }
}
