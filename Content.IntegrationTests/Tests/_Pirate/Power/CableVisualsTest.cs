// SPDX-License-Identifier: MIT

using Content.Shared.SubFloor;
using Content.Shared.Wires;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Pirate.Power;

[TestFixture]
public sealed class CableVisualsTest
{
    [TestCase("CableHV", "hvcable_")]
    [TestCase("CableMV", "mvcable_")]
    [TestCase("CableApcExtension", "lvcable_")]
    public async Task HiddenCablesKeepTheirConnectionSpritesCurrent(string prototype, string prefix)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;

        await client.WaitAssertion(() =>
        {
            var entities = client.EntMan;
            var appearance = client.System<AppearanceSystem>();
            var sprites = client.System<SpriteSystem>();
            var cable = entities.SpawnEntity(prototype, MapCoordinates.Nullspace);
            var sprite = entities.GetComponent<SpriteComponent>(cable);

            appearance.SetData(cable, SubFloorVisuals.Covered, true);
            appearance.SetData(cable, WireVisVisuals.ConnectedMask, WireVisDirFlags.East | WireVisDirFlags.West);
            appearance.OnChangeData(cable, sprite);

            Assert.That(sprite.Visible, Is.False, "Updating connections must not reveal a cable under the floor.");
            Assert.That(sprite[0].RsiState.Name, Is.EqualTo($"{prefix}12"),
                "A hidden cable must retain the connected sprite rather than the disconnected prototype state.");

            // A neighbour is removed while the cable is still hidden.
            appearance.SetData(cable, WireVisVisuals.ConnectedMask, WireVisDirFlags.West);
            appearance.OnChangeData(cable, sprite);
            Assert.That(sprite.Visible, Is.False);
            Assert.That(sprite[0].RsiState.Name, Is.EqualTo($"{prefix}8"));

            // Visibility can change without another connection-mask update.
            sprites.SetVisible((cable, sprite), true);
            Assert.That(sprite[0].RsiState.Name, Is.EqualTo($"{prefix}8"));

            appearance.SetData(cable, SubFloorVisuals.Covered, false);
            appearance.OnChangeData(cable, sprite);
            Assert.That(sprite.Visible, Is.True);
            Assert.That(sprite[0].RsiState.Name, Is.EqualTo($"{prefix}8"));

            entities.DeleteEntity(cable);
        });

        await pair.CleanReturnAsync();
    }
}
